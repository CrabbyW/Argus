using Argus.Api.Services;
using Argus.Api.WebApiPoco.Common;
using Argus.Api.WebApiPoco.Installations;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Tests;

/// <summary>
/// Repositories moved off the application and onto the installation, many-to-many. The point of
/// that change was that one url is stored once however many deployments share it, and that
/// attaching it to one installation does not silently attach it to its siblings. Both halves are
/// asserted here — before this file existed, nothing covered the write path at all.
/// </summary>
public class AppRepositoryServiceTests
{
    private static InstallationUpsertDto Deployment(
        int rootPathId,
        int stageId = TestDb.StageMain,
        int appNameId = TestDb.CallCenter) => new()
        {
            MachineId = TestDb.Gaiis1,
            AppNameId = appNameId,
            AppStageNameId = stageId,
            ProcessorArchitectureId = TestDb.X64,
            DnsEndpointId = TestDb.PahaEndpoint,
            RootPathId = rootPathId,
            PhysicalPathId = TestDb.DiskDefault,
            IsActive = true,
            ValidFromDate = new DateOnly(2026, 1, 1)
        };

    private static AppRepositoryUpsertDto Repo(string url, params int[] installationIds) => new()
    {
        RepositoryUrl = url,
        RepositoryTypeId = TestDb.RepoTypeGit,
        InstallationIds = installationIds.ToList()
    };

    /// <summary>
    /// The whole justification for the many-to-many change. Under the old model repositories hung
    /// off the application, so adding one to a deployment added it to every deployment of that
    /// application — including the production one nobody meant to touch.
    /// </summary>
    [Fact]
    public async Task A_repository_added_to_one_installation_does_not_appear_on_its_sibling()
    {
        using var testDb = TestDb.CreateSeeded();

        int mainId;
        int rc0Id;

        await using (var db = testDb.NewContext())
        {
            var rc0Path = await TestDb.RootPathIdAsync(db, "/callcenter.rc0");

            // Same application, same machine, two stages: siblings under the old model.
            mainId = (await new InstallationService(db).CreateInstallationAsync(
                Deployment(TestDb.RootSlash, TestDb.StageMain))).Id;

            rc0Id = (await new InstallationService(db).CreateInstallationAsync(
                Deployment(rc0Path, TestDb.StageRc0))).Id;
        }

        await using (var db = testDb.NewContext())
        {
            await new AppRepositoryService(db).CreateAsync(
                Repo("git://git.local/callcenter.git", rc0Id));
        }

        await using (var db = testDb.NewContext())
        {
            var onRc0 = await new AppRepositoryService(db).GetAllAsync(rc0Id, null);
            var onMain = await new AppRepositoryService(db).GetAllAsync(mainId, null);

            Assert.Single(onRc0);
            Assert.Empty(onMain);
        }
    }

    /// <summary>
    /// One url, one row — even when two installations are built from it. A plain foreign key
    /// would have produced a second row with the same text, which is the duplication the
    /// normalized model exists to prevent.
    /// </summary>
    [Fact]
    public async Task One_url_shared_by_two_installations_is_a_single_row()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            var rc0Path = await TestDb.RootPathIdAsync(db, "/callcenter.rc0");

            var first = (await new InstallationService(db).CreateInstallationAsync(
                Deployment(TestDb.RootSlash, TestDb.StageMain))).Id;

            var second = (await new InstallationService(db).CreateInstallationAsync(
                Deployment(rc0Path, TestDb.StageRc0))).Id;

