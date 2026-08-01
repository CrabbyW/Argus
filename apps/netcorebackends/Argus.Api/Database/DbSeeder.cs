using Argus.Api.Database.Entities;
using Argus.Api.Services;
using log4net;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Database;

/// <summary>
/// Applies pending migrations and inserts a small demo seed so the app is not empty.
/// Deliberately tiny — real/bulk data is loaded through the app itself and is out of scope.
/// Every step is idempotent: running twice changes nothing.
///
/// The order mirrors the order the app itself imposes: lookups first, installations from them,
/// repositories linked to the installations, users last.
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
        // After installations: a repository is linked to the installations built from it.
        await SeedRepositoriesAsync(db);
        await SeedUsersAsync(db, demoAdminPassword);

        logger.Info("Database migration and seeding finished.");
    }

    private static async Task SeedLookupsAsync(ArgusDbContext db)
    {
        if (!await db.Machines.AnyAsync())
        {
            db.Machines.AddRange(
                new Machine { Name = "SERVER1", Description = "Web front-end node 1" },
                new Machine { Name = "GAIIS1", Description = "Web front-end node 2" },
                new Machine { Name = "SERVER6354654", Description = "Back-office node" },
                new Machine { Name = "ONTARIO", Description = "Integration/worker node" });
        }

        if (!await db.AppNames.AnyAsync())
        {
            db.AppNames.AddRange(
                new AppName { Name = "ProAssist CallCenter", Description = "Call centre front end" },
                new AppName { Name = "Proassist Extranet", Description = "Partner extranet" },
                new AppName { Name = "Data Exchange WebApi", Description = "Integration web API" });
        }

        if (!await db.AppStageNames.AnyAsync())
        {
            db.AppStageNames.AddRange(
                new AppStageName { Name = "STAGING", SortOrder = 1 },
                new AppStageName { Name = "RC0", SortOrder = 2 },
                new AppStageName { Name = "MAIN", SortOrder = 3 },
                new AppStageName { Name = "PenTest", SortOrder = 4 },
                new AppStageName { Name = "Mirror", SortOrder = 5 });
        }

        if (!await db.ProcessorArchitectures.AnyAsync())
        {
            db.ProcessorArchitectures.AddRange(
                new ProcessorArchitecture { Name = "x64" },
                new ProcessorArchitecture { Name = "x86" },
                new ProcessorArchitecture { Name = "arm64" });
        }

        if (!await db.DnsEndpoints.AnyAsync())
        {
            db.DnsEndpoints.AddRange(
                new DnsEndpoint
                {
                    Name = "https://paha.ga.local",
                    IsLoadBalancer = true,
                    Description = "Load balancer in front of SERVER1 + GAIIS1"
                },
                new DnsEndpoint
                {
                    Name = "https://vipsprava.1220.cz",
                    Description = "Direct public endpoint"
                });
        }

        if (!await db.RootPaths.AnyAsync())
        {
            db.RootPaths.AddRange(
                new RootPath { Name = "/" },
                new RootPath { Name = "/callcenter.rc0" },
                new RootPath { Name = "/worker" });
        }

        if (!await db.PhysicalPaths.AnyAsync())
        {
            db.PhysicalPaths.AddRange(
                new PhysicalPath { Name = @"c:\inetpub\callcenter.rc0" },
                new PhysicalPath { Name = @"c:\inetpub\callcenter" },
                new PhysicalPath { Name = @"c:\inetpub\extranet" },
                new PhysicalPath { Name = @"c:\services\dataexchange" });
        }

        if (!await db.Tags.AnyAsync())
        {
            db.Tags.AddRange(
                new Tag { Name = "web" },
                new Tag { Name = "rc" },
                new Tag { Name = "prod" },
                new Tag { Name = "service" });
        }

        // The set the old RepositoryType enum hardcoded. Now rows, so a new system is an entry in
        // the lookup screen rather than a code change.
        if (!await db.RepositoryTypes.AnyAsync())
        {
            db.RepositoryTypes.AddRange(
                new RepositoryType { Name = "Git" },
                new RepositoryType { Name = "Svn" },
                new RepositoryType { Name = "Bitbucket" },
                new RepositoryType { Name = "Mercurial" },
                new RepositoryType { Name = "Tfs" });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedInstallationsAsync(ArgusDbContext db)
    {
        if (await db.ApplicationInstallations.AnyAsync())
        {
            return;
        }

        var server1 = await db.Machines.SingleAsync(x => x.Name == "SERVER1");
        var gaiis1 = await db.Machines.SingleAsync(x => x.Name == "GAIIS1");
        var server6354654 = await db.Machines.SingleAsync(x => x.Name == "SERVER6354654");
        var ontario = await db.Machines.SingleAsync(x => x.Name == "ONTARIO");

        var callCenter = await db.AppNames.SingleAsync(x => x.Name == "ProAssist CallCenter");
        var extranet = await db.AppNames.SingleAsync(x => x.Name == "Proassist Extranet");
        var dataExchange = await db.AppNames.SingleAsync(x => x.Name == "Data Exchange WebApi");

        var rc0 = await db.AppStageNames.SingleAsync(x => x.Name == "RC0");
        var main = await db.AppStageNames.SingleAsync(x => x.Name == "MAIN");
        var staging = await db.AppStageNames.SingleAsync(x => x.Name == "STAGING");

        var x64 = await db.ProcessorArchitectures.SingleAsync(x => x.Name == "x64");

        var paha = await db.DnsEndpoints.SingleAsync(x => x.Name == "https://paha.ga.local");
        var vip = await db.DnsEndpoints.SingleAsync(x => x.Name == "https://vipsprava.1220.cz");

        var rootSlash = await db.RootPaths.SingleAsync(x => x.Name == "/");
        var rootRc0 = await db.RootPaths.SingleAsync(x => x.Name == "/callcenter.rc0");
        var rootWorker = await db.RootPaths.SingleAsync(x => x.Name == "/worker");

        var diskRc0 = await db.PhysicalPaths.SingleAsync(x => x.Name == @"c:\inetpub\callcenter.rc0");
        var diskCallCenter = await db.PhysicalPaths.SingleAsync(x => x.Name == @"c:\inetpub\callcenter");
        var diskExtranet = await db.PhysicalPaths.SingleAsync(x => x.Name == @"c:\inetpub\extranet");
        var diskWorker = await db.PhysicalPaths.SingleAsync(x => x.Name == @"c:\services\dataexchange");

        var tagWeb = await db.Tags.SingleAsync(x => x.Name == "web");
        var tagRc = await db.Tags.SingleAsync(x => x.Name == "rc");
        var tagProd = await db.Tags.SingleAsync(x => x.Name == "prod");
        var tagService = await db.Tags.SingleAsync(x => x.Name == "service");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        static List<InstallationTag> Tagged(params Tag[] tags) =>
            tags.Select(tag => new InstallationTag { TagId = tag.Id }).ToList();

        db.ApplicationInstallations.AddRange(
            // The load-balancer case: one DNS name, two machines. This is why DnsEndpoints is
            // its own table rather than a column here.
            new ApplicationInstallation
            {
                MachineId = server1.Id, AppNameId = callCenter.Id, AppStageNameId = rc0.Id,
                ProcessorArchitectureId = x64.Id, DnsEndpointId = paha.Id,
                RootPathId = rootRc0.Id, PhysicalPathId = diskRc0.Id,
                InstallationTags = Tagged(tagWeb, tagRc), ValidFromDate = today
            },
            new ApplicationInstallation
            {
                MachineId = gaiis1.Id, AppNameId = callCenter.Id, AppStageNameId = rc0.Id,
                ProcessorArchitectureId = x64.Id, DnsEndpointId = paha.Id,
                RootPathId = rootRc0.Id, PhysicalPathId = diskRc0.Id,
                InstallationTags = Tagged(tagWeb, tagRc), ValidFromDate = today
            },
            new ApplicationInstallation
            {
                MachineId = gaiis1.Id, AppNameId = callCenter.Id, AppStageNameId = main.Id,
                ProcessorArchitectureId = x64.Id, DnsEndpointId = paha.Id,
                RootPathId = rootSlash.Id, PhysicalPathId = diskCallCenter.Id,
                InstallationTags = Tagged(tagWeb, tagProd), ValidFromDate = today
            },
            new ApplicationInstallation
            {
                MachineId = server6354654.Id, AppNameId = extranet.Id, AppStageNameId = main.Id,
                ProcessorArchitectureId = x64.Id, DnsEndpointId = vip.Id,
                RootPathId = rootSlash.Id, PhysicalPathId = diskExtranet.Id,
                InstallationTags = Tagged(tagWeb, tagProd), ValidFromDate = today
            },
            // The no-DNS case: a background API with no public endpoint of its own.
            new ApplicationInstallation
            {
                MachineId = ontario.Id, AppNameId = dataExchange.Id, AppStageNameId = staging.Id,
                ProcessorArchitectureId = x64.Id, DnsEndpointId = null,
                RootPathId = rootWorker.Id, PhysicalPathId = diskWorker.Id,
                InstallationTags = Tagged(tagService), IsActive = false, ValidFromDate = today
            });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Repositories are linked to installations, so this runs after them. Every installation of
    /// an application is built from that application's repositories — one repository row, several
    /// links.
    /// </summary>
    private static async Task SeedRepositoriesAsync(ArgusDbContext db)
    {
        if (await db.AppRepositories.AnyAsync())
        {
            return;
        }

        var callCenter = await db.AppNames.SingleAsync(x => x.Name == "ProAssist CallCenter");
        var extranet = await db.AppNames.SingleAsync(x => x.Name == "Proassist Extranet");

        var callCenterInstallations = await db.ApplicationInstallations
            .Where(x => x.AppNameId == callCenter.Id)
            .Select(x => x.Id)
            .ToListAsync();

        var extranetInstallations = await db.ApplicationInstallations
            .Where(x => x.AppNameId == extranet.Id)
            .Select(x => x.Id)
            .ToListAsync();

        static List<InstallationRepository> LinkedTo(IEnumerable<int> installationIds) =>
            installationIds.Select(id => new InstallationRepository { InstallationId = id }).ToList();

        // Looked up by name, not by a hardcoded Id: the ids depend on insertion order, and this
        // seed is the one place where writing them out would look harmless and drift silently.
        var git = await db.RepositoryTypes.SingleAsync(x => x.Name == "Git");
        var svn = await db.RepositoryTypes.SingleAsync(x => x.Name == "Svn");
        var bitbucket = await db.RepositoryTypes.SingleAsync(x => x.Name == "Bitbucket");

        db.AppRepositories.AddRange(
            new AppRepository
            {
                Name = "git://git.local/callcenter.git",
                RepositoryTypeId = git.Id,
                InstallationRepositories = LinkedTo(callCenterInstallations)
            },
            new AppRepository
            {
                Name = "svn://svn.local/callcenter/trunk",
                RepositoryTypeId = svn.Id,
                InstallationRepositories = LinkedTo(callCenterInstallations)
            },
            new AppRepository
            {
                Name = "bitbucket://team/extranet",
                RepositoryTypeId = bitbucket.Id,
                InstallationRepositories = LinkedTo(extranetInstallations)
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
