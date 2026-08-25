using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Argus.Api.Database.Interceptors;
using Argus.Api.Services;
using Argus.Api.WebApiPoco.Installations;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Tests;

/// <summary>
/// The change history of one installation. What is asserted here is mostly what the journal must
/// *not* do: invent rows for saves that changed nothing, record foreign keys as numbers, or let a
/// later rename in a lookup rewrite what it already said happened.
/// </summary>
public class EntityJournalTests
{
    private static InstallationUpsertDto Deployment(
        int machineId = TestDb.Gaiis1,
        List<int>? tagIds = null,
        List<int>? repositoryIds = null,
        DateOnly? validTo = null) => new()
        {
            MachineId = machineId,
            AppNameId = TestDb.Helpdesk,
            AppStageNameId = TestDb.StageMain,
            ProcessorArchitectureId = TestDb.X64,
            DnsEndpointId = TestDb.PahaEndpoint,
            RootPathId = TestDb.RootSlash,
            PhysicalPathId = TestDb.DiskDefault,
            TagIds = tagIds ?? new List<int>(),
            RepositoryIds = repositoryIds ?? new List<int>(),
            IsActive = true,
            ValidFromDate = new DateOnly(2026, 1, 1),
            ValidToDate = validTo
        };

    private static async Task<int> CreateInstallationAsync(TestDb testDb, InstallationUpsertDto? dto = null)
    {
        await using var db = testDb.NewJournalingContext();
        var created = await new InstallationService(db).CreateInstallationAsync(dto ?? Deployment());

        return created.Id;
    }

    private static async Task<List<EntityJournalEntry>> JournalAsync(TestDb testDb, int installationId)
    {
        await using var db = testDb.NewContext();

        return await db.EntityJournal
            .Where(x => x.InstallationId == installationId)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    [Fact]
    public async Task Creating_an_installation_writes_one_Created_row_with_its_new_id()
    {
        using var testDb = TestDb.CreateSeeded();

        var id = await CreateInstallationAsync(testDb);
        var journal = await JournalAsync(testDb, id);

        var entry = Assert.Single(journal);
        Assert.Equal(JournalActions.Created, entry.Action);
        Assert.Equal(nameof(ApplicationInstallation), entry.EntityName);
        // The Id only exists after the insert; this is the assertion that the deferred write works.
        Assert.NotEqual(0, entry.InstallationId);
        Assert.Equal("tester", entry.ChangedBy);
        Assert.Null(entry.Field);
    }

    /// <summary>
    /// The core case: a moved installation, recorded by the names that were on screen — "BOREAS01",
    /// not "1" — with the raw Ids kept alongside for anyone querying by machine.
    /// </summary>
    [Fact]
    public async Task Changing_a_reference_records_the_names_and_the_ids()
    {
        using var testDb = TestDb.CreateSeeded();
        var id = await CreateInstallationAsync(testDb);

        await using (var db = testDb.NewJournalingContext())
        {
            await new InstallationService(db).UpdateInstallationAsync(
                id,
                Deployment(machineId: TestDb.Gaiis2));
        }

        var entry = Assert.Single(await JournalAsync(testDb, id), x => x.Field == "Machine");

        Assert.Equal(JournalActions.Updated, entry.Action);
        Assert.Equal("BOREAS01", entry.OldValue);
        Assert.Equal("BOREAS02", entry.NewValue);
        Assert.Equal(TestDb.Gaiis1, entry.OldValueId);
        Assert.Equal(TestDb.Gaiis2, entry.NewValueId);
    }

    [Fact]
    public async Task Two_fields_changed_in_one_save_share_one_change_set()
    {
        using var testDb = TestDb.CreateSeeded();
        var id = await CreateInstallationAsync(testDb);

        await using (var db = testDb.NewJournalingContext())
        {
            await new InstallationService(db).UpdateInstallationAsync(
                id,
                Deployment(machineId: TestDb.Gaiis2, validTo: new DateOnly(2026, 12, 31)));
        }

        var updates = (await JournalAsync(testDb, id))
            .Where(x => x.Action == JournalActions.Updated)
            .ToList();

        Assert.Equal(2, updates.Count);
        Assert.Single(updates.Select(x => x.ChangeSetId).Distinct());
        Assert.Contains(updates, x => x.Field == "Valid to" && x.OldValue is null && x.NewValue == "2026-12-31");
        // ModifiedUtc moves on every edit and is not news.
        Assert.DoesNotContain(updates, x => x.Field is "ModifiedUtc" or "CreatedUtc");
    }

    /// <summary>
    /// Saving the same values again is the commonest thing a user does — open the dialog, look,
    /// press Save. If that wrote rows, the history would be mostly noise within a week.
    /// </summary>
    [Fact]
    public async Task Saving_without_changing_anything_writes_nothing()
    {
        using var testDb = TestDb.CreateSeeded();
        var id = await CreateInstallationAsync(testDb);

        await using (var db = testDb.NewJournalingContext())
        {
            await new InstallationService(db).UpdateInstallationAsync(id, Deployment());
        }

        Assert.Equal(JournalActions.Created, Assert.Single(await JournalAsync(testDb, id)).Action);
    }

    [Fact]
    public async Task Adding_and_removing_a_tag_is_recorded_by_name()
    {
        using var testDb = TestDb.CreateSeeded();
        var id = await CreateInstallationAsync(testDb);

        await using (var db = testDb.NewJournalingContext())
        {
            await new InstallationService(db).UpdateInstallationAsync(
                id,
                Deployment(tagIds: new List<int> { TestDb.TagWeb }));
        }

        await using (var db = testDb.NewJournalingContext())
        {
            await new InstallationService(db).UpdateInstallationAsync(id, Deployment());
        }

        var links = (await JournalAsync(testDb, id)).Where(x => x.Field == "Tag").ToList();

        Assert.Equal(2, links.Count);
        Assert.Equal(JournalActions.LinkAdded, links[0].Action);
        Assert.Equal("web", links[0].NewValue);
        Assert.Null(links[0].OldValue);
        Assert.Equal(JournalActions.LinkRemoved, links[1].Action);
        Assert.Equal("web", links[1].OldValue);
    }

    /// <summary>
    /// The case a journal written by hand inside InstallationService would have missed: the link
    /// is made from the Repositories screen, but it is the installation's history.
    /// </summary>
    [Fact]
    public async Task Linking_a_repository_from_the_repository_side_lands_on_the_installation()
    {
        using var testDb = TestDb.CreateSeeded();
        var id = await CreateInstallationAsync(testDb);

        await using (var db = testDb.NewJournalingContext("repo-editor"))
        {
            await new AppRepositoryService(db).CreateAsync(new AppRepositoryUpsertDto
            {
                RepositoryUrl = "https://git.example.com/helpdesk.git",
                RepositoryTypeId = TestDb.RepoTypeGit,
                InstallationIds = new List<int> { id }
            });
        }

        var entry = Assert.Single(await JournalAsync(testDb, id), x => x.Field == "Repository");

        Assert.Equal(JournalActions.LinkAdded, entry.Action);
        Assert.Equal(nameof(InstallationRepository), entry.EntityName);
        Assert.Equal("https://git.example.com/helpdesk.git", entry.NewValue);
        Assert.Equal("repo-editor", entry.ChangedBy);
    }

    [Fact]
    public async Task Soft_deleting_records_a_delete_rather_than_a_flag()
    {
        using var testDb = TestDb.CreateSeeded();
        var id = await CreateInstallationAsync(testDb);

        await using (var db = testDb.NewJournalingContext())
        {
            await new InstallationService(db).DeleteInstallationAsync(id);
        }

        var entry = Assert.Single(await JournalAsync(testDb, id), x => x.Action == JournalActions.Deleted);

        Assert.Null(entry.Field);
        Assert.DoesNotContain(await JournalAsync(testDb, id), x => x.Field == "IsEnabled");
    }

    /// <summary>
    /// The anti-rewrite guarantee. Resolving names at read time would make this test fail, and
    /// would make every historical row silently follow whatever the lookup says today.
    /// </summary>
    [Fact]
    public async Task Renaming_a_lookup_afterwards_does_not_change_the_history()
    {
        using var testDb = TestDb.CreateSeeded();
        var id = await CreateInstallationAsync(testDb);

        await using (var db = testDb.NewJournalingContext())
        {
            await new InstallationService(db).UpdateInstallationAsync(
                id,
                Deployment(machineId: TestDb.Gaiis2));
        }

        await using (var db = testDb.NewContext())
        {
            var machine = await db.Machines.FirstAsync(x => x.Id == TestDb.Gaiis2);
            machine.Name = "BOREAS-PROD-02";
            await db.SaveChangesAsync();
        }

        var entry = Assert.Single(await JournalAsync(testDb, id), x => x.Field == "Machine");

        Assert.Equal("BOREAS02", entry.NewValue);
        Assert.Equal(TestDb.Gaiis2, entry.NewValueId);
    }

    /// <summary>The seeder's contract: demo data is not somebody's edit.</summary>
    [Fact]
    public async Task Suppressed_journaling_writes_nothing()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewJournalingContext())
        {
            db.JournalingSuppressed = true;
            await new InstallationService(db).CreateInstallationAsync(Deployment());
        }

