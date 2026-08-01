using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Argus.Api.Services;
using Argus.Api.Services.Lookups;
using Argus.Api.WebApiPoco.Common;
using Argus.Api.WebApiPoco.Installations;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Tests;

public class LookupServiceTests
{
    /// <summary>
    /// How the lookup screens actually save: the row is read, one field is changed, and the whole
    /// DTO goes back. Any field the write side accepts but the read side does not return arrives
    /// as a default and overwrites the stored value — which is exactly the bug below.
    /// </summary>
    private static LookupUpsertDto ResubmittedAsTheUiWould(LookupItemDto read, string newName) => new()
    {
        Name = newName,
        Description = read.Description,
        SortOrder = read.SortOrder,
        IsLoadBalancer = read.IsLoadBalancer
    };

    /// <summary>Every kind the lookup API accepts writes for.</summary>
    public static TheoryData<LookupKind> WritableKinds()
    {
        var data = new TheoryData<LookupKind>();

        foreach (var descriptor in LookupRegistry.All.Where(x => !x.IsReadOnly))
        {
            data.Add(descriptor.Kind);
        }

        return data;
    }

    // --- The regression this suite exists for -------------------------------------------

    /// <summary>
    /// Renaming a DNS endpoint used to clear <c>IsLoadBalancer</c>: <see cref="LookupItemDto"/> did
    /// not return the flag, so the UI had nothing to send back and posted <c>false</c>. A load
    /// balancer fronting several machines is the case that justifies DnsEndpoints being its own
    /// table, so losing the flag silently discards the point of the model.
    /// </summary>
    [Fact]
    public async Task Renaming_a_dns_endpoint_keeps_the_load_balancer_flag()
    {
        using var testDb = TestDb.CreateSeeded();

        LookupItemDto read;

        await using (var db = testDb.NewContext())
        {
            read = (await new LookupService(db).GetByIdAsync(LookupKind.DnsEndpoints, TestDb.PahaEndpoint))!;
        }

        Assert.True(read.IsLoadBalancer, "the seeded endpoint is a load balancer to begin with");

        await using (var db = testDb.NewContext())
        {
            var updated = await new LookupService(db).UpdateAsync(
                LookupKind.DnsEndpoints,
                TestDb.PahaEndpoint,
                ResubmittedAsTheUiWould(read, "https://paha2.ga.local"));

            Assert.NotNull(updated);
            Assert.Equal("https://paha2.ga.local", updated.Name);
            Assert.True(updated.IsLoadBalancer);
        }

        await using (var assert = testDb.NewContext())
        {
            var endpoint = await assert.DnsEndpoints.SingleAsync(x => x.Id == TestDb.PahaEndpoint);

            Assert.Equal("https://paha2.ga.local", endpoint.Name);
            Assert.True(endpoint.IsLoadBalancer);
        }
    }

    /// <summary>The same defect on the other lookup that carries an extra field.</summary>
    [Fact]
    public async Task Renaming_a_stage_keeps_its_sort_order()
    {
        using var testDb = TestDb.CreateSeeded();

        LookupItemDto read;

        await using (var db = testDb.NewContext())
        {
            read = (await new LookupService(db).GetByIdAsync(LookupKind.AppStageNames, TestDb.StageRc0))!;
        }

        Assert.Equal(2, read.SortOrder);

        await using (var db = testDb.NewContext())
        {
            var updated = await new LookupService(db).UpdateAsync(
                LookupKind.AppStageNames, TestDb.StageRc0, ResubmittedAsTheUiWould(read, "RC1"));

            Assert.NotNull(updated);
            Assert.Equal(2, updated.SortOrder);
        }

        // Ordering is what SortOrder is for, so assert the consequence, not just the column.
        await using (var db = testDb.NewContext())
        {
            var stages = await new LookupService(db).GetAllAsync(LookupKind.AppStageNames);

            Assert.Equal(new[] { "MAIN", "RC1" }, stages.Select(x => x.Name));
        }
    }

