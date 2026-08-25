using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Argus.Api.Services;
using Argus.Api.WebApiPoco.Installations;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Tests;

/// <summary>
/// Tags and repositories are many-to-many, which is the part of the model most likely to be
/// rewritten by someone who has not read why it is shaped this way. These tests are the guard.
/// </summary>
public class InstallationLinkTests
{
    private static InstallationUpsertDto Deployment(
        int rootPathId = TestDb.RootSlash,
        List<int>? tagIds = null,
        List<int>? repositoryIds = null,
        int machineId = TestDb.Gaiis1,
        int appNameId = TestDb.Helpdesk,
        int stageId = TestDb.StageMain) => new()
        {
            MachineId = machineId,
            AppNameId = appNameId,
            AppStageNameId = stageId,
            ProcessorArchitectureId = TestDb.X64,
            DnsEndpointId = TestDb.PahaEndpoint,
            RootPathId = rootPathId,
            PhysicalPathId = TestDb.DiskDefault,
            TagIds = tagIds ?? new List<int>(),
            RepositoryIds = repositoryIds ?? new List<int>(),
            IsActive = true,
            ValidFromDate = new DateOnly(2026, 1, 1)
        };

    private static async Task<int> TagIdAsync(ArgusDbContext db, string name)
    {
        var existing = await db.Tags.FirstOrDefaultAsync(x => x.Name == name);

        if (existing is not null)
        {
            return existing.Id;
        }

        var created = new Tag { Name = name };
        db.Tags.Add(created);
        await db.SaveChangesAsync();

        return created.Id;
    }

    private static async Task<int> RepositoryIdAsync(ArgusDbContext db, string url)
    {
        var existing = await db.AppRepositories.FirstOrDefaultAsync(x => x.Name == url);

        if (existing is not null)
        {
            return existing.Id;
        }

        var created = new AppRepository { Name = url };
        db.AppRepositories.Add(created);
        await db.SaveChangesAsync();

        return created.Id;
    }

    // --- The regression this file exists for ---------------------------------------------

    /// <summary>
    /// The single most expensive mistake available in this codebase.
    ///
    /// <c>CountAsync</c> runs on the same query as the page, before paging. If the tag predicate
    /// is ever rewritten from <c>.Any()</c> to a join, one installation carrying two matching
    /// tags becomes two rows, TotalCount becomes 2, and every page count in the UI is quietly
    /// wrong — with the grid still showing the correct single row, so nothing looks broken.
    /// </summary>
    [Fact]
    public async Task Searching_by_tag_does_not_multiply_the_row_count()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            // Two tags that both match the search term "web".
            var web = TestDb.TagWeb;
            var webhook = await TagIdAsync(db, "webhook");

