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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Explicit IEntityTypeConfiguration<T> per entity — no convention magic.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ArgusDbContext).Assembly);
    }
}