    /// <summary>
    /// The structural guard: whatever <see cref="LookupUpsertDto"/> accepts has to be readable
    /// from <see cref="LookupItemDto"/>. Adding a write-only field reintroduces the whole class of
    /// bug above, and this fails the moment someone does.
    /// </summary>
    [Fact]
    public void Every_field_the_write_side_accepts_can_be_read_back()
    {
        var readable = typeof(LookupItemDto).GetProperties().Select(x => x.Name).ToHashSet();

        var writeOnly = typeof(LookupUpsertDto).GetProperties()
            .Where(x => !readable.Contains(x.Name))
            .Select(x => x.Name)
            .ToList();

        Assert.True(
            writeOnly.Count == 0,
            $"LookupUpsertDto accepts fields LookupItemDto never returns: {string.Join(", ", writeOnly)}. " +
            "An edit will reset them to their default. Add them to LookupItemDto.");
    }

    /// <summary>
    /// The behavioural version of the guard above, run against every writable kind rather than
    /// just the two that once had the bug: read a row, send it straight back with a new name, and
    /// nothing else may change. A descriptor whose Projection and Apply disagree fails here.
    /// </summary>
    [Theory]
    [MemberData(nameof(WritableKinds))]
    public async Task Resubmitting_a_row_unchanged_preserves_every_field(LookupKind kind)
    {
        using var testDb = TestDb.CreateSeeded();

        LookupItemDto created;

        await using (var db = testDb.NewContext())
        {
            created = await new LookupService(db).CreateAsync(kind, new LookupUpsertDto
            {
                Name = "round-trip-source",
                Description = "described",
                SortOrder = 42,
                IsLoadBalancer = true
            });
        }

        await using (var db = testDb.NewContext())
        {
            var updated = await new LookupService(db).UpdateAsync(
                kind, created.Id, ResubmittedAsTheUiWould(created, "round-trip-renamed"));

            Assert.NotNull(updated);
            Assert.Equal("round-trip-renamed", updated.Name);
            Assert.Equal(created.Description, updated.Description);
            Assert.Equal(created.SortOrder, updated.SortOrder);
            Assert.Equal(created.IsLoadBalancer, updated.IsLoadBalancer);
        }
    }

    // --- The generic layer itself ----------------------------------------------------------

    /// <summary>
    /// Adding a <see cref="LookupKind"/> without registering a descriptor would only fail at the
    /// first request for that kind. This turns it into a failing test instead.
    /// </summary>
    [Fact]
    public void Every_lookup_kind_has_a_descriptor()
    {
        var registered = LookupRegistry.All.Select(x => x.Kind).ToHashSet();

        var missing = Enum.GetValues<LookupKind>().Where(kind => !registered.Contains(kind)).ToList();

        Assert.True(missing.Count == 0, $"No descriptor registered for: {string.Join(", ", missing)}.");
    }

    /// <summary>
    /// The UI builds its tabs, its form fields and its length limits from <c>GET /api/lookups</c>,
    /// so a kind whose metadata is blank does not fail here — it renders as an unlabelled tab with
    /// an input that accepts nothing. The point of moving these facts to the server was to have one
    /// copy; this checks that copy is filled in.
    /// </summary>
    [Fact]
    public void Every_lookup_kind_describes_itself_for_the_ui()
    {
        using var testDb = TestDb.CreateSeeded();
        using var db = testDb.NewContext();

        var metadata = new LookupService(db).GetMetadata();

        Assert.Equal(Enum.GetValues<LookupKind>().Length, metadata.Count);

        foreach (var meta in metadata)
        {
            Assert.False(string.IsNullOrWhiteSpace(meta.Label), $"{meta.Kind} has no label.");
            Assert.False(string.IsNullOrWhiteSpace(meta.Singular), $"{meta.Kind} has no singular.");
            Assert.True(meta.MaxNameLength > 0, $"{meta.Kind} reports no name length.");

            // Lower-case so a client can put it straight into a url.
            Assert.Equal(meta.Kind.ToLowerInvariant(), meta.Kind);
        }
    }

    /// <summary>
    /// The name length check reads <c>HasMaxLength</c> off the EF model rather than a hand-kept
    /// table. A lookup configured without one would only fail as a raw database error on save.
    /// </summary>
    [Fact]
    public async Task Every_lookup_declares_a_maximum_name_length()
    {
        using var testDb = TestDb.CreateSeeded();
        await using var db = testDb.NewContext();

        foreach (var kind in Enum.GetValues<LookupKind>())
        {
            var tooLong = new string('x', 1024);

            var error = await Assert.ThrowsAnyAsync<Exception>(
                () => new LookupService(db).CreateAsync(kind, new LookupUpsertDto { Name = tooLong }));

            // Read-only kinds refuse the write outright, which is also a pass.
            Assert.True(
                error is ArgumentException or NotSupportedException,
                $"{kind} failed with {error.GetType().Name} instead of a validation error: {error.Message}");
        }
    }

