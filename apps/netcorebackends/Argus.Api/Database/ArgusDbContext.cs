using Argus.Api.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Database;

public class ArgusDbContext : DbContext
{
    public ArgusDbContext(DbContextOptions<ArgusDbContext> options) : base(options)
    {
    }

    // --- Lookups: filled first, one screen each. Every one implements ILookupEntity. ---
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<AppName> AppNames => Set<AppName>();
    public DbSet<AppStageName> AppStageNames => Set<AppStageName>();
    public DbSet<ProcessorArchitecture> ProcessorArchitectures => Set<ProcessorArchitecture>();
    public DbSet<DnsEndpoint> DnsEndpoints => Set<DnsEndpoint>();
    public DbSet<RootPath> RootPaths => Set<RootPath>();
    public DbSet<PhysicalPath> PhysicalPaths => Set<PhysicalPath>();
    public DbSet<AppRepository> AppRepositories => Set<AppRepository>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<RepositoryType> RepositoryTypes => Set<RepositoryType>();

    // --- The result table: nothing but references into the lookups above, plus its own dates ---
    public DbSet<ApplicationInstallation> ApplicationInstallations => Set<ApplicationInstallation>();

    // --- Link tables (no soft delete of their own) ---
    public DbSet<InstallationTag> InstallationTags => Set<InstallationTag>();
    public DbSet<InstallationRepository> InstallationRepositories => Set<InstallationRepository>();

    // --- Auth ---
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();

    // --- Audit: who changed what on which installation, written by EntityJournalInterceptor ---
    public DbSet<EntityJournalEntry> EntityJournal => Set<EntityJournalEntry>();

    /// <summary>
    /// Turns the journal off for writes that are not somebody's edit.
    ///
    /// Set by <see cref="DbSeeder"/>: seeding writes ~200 demo installations straight through this
    /// context, and journaling them would bury the first real change under rows nobody made. It
    /// doubles as the interceptor's re-entrancy guard when it saves its own rows.
    /// </summary>
    public bool JournalingSuppressed { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Explicit IEntityTypeConfiguration<T> per entity — no convention magic.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ArgusDbContext).Assembly);
    }
}
