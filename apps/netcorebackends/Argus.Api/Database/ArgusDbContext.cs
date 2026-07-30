using Argus.Api.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Database;

public class ArgusDbContext : DbContext
{
    public ArgusDbContext(DbContextOptions<ArgusDbContext> options) : base(options)
    {
    }

    // --- Lookups ---
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<AppStage> AppStages => Set<AppStage>();
    public DbSet<ProcessorArchitecture> ProcessorArchitectures => Set<ProcessorArchitecture>();
    public DbSet<DnsEndpoint> DnsEndpoints => Set<DnsEndpoint>();

    // --- Core ---
    public DbSet<Installation> Installations => Set<Installation>();
    public DbSet<AppRepository> AppRepositories => Set<AppRepository>();

    // --- Auth ---
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Explicit IEntityTypeConfiguration<T> per entity — no convention magic.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ArgusDbContext).Assembly);
    }
}
