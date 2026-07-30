using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Argus.Api.Database;

/// <summary>
/// Used by <c>dotnet ef migrations add</c> so migrations can be generated without a running
/// database or a fully booted host. The connection string here is a design-time placeholder;
/// the runtime one comes from configuration.
/// </summary>
public class ArgusDbContextFactory : IDesignTimeDbContextFactory<ArgusDbContext>
{
    public ArgusDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ARGUS_CONNECTIONSTRING")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=Argus;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<ArgusDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ArgusDbContext(options);
    }
}