        await using var assert = testDb.NewContext();
        Assert.Empty(assert.EntityJournal);
    }

    [Fact]
    public async Task Reading_installations_writes_nothing()
    {
        using var testDb = TestDb.CreateSeeded();
        var id = await CreateInstallationAsync(testDb);

        await using (var db = testDb.NewJournalingContext())
        {
            var service = new InstallationService(db);
            await service.GetInstallationByIdAsync(id);
            await service.GetInstallationsAsync(new InstallationFilterDto());
        }

        Assert.Single(await JournalAsync(testDb, id));
    }

    [Fact]
    public async Task The_service_returns_the_newest_entries_first_and_404s_an_unknown_installation()
    {
        using var testDb = TestDb.CreateSeeded();
        var id = await CreateInstallationAsync(testDb);

        await using (var db = testDb.NewJournalingContext())
        {
            await new InstallationService(db).UpdateInstallationAsync(
                id,
                Deployment(machineId: TestDb.Gaiis2));
        }

        await using var read = testDb.NewContext();
        var service = new EntityJournalService(read);

        var entries = await service.GetForInstallationAsync(id, maxEntries: 200);

        Assert.NotNull(entries);
        Assert.Equal(JournalActions.Updated, entries![0].Action);
        Assert.Equal(JournalActions.Created, entries[^1].Action);

        // Null, not an empty list: "no such installation" is a different answer from "no changes".
        Assert.Null(await service.GetForInstallationAsync(9999, maxEntries: 200));
    }

    [Fact]
    public async Task The_service_clamps_how_much_history_it_will_return()
    {
        using var testDb = TestDb.CreateSeeded();
        var id = await CreateInstallationAsync(testDb);

        await using var read = testDb.NewContext();

        Assert.Single((await new EntityJournalService(read).GetForInstallationAsync(id, maxEntries: 100_000))!);
    }
}
