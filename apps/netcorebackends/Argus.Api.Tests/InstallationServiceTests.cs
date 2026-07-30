using Argus.Api.Services;
using Argus.Api.WebApiPoco.Installations;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Tests;

public class InstallationServiceTests
{
    private static InstallationUpsertDto Deployment(
        string rootPath = "/",
        int machineId = TestDb.Gaiis1,
        int applicationId = TestDb.ProAssistNet,
        int stageId = TestDb.StageMain,
        string validFrom = "2026-01-01",
        string? validTo = null) => new()
        {
            MachineId = machineId,
            ApplicationId = applicationId,
            AppStageId = stageId,
            ProcessorArchitectureId = TestDb.X64,
            DnsEndpointId = TestDb.PahaEndpoint,
            RootPath = rootPath,
            PhysicalPath = @"c:\inetpub\wwwroot\proassistnet",
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
            var all = await assert.Installations.IgnoreQueryFilters().ToListAsync();

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

        db.Installations.Add(new Argus.Api.Database.Entities.Installation
        {
            MachineId = TestDb.Gaiis1,
            ApplicationId = TestDb.ProAssistNet,
            AppStageId = TestDb.StageMain,
            ProcessorArchitectureId = TestDb.X64,
            RootPath = "/",
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

        await service.CreateInstallationAsync(Deployment(rootPath: "/"));
        await service.CreateInstallationAsync(Deployment(rootPath: "/proassistnet.mirror"));

        Assert.Equal(2, await db.Installations.CountAsync());
    }

    [Fact]
    public async Task Saving_an_installation_unchanged_does_not_clash_with_itself()
    {
        using var testDb = TestDb.CreateSeeded();

        await using var db = testDb.NewContext();
        var service = new InstallationService(db);

        var created = await service.CreateInstallationAsync(Deployment());

        var dto = Deployment();
        dto.PhysicalPath = @"d:\sites\proassistnet";

        var updated = await service.UpdateInstallationAsync(created.Id, dto);

        Assert.NotNull(updated);
        Assert.Equal(@"d:\sites\proassistnet", updated!.PhysicalPath);
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

            // Started long before the window, retired inside it.
            await service.CreateInstallationAsync(
                Deployment(rootPath: "/spans-into", validFrom: "2025-01-01", validTo: "2026-03-15"));

            // Entirely before the window.
            await service.CreateInstallationAsync(
                Deployment(rootPath: "/over-before", validFrom: "2024-01-01", validTo: "2024-12-31"));

            // Still installed, started before the window — open-ended.
            await service.CreateInstallationAsync(
                Deployment(rootPath: "/still-there", validFrom: "2025-06-01"));
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
                new InstallationFilterDto { SortBy = "'; drop table Installations; --" });

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

            await service.CreateInstallationAsync(Deployment(rootPath: "/one"));

            var elsewhere = Deployment(rootPath: "/two");
            elsewhere.PhysicalPath = @"e:\services\vipsprava.worker";
            await service.CreateInstallationAsync(elsewhere);
        }

        await using (var db = testDb.NewContext())
        {
            var result = await new InstallationService(db).GetInstallationsAsync(
                new InstallationFilterDto { SearchTerm = "vipsprava.worker" });

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
                await service.CreateInstallationAsync(Deployment(rootPath: $"/app{i}"));
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
}
