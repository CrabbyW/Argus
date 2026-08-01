using Argus.Api.Database;
using Argus.Api.Services;
using Argus.Api.WebApiPoco.Installations;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Tests;

public class InstallationServiceTests
{
    private static InstallationUpsertDto Deployment(
        int rootPathId = TestDb.RootSlash,
        int? physicalPathId = TestDb.DiskDefault,
        int machineId = TestDb.Gaiis1,
        int appNameId = TestDb.CallCenter,
        int stageId = TestDb.StageMain,
        string validFrom = "2026-01-01",
        string? validTo = null) => new()
        {
            MachineId = machineId,
            AppNameId = appNameId,
            AppStageNameId = stageId,
            ProcessorArchitectureId = TestDb.X64,
            DnsEndpointId = TestDb.PahaEndpoint,
            RootPathId = rootPathId,
            PhysicalPathId = physicalPathId,
            IsActive = true,
            ValidFromDate = DateOnly.Parse(validFrom),
            ValidToDate = validTo is null ? null : DateOnly.Parse(validTo)
        };

    // --- The regression this suite exists for -------------------------------------------

    /// <summary>
    /// Decommissioning is a soft delete, so the retired row stays in the table. Installing the
    /// same application at the same place again is an ordinary event in an inventory — a second
    /// period of validity, not a duplicate — and it used to fail on the unique index with a 500.
    /// </summary>
    [Fact]
    public async Task Reinstalling_a_decommissioned_deployment_is_allowed()
    {
        using var testDb = TestDb.CreateSeeded();

        int retiredId;

        await using (var db = testDb.NewContext())
        {
            var created = await new InstallationService(db).CreateInstallationAsync(Deployment());
            retiredId = created.Id;
        }

        await using (var db = testDb.NewContext())
        {
            Assert.True(await new InstallationService(db).DeleteInstallationAsync(retiredId));
        }

        await using (var db = testDb.NewContext())
        {
            var recreated = await new InstallationService(db).CreateInstallationAsync(Deployment());

            Assert.NotEqual(retiredId, recreated.Id);
        }

        // Both periods survive: the retired row is still there for historical questions.
        await using (var assert = testDb.NewContext())
        {
            var all = await assert.ApplicationInstallations.IgnoreQueryFilters().ToListAsync();

            Assert.Equal(2, all.Count);
            Assert.Single(all, x => !x.IsEnabled);
            Assert.Single(all, x => x.IsEnabled);
        }
    }

    /// <summary>
    /// The half of the fix that lives in the database rather than in the service: even with the
    /// service bypassed entirely, SQL must accept the second row.
    /// </summary>
    [Fact]
    public async Task The_unique_index_ignores_decommissioned_rows()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();
        var service = new InstallationService(db);

        var first = await service.CreateInstallationAsync(Deployment());
        await service.DeleteInstallationAsync(first.Id);

        db.ApplicationInstallations.Add(new Argus.Api.Database.Entities.ApplicationInstallation
        {
            MachineId = TestDb.Gaiis1,
            AppNameId = TestDb.CallCenter,
            AppStageNameId = TestDb.StageMain,
            ProcessorArchitectureId = TestDb.X64,
            RootPathId = TestDb.RootSlash,
            ValidFromDate = new DateOnly(2026, 6, 1)
        });