    /// <summary>
    /// The limit each kind actually enforces is its own column width, not the 512 on
    /// <see cref="LookupUpsertDto"/> — that annotation only exists because one shared DTO cannot
    /// express nine widths. A 512-character physical path is legal; a 512-character machine name is
    /// not. <c>Every_lookup_declares_a_maximum_name_length</c> only proves 1024 characters fail
    /// somewhere, which a single blanket check would also satisfy, so it cannot see the difference.
    /// This pins the boundary from both sides: exactly at the limit saves, one over is rejected as a
    /// validation error rather than reaching the database as a raw SqlException.
    /// </summary>
    [Theory]
    [MemberData(nameof(WritableKinds))]
    public async Task A_lookup_name_is_measured_against_that_kinds_own_limit(LookupKind kind)
    {
        using var testDb = TestDb.CreateSeeded();
        await using var db = testDb.NewContext();

        var max = db.Model
                    .FindEntityType(LookupRegistry.Get(kind).EntityType)!
                    .FindProperty(nameof(ILookupEntity.Name))!
                    .GetMaxLength()!
                    .Value;

        var atTheLimit = await new LookupService(db).CreateAsync(
            kind, new LookupUpsertDto { Name = new string('x', max) });

        Assert.Equal(max, atTheLimit.Name.Length);

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => new LookupService(db).CreateAsync(
                      kind, new LookupUpsertDto { Name = new string('y', max + 1) }));

