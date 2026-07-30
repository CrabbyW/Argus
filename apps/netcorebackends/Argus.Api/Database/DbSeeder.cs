using Argus.Api.Database.Entities;
using Argus.Api.Database.Entities.Enums;
using Argus.Api.Services;
using log4net;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Database;

/// <summary>
/// Applies pending migrations and inserts a small demo seed so the app is not empty.
/// Deliberately tiny — real/bulk data is loaded through the app itself and is out of scope.
/// Every step is idempotent: running twice changes nothing.
/// </summary>
public static class DbSeeder
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(DbSeeder));

    public static async Task MigrateAndSeedAsync(IServiceProvider services, string demoAdminPassword)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();

        logger.Info("Applying database migrations...");
        await db.Database.MigrateAsync();

        await SeedLookupsAsync(db);
        await SeedInstallationsAsync(db);
        await SeedUsersAsync(db, demoAdminPassword);

        logger.Info("Database migration and seeding finished.");
    }

    private static async Task SeedLookupsAsync(ArgusDbContext db)
    {
        if (!await db.Machines.AnyAsync())
        {
            db.Machines.AddRange(
                new Machine { MachineName = "GAIIS1", Description = "Web front-end node 1" },
                new Machine { MachineName = "GAIIS2", Description = "Web front-end node 2" },
                new Machine { MachineName = "GAIIS3", Description = "Web front-end node 3" });
        }

        if (!await db.Applications.AnyAsync())
        {
            db.Applications.AddRange(
                new Application
                {
                    AppName = "ProAssistNet",
                    Description = "Assistance portal",
                    AppRepositories = new List<AppRepository>
                    {
                        new() { RepositoryUrl = "git://git.local/proassistnet.git", RepositoryType = RepositoryType.Git },
                        new() { RepositoryUrl = "svn://svn.local/proassistnet/trunk", RepositoryType = RepositoryType.Svn }
                    }
                },
                new Application
                {
                    AppName = "VipSprava",
                    Description = "Administration back-office",
                    AppRepositories = new List<AppRepository>
                    {
                        new() { RepositoryUrl = "bitbucket://team/vipsprava", RepositoryType = RepositoryType.Bitbucket }
                    }
                });
        }

        if (!await db.AppStages.AnyAsync())
        {
            db.AppStages.AddRange(
                new AppStage { StageName = "Main", SortOrder = 1 },
                new AppStage { StageName = "RC0", SortOrder = 2 },
                new AppStage { StageName = "Staging", SortOrder = 3 },
                new AppStage { StageName = "PenTest", SortOrder = 4 },
                new AppStage { StageName = "Mirror", SortOrder = 5 });
        }

        if (!await db.ProcessorArchitectures.AnyAsync())
        {
            db.ProcessorArchitectures.AddRange(
                new ProcessorArchitecture { ArchitectureName = "x64" },
                new ProcessorArchitecture { ArchitectureName = "x86" },
                new ProcessorArchitecture { ArchitectureName = "arm64" });
        }

        if (!await db.DnsEndpoints.AnyAsync())
        {
            db.DnsEndpoints.AddRange(
                new DnsEndpoint
                {
                    DnsName = "https://paha.ga.local",
                    IsLoadBalancer = true,
                    Description = "Load balancer in front of GAIIS1 + GAIIS2"
                },
                new DnsEndpoint
                {
                    DnsName = "https://vipsprava.1220.cz",
                    Description = "Direct public endpoint"
                });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedInstallationsAsync(ArgusDbContext db)
    {
        if (await db.Installations.AnyAsync())
        {
            return;
        }

        var gaiis1 = await db.Machines.SingleAsync(x => x.MachineName == "GAIIS1");
        var gaiis2 = await db.Machines.SingleAsync(x => x.MachineName == "GAIIS2");
        var gaiis3 = await db.Machines.SingleAsync(x => x.MachineName == "GAIIS3");

        var proAssist = await db.Applications.SingleAsync(x => x.AppName == "ProAssistNet");
        var vipSprava = await db.Applications.SingleAsync(x => x.AppName == "VipSprava");

        var rc0 = await db.AppStages.SingleAsync(x => x.StageName == "RC0");
        var main = await db.AppStages.SingleAsync(x => x.StageName == "Main");

        var x64 = await db.ProcessorArchitectures.SingleAsync(x => x.ArchitectureName == "x64");

        var paha = await db.DnsEndpoints.SingleAsync(x => x.DnsName == "https://paha.ga.local");
        var vip = await db.DnsEndpoints.SingleAsync(x => x.DnsName == "https://vipsprava.1220.cz");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        db.Installations.AddRange(
            // The load-balancer case: one DNS name, two machines.
            new Installation
            {
                MachineId = gaiis1.Id, ApplicationId = proAssist.Id, AppStageId = rc0.Id,
                ProcessorArchitectureId = x64.Id, DnsEndpointId = paha.Id,
                RootPath = "/proassistnet.rc0", PhysicalPath = @"c:\inetpub\proassistnet.rc0",
                Tags = "web;rc", ValidFromDate = today
            },
            new Installation
            {
                MachineId = gaiis2.Id, ApplicationId = proAssist.Id, AppStageId = rc0.Id,
                ProcessorArchitectureId = x64.Id, DnsEndpointId = paha.Id,
                RootPath = "/proassistnet.rc0", PhysicalPath = @"c:\inetpub\proassistnet.rc0",
                Tags = "web;rc", ValidFromDate = today
            },
            new Installation
            {
                MachineId = gaiis1.Id, ApplicationId = proAssist.Id, AppStageId = main.Id,
                ProcessorArchitectureId = x64.Id, DnsEndpointId = paha.Id,
                RootPath = "/", PhysicalPath = @"c:\inetpub\proassistnet",
                Tags = "web;prod", ValidFromDate = today
            },
            new Installation
            {
                MachineId = gaiis3.Id, ApplicationId = vipSprava.Id, AppStageId = main.Id,
                ProcessorArchitectureId = x64.Id, DnsEndpointId = vip.Id,
                RootPath = "/", PhysicalPath = @"c:\inetpub\vipsprava",
                Tags = "web;prod", ValidFromDate = today
            },
            // The no-DNS case: a background worker with no public endpoint.
            new Installation
            {
                MachineId = gaiis3.Id, ApplicationId = vipSprava.Id, AppStageId = rc0.Id,
                ProcessorArchitectureId = x64.Id, DnsEndpointId = null,
                RootPath = "/worker", PhysicalPath = @"c:\services\vipsprava.worker",
                Tags = "service", IsActive = false, ValidFromDate = today
            });

        await db.SaveChangesAsync();
    }

    private static async Task SeedUsersAsync(ArgusDbContext db, string demoAdminPassword)
    {
        if (await db.ApplicationUsers.AnyAsync())
        {
            return;
        }

        var (hash, salt) = PasswordHasher.HashPassword(demoAdminPassword);

        db.ApplicationUsers.Add(new ApplicationUser
        {
            Username = "msfadmin",
            DisplayName = "Demo Administrator",
            PasswordHash = hash,
            PasswordSalt = salt
        });

        await db.SaveChangesAsync();
        logger.Info("Seeded demo user 'msfadmin'. Change this password before any real deployment.");
    }
}