        // No exception: the index is filtered on IsEnabled = 1.
        await db.SaveChangesAsync();
    }

    // --- Uniqueness among rows that are actually there ----------------------------------

    [Fact]
    public async Task Installing_the_same_thing_twice_at_the_same_path_is_rejected()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();
        var service = new InstallationService(db);

        await service.CreateInstallationAsync(Deployment());

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateInstallationAsync(Deployment()));

        Assert.Contains("already installed", error.Message);
    }

    [Fact]
    public async Task The_same_app_and_stage_may_sit_at_different_paths()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();
        var service = new InstallationService(db);

        var mirror = await TestDb.RootPathIdAsync(db, "/callcenter.mirror");

        await service.CreateInstallationAsync(Deployment());
        await service.CreateInstallationAsync(Deployment(rootPathId: mirror));

        Assert.Equal(2, await db.ApplicationInstallations.CountAsync());
    }

    [Fact]
    public async Task Saving_an_installation_unchanged_does_not_clash_with_itself()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();
        var service = new InstallationService(db);

        var created = await service.CreateInstallationAsync(Deployment());

        var elsewhere = await TestDb.PhysicalPathIdAsync(db, @"d:\sites\callcenter");

        var dto = Deployment(physicalPathId: elsewhere);
        var updated = await service.UpdateInstallationAsync(created.Id, dto);

        Assert.NotNull(updated);
        Assert.Equal(@"d:\sites\callcenter", updated!.PhysicalPath);
    }

    // --- Validation ----------------------------------------------------------------------

    [Fact]
    public async Task An_end_date_before_the_start_date_is_rejected()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => new InstallationService(db).CreateInstallationAsync(
                Deployment(validFrom: "2026-05-01", validTo: "2026-04-01")));

        Assert.Contains("ValidToDate", error.Message);
    }

    [Fact]
    public async Task A_machine_that_does_not_exist_is_rejected()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => new InstallationService(db).CreateInstallationAsync(Deployment(machineId: 999)));

        Assert.Contains("Machine 999", error.Message);
    }

    /// <summary>
    /// The rule that makes ApplicationInstallations the last table to be filled: a lookup Id that
    /// is not in its table yet is refused, rather than creating the row implicitly.
    /// </summary>
    [Fact]
    public async Task A_root_path_that_does_not_exist_is_rejected()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => new InstallationService(db).CreateInstallationAsync(Deployment(rootPathId: 999)));

        Assert.Contains("RootPath 999", error.Message);
    }

    // --- Soft delete ---------------------------------------------------------------------

    [Fact]
    public async Task Deleting_hides_the_row_but_keeps_it()
    {
        using var testDb = TestDb.CreateSeeded();

        int id;

        await using (var db = testDb.NewContext())
        {
            var service = new InstallationService(db);
            id = (await service.CreateInstallationAsync(Deployment())).Id;
            await service.DeleteInstallationAsync(id);
        }

        await using (var db = testDb.NewContext())
        {
            var visible = await new InstallationService(db)
                .GetInstallationsAsync(new InstallationFilterDto());

            Assert.Empty(visible.Items);
        }

        await using (var db = testDb.NewContext())
        {
            var withRetired = await new InstallationService(db)
                .GetInstallationsAsync(new InstallationFilterDto { IncludeDisabled = true });

            Assert.Single(withRetired.Items);
            Assert.Equal(id, withRetired.Items[0].Id);
        }
    }

    // --- The date window ------------------------------------------------------------------

    /// <summary>
    /// "What was installed during this window?" must include an installation that started
    /// before the window and was still running inside it — overlap, not containment. This is
    /// the whole point of asking the question about a past quarter.
    /// </summary>
    [Fact]
    public async Task The_date_filter_matches_on_overlap_not_containment()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            var service = new InstallationService(db);

            var spansInto = await TestDb.RootPathIdAsync(db, "/spans-into");
            var overBefore = await TestDb.RootPathIdAsync(db, "/over-before");
            var stillThere = await TestDb.RootPathIdAsync(db, "/still-there");

            // Started long before the window, retired inside it.
            await service.CreateInstallationAsync(
                Deployment(rootPathId: spansInto, validFrom: "2025-01-01", validTo: "2026-03-15"));

            // Entirely before the window.
            await service.CreateInstallationAsync(
                Deployment(rootPathId: overBefore, validFrom: "2024-01-01", validTo: "2024-12-31"));

            // Still installed, started before the window — open-ended.
            await service.CreateInstallationAsync(
                Deployment(rootPathId: stillThere, validFrom: "2025-06-01"));
        }

        await using (var db = testDb.NewContext())
        {
            var result = await new InstallationService(db).GetInstallationsAsync(new InstallationFilterDto
            {
                ValidFrom = new DateOnly(2026, 3, 1),
                ValidTo = new DateOnly(2026, 3, 31)
            });

            var paths = result.Items.Select(x => x.RootPath).OrderBy(x => x).ToList();

            Assert.Equal(new[] { "/spans-into", "/still-there" }, paths);
        }
    }

    // --- Sorting and search -----------------------------------------------------------------

    [Fact]
    public async Task An_unknown_sort_column_falls_back_to_machine_name()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            var service = new InstallationService(db);
            await service.CreateInstallationAsync(Deployment(machineId: TestDb.Gaiis2));
            await service.CreateInstallationAsync(Deployment(machineId: TestDb.Gaiis1));
        }

        await using (var db = testDb.NewContext())
        {
            var result = await new InstallationService(db).GetInstallationsAsync(
                new InstallationFilterDto { SortBy = "'; drop table ApplicationInstallations; --" });

            Assert.Equal(
                new[] { "GAIIS1", "GAIIS2" },
                result.Items.Select(x => x.MachineName).ToArray());
        }
    }

    [Fact]
    public async Task Search_looks_inside_the_physical_path()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            var service = new InstallationService(db);

            var one = await TestDb.RootPathIdAsync(db, "/one");
            var two = await TestDb.RootPathIdAsync(db, "/two");
            var elsewhere = await TestDb.PhysicalPathIdAsync(db, @"e:\services\dataexchange.worker");

            await service.CreateInstallationAsync(Deployment(rootPathId: one));
            await service.CreateInstallationAsync(
                Deployment(rootPathId: two, physicalPathId: elsewhere));
        }

        await using (var db = testDb.NewContext())
        {
            var result = await new InstallationService(db).GetInstallationsAsync(
                new InstallationFilterDto { SearchTerm = "dataexchange.worker" });

            Assert.Single(result.Items);
            Assert.Equal("/two", result.Items[0].RootPath);
        }
    }

    // --- Paging ------------------------------------------------------------------------------

    [Fact]
    public async Task Paging_reports_the_total_across_all_pages()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            var service = new InstallationService(db);

            for (var i = 0; i < 5; i++)
            {
                var path = await TestDb.RootPathIdAsync(db, $"/app{i}");
                await service.CreateInstallationAsync(Deployment(rootPathId: path));
            }
        }

        await using (var db = testDb.NewContext())
        {
            var page = await new InstallationService(db).GetInstallationsAsync(
                new InstallationFilterDto { PageNumber = 2, PageSize = 2 });

            Assert.Equal(5, page.TotalCount);
            Assert.Equal(3, page.TotalPages);
            Assert.Equal(2, page.Items.Count);
        }
    }

    /// <summary>
    /// The grid shows the foreign keys themselves — that is the roadplan's fact table, a row of
    /// references and nothing else. They travel on the list row, so a mapper that forgets one
    /// leaves a blank column on the main screen with nothing else failing. Every reference is
    /// asserted against the entity it came from rather than against a literal, so this keeps
    /// holding if the seed changes.
    /// </summary>
    [Fact]
    public async Task A_list_row_reports_the_same_references_the_installation_holds()
    {
        using var testDb = TestDb.CreateSeeded();

        int createdId;

        await using (var db = testDb.NewContext())
        {
            var created = await new InstallationService(db).CreateInstallationAsync(
                Deployment(machineId: TestDb.Gaiis1, appNameId: TestDb.CallCenter));
            createdId = created.Id;
        }

        await using (var db = testDb.NewContext())
        {
            var entity = await db.ApplicationInstallations.SingleAsync(i => i.Id == createdId);

            var row = (await new InstallationService(db).GetInstallationsAsync(new InstallationFilterDto()))
                .Items.Single(i => i.Id == createdId);

            Assert.Equal(entity.MachineId, row.MachineId);
            Assert.Equal(entity.AppNameId, row.AppNameId);
            Assert.Equal(entity.AppStageNameId, row.AppStageNameId);
            Assert.Equal(entity.ProcessorArchitectureId, row.ProcessorArchitectureId);
            Assert.Equal(entity.DnsEndpointId, row.DnsEndpointId);
            Assert.Equal(entity.RootPathId, row.RootPathId);
            Assert.Equal(entity.PhysicalPathId, row.PhysicalPathId);
        }
    }

    /// <summary>
    /// The two optional references stay null on the way out. An Id view renders null as an empty
    /// cell; a mapper that defaulted them to 0 would print a reference to a lookup row that does
    /// not exist.
    /// </summary>
    [Fact]
    public async Task Optional_references_stay_null_on_a_list_row()
    {
        using var testDb = TestDb.CreateSeeded();

        int createdId;

        await using (var db = testDb.NewContext())
        {
            var upsert = Deployment(physicalPathId: null);
            upsert.DnsEndpointId = null;

            createdId = (await new InstallationService(db).CreateInstallationAsync(upsert)).Id;
        }

        await using (var db = testDb.NewContext())
        {
            var row = (await new InstallationService(db).GetInstallationsAsync(new InstallationFilterDto()))
                .Items.Single(i => i.Id == createdId);

            Assert.Null(row.DnsEndpointId);
            Assert.Null(row.PhysicalPathId);
        }
    }
}
