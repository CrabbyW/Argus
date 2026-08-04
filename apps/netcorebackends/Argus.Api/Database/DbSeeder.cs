using Argus.Api.Database.Entities;
using Argus.Api.Services;
using log4net;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Database;

/// <summary>
/// Applies pending migrations and inserts the demo seed so the app is not empty.
/// Every step is idempotent: running twice changes nothing.
///
/// The order mirrors the order the app itself imposes: lookups first, installations from them,
/// repositories linked to the installations, users last.
/// </summary>
public static class DbSeeder
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(DbSeeder));

    public static async Task MigrateAndSeedAsync(
        IServiceProvider services,
        string demoAdminPassword,
        int demoInstallationCount)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArgusDbContext>();

        // Seeding is not somebody's edit. Without this the demo volume alone would write a couple
        // of hundred journal rows made by nobody, and the first real change would be buried under
        // them. A seeded installation shows an empty history until someone actually changes it,
        // which is the truth.
        db.JournalingSuppressed = true;

        logger.Info("Applying database migrations...");
        await db.Database.MigrateAsync();

        await SeedLookupsAsync(db);
        await SeedInstallationsAsync(db);
        // The five rows above are the hand-written cases worth reading; this tops the table up
        // to a realistic size so paging, sorting and filtering have something to work on.
        await SeedDemoVolumeAsync(db, demoInstallationCount);
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
    /// Tops the installations table up to <paramref name="targetCount"/> rows so the grid has a
    /// realistic amount to page, sort and filter through. Only ever adds: if the table already
    /// holds that many rows — including once real data is in there — this does nothing.
    ///
    /// Rows are generated, not random. The unique key is
    /// (MachineId, AppNameId, AppStageNameId, RootPathId), so walking those four lookups in
    /// nested order produces a distinct combination every time, and re-running the seeder on a
    /// half-filled table continues where it left off instead of colliding.
    /// </summary>
    private static async Task SeedDemoVolumeAsync(ArgusDbContext db, int targetCount)
    {
        if (targetCount <= 0)
        {
            return;
        }

        // IgnoreQueryFilters throughout this method: installations carry a soft-delete filter, so
        // the plain query hides disabled rows. Counting through it would make the seeder top the
        // table up again on every start as rows get deleted, and — because the unique index binds
        // disabled rows too — a "free" combination could collide with a soft-deleted one.
        if (await db.ApplicationInstallations.IgnoreQueryFilters().CountAsync() >= targetCount)
        {
            return;
        }

        // The hand-written seed above carries too few lookup values to build this many distinct
        // combinations (4 machines x 3 apps x 5 stages x 3 root paths = 180). These fill the pool
        // out; each is added only if a row of that name is missing, so an edited name stays edited.
        await EnsureMachinesAsync(db);
        await EnsureAppNamesAsync(db);
        await EnsureRootPathsAsync(db);
        await EnsurePhysicalPathsAsync(db);
        await db.SaveChangesAsync();

        var machines = await db.Machines.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        var apps = await db.AppNames.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        var stages = await db.AppStageNames.OrderBy(x => x.SortOrder).Select(x => x.Id).ToListAsync();
        var roots = await db.RootPaths.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        var architectures = await db.ProcessorArchitectures.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        var endpoints = await db.DnsEndpoints.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        var physicalPaths = await db.PhysicalPaths.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        var tags = await db.Tags.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();

        // These four are required foreign keys, and the row generator divides by their counts.
        // If every value of one of them has been deleted in the UI there is nothing to build a
        // valid installation from, and saying so beats an index-out-of-range on startup.
        if (machines.Count == 0 || apps.Count == 0 || stages.Count == 0
            || roots.Count == 0 || architectures.Count == 0)
        {
            logger.Warn(
                "Demo installations were not seeded: machines, applications, stages, root paths " +
                "or architectures are empty.");
            return;
        }

        var used = (await db.ApplicationInstallations
                .IgnoreQueryFilters()
                .Select(x => new { x.MachineId, x.AppNameId, x.AppStageNameId, x.RootPathId })
                .ToListAsync())
            .Select(x => (x.MachineId, x.AppNameId, x.AppStageNameId, x.RootPathId))
            .ToHashSet();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var toAdd = targetCount - used.Count;
        var added = 0;
        var index = 0;

        // The combination space is walked with a coprime stride rather than as nested loops.
        // Nested loops are machine-major, so a target smaller than the space fills up on the
        // first machine or two and leaves the rest unused — 150 of 200 rows landed on one
        // machine that way. A stride coprime with the total visits every combination exactly
        // once while moving through all four lookups at once, so any cut-off point is still
        // spread across machines, applications, stages and root paths.
        var total = machines.Count * apps.Count * stages.Count * roots.Count;
        var stride = CoprimeStride(total);

        for (var step = 0; step < total && added < toAdd; step++)
        {
            var combo = (int)((long)step * stride % total);

            var rootId = roots[combo % roots.Count];
            combo /= roots.Count;
            var stageId = stages[combo % stages.Count];
            combo /= stages.Count;
            var appId = apps[combo % apps.Count];
            combo /= apps.Count;
            var machineId = machines[combo % machines.Count];

            if (!used.Add((machineId, appId, stageId, rootId)))
            {
                continue;
            }

            var i = index++;
            var validFrom = today.AddDays(-(i % 365));

            db.ApplicationInstallations.Add(new ApplicationInstallation
            {
                MachineId = machineId,
                AppNameId = appId,
                AppStageNameId = stageId,
                RootPathId = rootId,
                ProcessorArchitectureId = architectures[i % architectures.Count],
                // Both are optional columns, so an empty lookup is simply left null rather than
                // stopping the seed. Every fifth row has no DNS name anyway: a worker or console
                // app, the case the column has to survive being empty for.
                DnsEndpointId = i % 5 == 4 || endpoints.Count == 0 ? null : endpoints[i % endpoints.Count],
                PhysicalPathId = i % 11 == 10 || physicalPaths.Count == 0
                    ? null
                    : physicalPaths[i % physicalPaths.Count],
                InstallationTags = TagPair(tags, i),
                IsActive = i % 7 != 0,
                // A few soft-deleted rows, so "include disabled" has something to show.
                IsEnabled = i % 25 != 24,
                ValidFromDate = validFrom,
                ValidToDate = i % 9 == 8 ? validFrom.AddDays(180) : null
            });

            added++;
        }

        await db.SaveChangesAsync();
        logger.Info($"Seeded {added} demo installation(s); the table now holds {targetCount}.");
    }

    /// <summary>
    /// A step size that shares no factor with <paramref name="total"/>, so repeatedly adding it
    /// modulo the total lands on every value exactly once before repeating. Starts near the
    /// golden-ratio fraction of the range, which keeps successive picks far apart, and walks up
    /// until the value is coprime — 1 as the last resort, which degrades to a plain sequence.
    /// </summary>
    private static int CoprimeStride(int total)
    {
        if (total <= 2)
        {
            return 1;
        }

        for (var candidate = (int)(total * 0.618) | 1; candidate < total; candidate += 2)
        {
            if (Gcd(candidate, total) == 1)
            {
                return candidate;
            }
        }

        return 1;
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }

    /// <summary>Two different tags per row, walked so every tag gets used.</summary>
    private static List<InstallationTag> TagPair(IReadOnlyList<int> tagIds, int index)
    {
        if (tagIds.Count == 0)
        {
            return new List<InstallationTag>();
        }

        var first = tagIds[index % tagIds.Count];
        var second = tagIds[(index + 1 + index / tagIds.Count) % tagIds.Count];

        var pair = new List<InstallationTag> { new() { TagId = first } };

        if (second != first)
        {
            pair.Add(new InstallationTag { TagId = second });
        }

        return pair;
    }

    private static async Task EnsureMachinesAsync(ArgusDbContext db)
    {
        // Ignoring the soft-delete filter: the unique index on Name covers disabled rows too, so
        // a name that was deleted in the UI must not be "missing" here and re-added.
        var existing = await db.Machines.IgnoreQueryFilters().Select(x => x.Name).ToListAsync();

        foreach (var (name, description) in new[]
        {
            ("QUEBEC", "Web front-end node 3"),
            ("ALBERTA", "Web front-end node 4"),
            ("MANITOBA", "Back-office node 2"),
            ("YUKON", "Integration/worker node 2"),
            ("WEB01", "Public web node 1"),
            ("WEB02", "Public web node 2")
        })
        {
            if (!existing.Contains(name))
            {
                db.Machines.Add(new Machine { Name = name, Description = description });
            }
        }
    }

    private static async Task EnsureAppNamesAsync(ArgusDbContext db)
    {
        // Ignoring the soft-delete filter: the unique index on Name covers disabled rows too, so
        // a name that was deleted in the UI must not be "missing" here and re-added.
        var existing = await db.AppNames.IgnoreQueryFilters().Select(x => x.Name).ToListAsync();

        foreach (var (name, description) in new[]
        {
            ("Billing Gateway", "Payment and invoicing gateway"),
            ("Reporting Portal", "Management reporting front end"),
            ("Identity Provider", "Sign-in and token issuing")
        })
        {
            if (!existing.Contains(name))
            {
                db.AppNames.Add(new AppName { Name = name, Description = description });
            }
        }
    }

    private static async Task EnsureRootPathsAsync(ArgusDbContext db)
    {
        // Ignoring the soft-delete filter: the unique index on Name covers disabled rows too, so
        // a name that was deleted in the UI must not be "missing" here and re-added.
        var existing = await db.RootPaths.IgnoreQueryFilters().Select(x => x.Name).ToListAsync();

        foreach (var name in new[] { "/api", "/admin" })
        {
            if (!existing.Contains(name))
            {
                db.RootPaths.Add(new RootPath { Name = name });
            }
        }
    }

    private static async Task EnsurePhysicalPathsAsync(ArgusDbContext db)
    {
        // Ignoring the soft-delete filter: the unique index on Name covers disabled rows too, so
        // a name that was deleted in the UI must not be "missing" here and re-added.
        var existing = await db.PhysicalPaths.IgnoreQueryFilters().Select(x => x.Name).ToListAsync();

        foreach (var name in new[] { @"c:\inetpub\billing", @"c:\inetpub\reporting", @"c:\services\identity" })
        {
            if (!existing.Contains(name))
            {
                db.PhysicalPaths.Add(new PhysicalPath { Name = name });
            }
        }
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