        Assert.Contains(max.ToString(), error.Message);
    }

    /// <summary>
    /// Repositories carry a type and installation links that the shared lookup payload cannot
    /// express, so writing one through here would quietly destroy both. It is readable and
    /// nothing more.
    /// </summary>
    [Fact]
    public async Task Repositories_are_readable_but_not_writable_through_the_lookup_api()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            db.AppRepositories.Add(new AppRepository { Name = "git://git.local/callcenter.git" });
            await db.SaveChangesAsync();
        }

        await using (var db = testDb.NewContext())
        {
            var repositories = await new LookupService(db).GetAllAsync(LookupKind.AppRepositories);

            Assert.Equal(new[] { "git://git.local/callcenter.git" }, repositories.Select(x => x.Name));
        }

        await using (var db = testDb.NewContext())
        {
            var service = new LookupService(db);

            await Assert.ThrowsAsync<NotSupportedException>(
                () => service.CreateAsync(LookupKind.AppRepositories, new LookupUpsertDto { Name = "svn://x" }));

            await Assert.ThrowsAsync<NotSupportedException>(
                () => service.UpdateAsync(LookupKind.AppRepositories, 1, new LookupUpsertDto { Name = "svn://x" }));

            await Assert.ThrowsAsync<NotSupportedException>(
                () => service.DeleteAsync(LookupKind.AppRepositories, 1));
        }
    }

    // --- The promise of the normalized model ---------------------------------------------

    /// <summary>
    /// The reason shared values live in lookup tables: renaming a machine is one row, and every
    /// installation that references it reads the new name at once.
    /// </summary>
    [Fact]
    public async Task Renaming_a_machine_renames_it_on_every_installation()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            var service = new InstallationService(db);
            var api = await TestDb.RootPathIdAsync(db, "/api");

            await service.CreateInstallationAsync(Deployment(TestDb.RootSlash));
            await service.CreateInstallationAsync(Deployment(api));
        }

        await using (var db = testDb.NewContext())
        {
            var read = (await new LookupService(db).GetByIdAsync(LookupKind.Machines, TestDb.Gaiis1))!;

            await new LookupService(db).UpdateAsync(
                LookupKind.Machines, TestDb.Gaiis1, ResubmittedAsTheUiWould(read, "GAIIS1-RENAMED"));
        }

        await using (var db = testDb.NewContext())
        {
            var page = await new InstallationService(db).GetInstallationsAsync(new InstallationFilterDto());

            Assert.Equal(2, page.TotalCount);
            Assert.All(page.Items, x => Assert.Equal("GAIIS1-RENAMED", x.MachineName));
        }
    }

    /// <summary>
    /// Hiding a lookup that installations still point at would leave those rows showing a blank
    /// name, so it is refused rather than cascaded.
    /// </summary>
    [Fact]
    public async Task A_lookup_still_used_by_an_installation_cannot_be_deleted()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            await new InstallationService(db).CreateInstallationAsync(Deployment(TestDb.RootSlash));
        }

        await using (var db = testDb.NewContext())
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => new LookupService(db).DeleteAsync(LookupKind.Machines, TestDb.Gaiis1));
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.True(await assert.Machines.AnyAsync(x => x.Id == TestDb.Gaiis1));
        }
    }

    /// <summary>
    /// The counterpart, and the reason the in-use check is rooted in ApplicationInstallations
    /// rather than in the link table: a decommissioned installation is gone as far as the
    /// inventory is concerned, so the tag it used must be free to retire too.
    /// </summary>
    [Fact]
    public async Task A_tag_used_only_by_a_decommissioned_installation_can_be_deleted()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            var service = new InstallationService(db);

            var dto = Deployment(TestDb.RootSlash);
            dto.TagIds.Add(TestDb.TagWeb);

            var created = await service.CreateInstallationAsync(dto);
            await service.DeleteInstallationAsync(created.Id);
        }

        await using (var db = testDb.NewContext())
        {
            Assert.True(await new LookupService(db).DeleteAsync(LookupKind.Tags, TestDb.TagWeb));
        }
    }

    [Fact]
    public async Task A_name_that_already_exists_is_rejected()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();

        await Assert.ThrowsAsync<ArgumentException>(
            () => new LookupService(db).CreateAsync(LookupKind.Machines, new LookupUpsertDto { Name = "GAIIS2" }));
    }

    /// <summary>Deleting an unused lookup hides it from the dropdowns but keeps the history.</summary>
    [Fact]
    public async Task Deleting_an_unused_lookup_hides_it_but_keeps_the_row()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            Assert.True(await new LookupService(db).DeleteAsync(LookupKind.Machines, TestDb.Gaiis2));
        }

        await using (var db = testDb.NewContext())
        {
            var machines = await new LookupService(db).GetAllAsync(LookupKind.Machines);

            Assert.DoesNotContain("GAIIS2", machines.Select(x => x.Name));
        }

        await using (var assert = testDb.NewContext())
        {
            var hidden = await assert.Machines.IgnoreQueryFilters()
                .SingleAsync(x => x.Id == TestDb.Gaiis2);

            Assert.False(hidden.IsEnabled);
        }
    }

    /// <summary>
    /// The duplicate check only sees live rows, so the unique index has to be filtered the same
    /// way. Unfiltered, this sequence passes validation and then dies as a raw database error.
    /// </summary>
    [Fact]
    public async Task A_retired_name_can_be_used_again()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            Assert.True(await new LookupService(db).DeleteAsync(LookupKind.Machines, TestDb.Gaiis2));
        }

        await using (var db = testDb.NewContext())
        {
            var recreated = await new LookupService(db)
                .CreateAsync(LookupKind.Machines, new LookupUpsertDto { Name = "GAIIS2" });

            Assert.NotEqual(TestDb.Gaiis2, recreated.Id);
        }

        // The retired row is still there — the history of the old GAIIS2 survives.
        await using (var assert = testDb.NewContext())
        {
            var all = await assert.Machines.IgnoreQueryFilters()
                .Where(x => x.Name == "GAIIS2")
                .ToListAsync();

            Assert.Equal(2, all.Count);
            Assert.Single(all, x => !x.IsEnabled);
        }
    }

    /// <summary>Deleting an id that is not there is a miss, not a validation failure.</summary>
    [Fact]
    public async Task Deleting_a_lookup_that_does_not_exist_reports_a_miss()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();

        Assert.False(await new LookupService(db).DeleteAsync(LookupKind.Machines, 999));
    }

    private static InstallationUpsertDto Deployment(int rootPathId) => new()
    {
        MachineId = TestDb.Gaiis1,
        AppNameId = TestDb.CallCenter,
        AppStageNameId = TestDb.StageMain,
        ProcessorArchitectureId = TestDb.X64,
        DnsEndpointId = TestDb.PahaEndpoint,
        RootPathId = rootPathId,
        PhysicalPathId = TestDb.DiskDefault,
        IsActive = true,
        ValidFromDate = new DateOnly(2026, 1, 1)
    };
}