            await new InstallationService(db).CreateInstallationAsync(
                Deployment(tagIds: new List<int> { web, webhook }));
        }

        await using (var db = testDb.NewContext())
        {
            var page = await new InstallationService(db).GetInstallationsAsync(
                new InstallationFilterDto { SearchTerm = "web" });

            Assert.Equal(1, page.TotalCount);
            Assert.Single(page.Items);
            Assert.Equal(new[] { "web", "webhook" }, page.Items[0].Tags.OrderBy(x => x).ToArray());
        }
    }

    // --- Tag links -----------------------------------------------------------------------

    [Fact]
    public async Task Tags_are_saved_as_links_and_read_back()
    {
        using var testDb = TestDb.CreateSeeded();

        int installationId;

        await using (var db = testDb.NewContext())
        {
            var prod = await TagIdAsync(db, "prod");

            var created = await new InstallationService(db).CreateInstallationAsync(
                Deployment(tagIds: new List<int> { TestDb.TagWeb, prod }));

            installationId = created.Id;
        }

        await using (var db = testDb.NewContext())
        {
            var detail = await new InstallationService(db).GetInstallationByIdAsync(installationId);

            Assert.NotNull(detail);
            Assert.Equal(
                new[] { "prod", "web" },
                detail!.Tags.Select(x => x.Name).OrderBy(x => x).ToArray());
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.Equal(2, await assert.InstallationTags.CountAsync());
        }
    }

    /// <summary>
    /// The edit path diffs the links rather than deleting and re-adding them all. A test that
    /// only checked the end state would pass either way; this one checks that the row which did
    /// not change was left alone.
    /// </summary>
    [Fact]
    public async Task Editing_tags_adds_and_removes_only_what_changed()
    {
        using var testDb = TestDb.CreateSeeded();

        int installationId;
        int prodId;
        int serviceId;

        await using (var db = testDb.NewContext())
        {
            prodId = await TagIdAsync(db, "prod");
            serviceId = await TagIdAsync(db, "service");

            var created = await new InstallationService(db).CreateInstallationAsync(
                Deployment(tagIds: new List<int> { TestDb.TagWeb, prodId }));

            installationId = created.Id;
        }

        // web stays, prod goes, service arrives.
        await using (var db = testDb.NewContext())
        {
            await new InstallationService(db).UpdateInstallationAsync(
                installationId,
                Deployment(tagIds: new List<int> { TestDb.TagWeb, serviceId }));
        }

        await using (var assert = testDb.NewContext())
        {
            var links = await assert.InstallationTags
                .Where(x => x.InstallationId == installationId)
                .Select(x => x.TagId)
                .ToListAsync();

            Assert.Equal(2, links.Count);
            Assert.Contains(TestDb.TagWeb, links);
            Assert.Contains(serviceId, links);
            Assert.DoesNotContain(prodId, links);
        }
    }

    /// <summary>
    /// A composite primary key means a repeated Id in the payload is a crash, not a no-op, so
    /// the service de-duplicates before writing. A multiselect that fires twice on one click is
    /// enough to trigger it.
    /// </summary>
    [Fact]
    public async Task Duplicate_tag_ids_produce_one_link()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            await new InstallationService(db).CreateInstallationAsync(
                Deployment(tagIds: new List<int> { TestDb.TagWeb, TestDb.TagWeb, TestDb.TagWeb }));
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.Equal(1, await assert.InstallationTags.CountAsync());
        }
    }

    [Fact]
    public async Task Duplicate_repository_ids_produce_one_link()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            var repoId = await RepositoryIdAsync(db, "git://git.example.local/helpdesk.git");

            await new InstallationService(db).CreateInstallationAsync(
                Deployment(repositoryIds: new List<int> { repoId, repoId }));
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.Equal(1, await assert.InstallationRepositories.CountAsync());
        }
    }

    /// <summary>
    /// A repository shared by two installations must report both of them on either detail payload.
    ///
    /// The detail query has to include the repository's own link collection; without it, EF fills
    /// that collection by relationship fixup alone and it holds only the installation being read.
    /// Nothing looks broken — the detail screen shows the right repository — but the payload
    /// disagrees with GET /api/apprepositories, and anything that sent it back in a PUT would
    /// unlink the repository from its siblings, since InstallationIds is the complete target state.
    /// </summary>
    [Fact]
    public async Task A_shared_repository_reports_every_installation_on_the_detail_payload()
    {
        using var testDb = TestDb.CreateSeeded();

        int firstId;
        int secondId;
        int repoId;

        await using (var db = testDb.NewContext())
        {
            repoId = await RepositoryIdAsync(db, "git://git.example.local/helpdesk.git");
            var otherPath = await TestDb.RootPathIdAsync(db, "/helpdesk.rc0");

            var first = await new InstallationService(db).CreateInstallationAsync(
                Deployment(repositoryIds: new List<int> { repoId }));

            var second = await new InstallationService(db).CreateInstallationAsync(
                Deployment(rootPathId: otherPath, repositoryIds: new List<int> { repoId }));

            firstId = first.Id;
            secondId = second.Id;
        }

        await using (var db = testDb.NewContext())
        {
            var detail = await new InstallationService(db).GetInstallationByIdAsync(firstId);

            Assert.NotNull(detail);
            var repo = Assert.Single(detail!.AppRepositories);
            Assert.Equal(repoId, repo.Id);
            Assert.Equal(new[] { firstId, secondId }.OrderBy(x => x), repo.InstallationIds.OrderBy(x => x));
        }
    }

    [Fact]
    public async Task A_tag_that_does_not_exist_is_rejected()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new InstallationService(db).CreateInstallationAsync(
                Deployment(tagIds: new List<int> { 9999 })));
    }

    // --- Filters -------------------------------------------------------------------------

    [Fact]
    public async Task Filtering_by_tag_returns_only_installations_carrying_it()
    {
        using var testDb = TestDb.CreateSeeded();

        int prodId;

        await using (var db = testDb.NewContext())
        {
            prodId = await TagIdAsync(db, "prod");
            var otherPath = await TestDb.RootPathIdAsync(db, "/helpdesk.rc0");

            await new InstallationService(db).CreateInstallationAsync(
                Deployment(tagIds: new List<int> { TestDb.TagWeb }));

            await new InstallationService(db).CreateInstallationAsync(
                Deployment(rootPathId: otherPath, tagIds: new List<int> { prodId }));
        }

        await using (var db = testDb.NewContext())
        {
            var page = await new InstallationService(db).GetInstallationsAsync(
                new InstallationFilterDto { TagIds = { prodId } });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal(new[] { "prod" }, page.Items[0].Tags.ToArray());
        }
    }

    /// <summary>
    /// Tags are the one multi-value facet, and several of them are matched with OR: picking a
    /// second tag widens the result rather than narrowing it, which is what selecting two entries
    /// in a list looks like. An installation carrying both must still appear once.
    /// </summary>
    [Fact]
    public async Task Filtering_by_several_tags_returns_installations_carrying_any_of_them()
    {
        using var testDb = TestDb.CreateSeeded();

        int prodId;

        await using (var db = testDb.NewContext())
        {
            prodId = await TagIdAsync(db, "prod");
            var otherPath = await TestDb.RootPathIdAsync(db, "/helpdesk.rc0");

            // One row tagged "web", one tagged "prod", and one carrying both. Each differs in the
            // machine/app/stage/root-path key, which is unique.
            await new InstallationService(db).CreateInstallationAsync(
                Deployment(tagIds: new List<int> { TestDb.TagWeb }));

            await new InstallationService(db).CreateInstallationAsync(
                Deployment(rootPathId: otherPath, tagIds: new List<int> { prodId }));

            await new InstallationService(db).CreateInstallationAsync(
                Deployment(stageId: TestDb.StageRc0, tagIds: new List<int> { TestDb.TagWeb, prodId }));
        }

        await using (var db = testDb.NewContext())
        {
            var page = await new InstallationService(db).GetInstallationsAsync(
                new InstallationFilterDto { TagIds = { TestDb.TagWeb, prodId } });

            // All three: two match one tag each, the third matches both but is still one row.
            Assert.Equal(3, page.TotalCount);
        }
    }

    [Fact]
    public async Task Filtering_by_repository_returns_only_installations_linked_to_it()
    {
        using var testDb = TestDb.CreateSeeded();

        int repoId;

        await using (var db = testDb.NewContext())
        {
            repoId = await RepositoryIdAsync(db, "git://git.example.local/helpdesk.git");
            var otherPath = await TestDb.RootPathIdAsync(db, "/helpdesk.rc0");

            await new InstallationService(db).CreateInstallationAsync(
                Deployment(repositoryIds: new List<int> { repoId }));

            await new InstallationService(db).CreateInstallationAsync(
                Deployment(rootPathId: otherPath));
        }

        await using (var db = testDb.NewContext())
        {
            var page = await new InstallationService(db).GetInstallationsAsync(
                new InstallationFilterDto { RepositoryId = repoId });

            Assert.Equal(1, page.TotalCount);
        }
    }

    /// <summary>
    /// Decommissioning is a soft delete, so "what was here last quarter?" cannot be answered
    /// without this switch. Off by default, or the everyday grid fills with retired rows.
    /// </summary>
    [Fact]
    public async Task Decommissioned_rows_appear_only_when_asked_for()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            var created = await new InstallationService(db).CreateInstallationAsync(Deployment());
            await new InstallationService(db).DeleteInstallationAsync(created.Id);
        }

        await using (var db = testDb.NewContext())
        {
            var hidden = await new InstallationService(db).GetInstallationsAsync(
                new InstallationFilterDto());

            Assert.Equal(0, hidden.TotalCount);
        }

        await using (var db = testDb.NewContext())
        {
            var shown = await new InstallationService(db).GetInstallationsAsync(
                new InstallationFilterDto { IncludeDisabled = true });

            Assert.Equal(1, shown.TotalCount);
        }
    }
}