            await new AppRepositoryService(db).CreateAsync(
                Repo("git://git.local/callcenter.git", first, second));
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.Equal(1, await assert.AppRepositories.CountAsync());
            Assert.Equal(2, await assert.InstallationRepositories.CountAsync());
        }
    }

    [Fact]
    public async Task The_same_url_cannot_be_registered_twice()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();
        var service = new AppRepositoryService(db);

        await service.CreateAsync(Repo("git://git.local/callcenter.git"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(Repo("git://git.local/callcenter.git")));
    }

    /// <summary>
    /// Registering a repository before its installation exists is legitimate — the roadplan
    /// fills lookups first — so an empty link list must not be an error.
    /// </summary>
    [Fact]
    public async Task A_repository_may_be_registered_before_any_installation_uses_it()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            var created = await new AppRepositoryService(db).CreateAsync(
                Repo("svn://svn.local/callcenter/trunk"));

            Assert.Empty(created.InstallationIds);
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.Equal(1, await assert.AppRepositories.CountAsync());
            Assert.Equal(0, await assert.InstallationRepositories.CountAsync());
        }
    }

    // --- Repository type, since it became a lookup ---------------------------------------

    /// <summary>
    /// The type is a foreign key now, so the same rule applies to it as to every other lookup Id:
    /// it has to point at a row that exists. As a bare enum column any number was accepted.
    /// </summary>
    [Fact]
    public async Task A_repository_type_that_does_not_exist_is_rejected()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();

        var dto = Repo("git://git.local/callcenter.git");
        dto.RepositoryTypeId = 9999;

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => new AppRepositoryService(db).CreateAsync(dto));

        Assert.Contains("9999", error.Message);
    }

    /// <summary>
    /// Null replaced the enum's <c>Unknown</c> member: a repository whose source-control system
    /// was never recorded is a normal row, not an error and not a magic zero.
    /// </summary>
    [Fact]
    public async Task A_repository_may_have_no_type_at_all()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            var dto = Repo("git://git.local/callcenter.git");
            dto.RepositoryTypeId = null;

            var created = await new AppRepositoryService(db).CreateAsync(dto);

            Assert.Null(created.RepositoryTypeId);
            Assert.Null(created.RepositoryTypeName);
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.Equal(1, await assert.AppRepositories.CountAsync());
        }
    }

    /// <summary>
    /// The read side carries the type's name as well as its Id, so a grid renders a row without a
    /// second request — and renaming the type shows up everywhere at once, which is the whole
    /// reason it stopped being a hardcoded string.
    /// </summary>
    [Fact]
    public async Task Renaming_a_repository_type_changes_what_every_repository_reports()
    {
        using var testDb = TestDb.CreateSeeded();

        int repositoryId;

        await using (var db = testDb.NewContext())
        {
            var created = await new AppRepositoryService(db).CreateAsync(
                Repo("git://git.local/callcenter.git"));

            Assert.Equal("Git", created.RepositoryTypeName);
            repositoryId = created.Id;
        }

        await using (var db = testDb.NewContext())
        {
            await new LookupService(db).UpdateAsync(
                LookupKind.RepositoryTypes,
                TestDb.RepoTypeGit,
                new LookupUpsertDto { Name = "Git (self-hosted)" });
        }

        await using (var db = testDb.NewContext())
        {
            var read = await new AppRepositoryService(db).GetByIdAsync(repositoryId);

            Assert.Equal("Git (self-hosted)", read!.RepositoryTypeName);
        }
    }

    /// <summary>
    /// The delete guard for this kind has to ask the repositories, not the installations.
    ///
    /// Asking through <c>ApplicationInstallation</c> is what the other nine kinds do, and it would
    /// compile here — but this repository is attached to no installation, so that query would
    /// answer "not in use", the type would be soft-deleted, and the foreign key would point at a
    /// hidden row. Registering a repository before its installation exists is normal (see above),
    /// so this is not a corner case.
    /// </summary>
    [Fact]
    public async Task A_repository_type_used_by_an_unattached_repository_cannot_be_deleted()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            await new AppRepositoryService(db).CreateAsync(Repo("git://git.local/callcenter.git"));
        }

        await using (var db = testDb.NewContext())
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(
                () => new LookupService(db).DeleteAsync(LookupKind.RepositoryTypes, TestDb.RepoTypeGit));

            Assert.Contains("repositories", error.Message);
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.True(await assert.RepositoryTypes.AnyAsync(x => x.Id == TestDb.RepoTypeGit));
        }
    }

    /// <summary>The other half: a type nothing points at is removable.</summary>
    [Fact]
    public async Task An_unused_repository_type_can_be_deleted()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            Assert.True(await new LookupService(db).DeleteAsync(
                LookupKind.RepositoryTypes, TestDb.RepoTypeSvn));
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.False(await assert.RepositoryTypes.AnyAsync(x => x.Id == TestDb.RepoTypeSvn));
        }
    }

    [Fact]
    public async Task An_installation_that_does_not_exist_is_rejected()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new AppRepositoryService(db).CreateAsync(Repo("git://git.local/ghost.git", 9999)));
    }

    /// <summary>
    /// The application filter is the cross-installation view: every repository used anywhere
    /// that application runs, reached through the links rather than through an owning column.
    /// </summary>
    [Fact]
    public async Task Filtering_by_application_follows_the_links()
    {
        using var testDb = TestDb.CreateSeeded();

        await using (var db = testDb.NewContext())
        {
            var extranetPath = await TestDb.RootPathIdAsync(db, "/extranet");

            var callCenter = (await new InstallationService(db).CreateInstallationAsync(
                Deployment(TestDb.RootSlash, TestDb.StageMain))).Id;

            var extranet = (await new InstallationService(db).CreateInstallationAsync(
                Deployment(extranetPath, TestDb.StageMain, TestDb.Extranet))).Id;

            var service = new AppRepositoryService(db);
            await service.CreateAsync(Repo("git://git.local/callcenter.git", callCenter));
            await service.CreateAsync(Repo("bitbucket://team/extranet", extranet));
        }

        await using (var db = testDb.NewContext())
        {
            var service = new AppRepositoryService(db);

            Assert.Single(await service.GetAllAsync(null, TestDb.CallCenter));
            Assert.Single(await service.GetAllAsync(null, TestDb.Extranet));
            Assert.Equal(2, (await service.GetAllAsync(null, null)).Count);
        }
    }

    /// <summary>
    /// Editing the link list is a diff, like tags. Removing the last link leaves the repository
    /// registered rather than deleting it — the url is still a fact about the world.
    /// </summary>
    [Fact]
    public async Task Editing_the_links_leaves_the_repository_in_place()
    {
        using var testDb = TestDb.CreateSeeded();

        int repositoryId;
        int installationId;

        await using (var db = testDb.NewContext())
        {
            installationId = (await new InstallationService(db).CreateInstallationAsync(
                Deployment(TestDb.RootSlash))).Id;

            repositoryId = (await new AppRepositoryService(db).CreateAsync(
                Repo("git://git.local/callcenter.git", installationId))).Id;
        }

        await using (var db = testDb.NewContext())
        {
            await new AppRepositoryService(db).UpdateAsync(
                repositoryId, Repo("git://git.local/callcenter.git"));
        }

        await using (var assert = testDb.NewContext())
        {
            Assert.Equal(1, await assert.AppRepositories.CountAsync());
            Assert.Equal(0, await assert.InstallationRepositories.CountAsync());
        }
    }
}
