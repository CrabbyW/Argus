using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Tests;

/// <summary>
/// A throwaway database for one test, built from the real model with the real constraints.
///
/// SQLite in memory rather than the InMemory provider on purpose: the behaviour under test is
/// relational. The unique indexes and their <c>IsEnabled = 1</c> filters only exist in a provider
/// that has indexes, and <c>EF.Functions.Like</c> only translates in one that has SQL.
/// The connection has to stay open — closing the last one drops the database.
/// </summary>
internal sealed class TestDb : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<ArgusDbContext> options;

    private TestDb()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        options = new DbContextOptionsBuilder<ArgusDbContext>()
            .UseSqlite(connection)
            .Options;

        using var setup = NewContext();
        setup.Database.EnsureCreated();
    }

    /// <summary>Ids are fixed so tests can refer to them without extra queries.</summary>
    public const int Gaiis1 = 1;
    public const int Gaiis2 = 2;
    public const int CallCenter = 1;
    public const int Extranet = 2;
    public const int StageMain = 1;
    public const int StageRc0 = 2;
    public const int X64 = 1;
    public const int PahaEndpoint = 1;
    public const int RootSlash = 1;
    public const int DiskDefault = 1;
    public const int TagWeb = 1;

    public static TestDb CreateSeeded()
    {
        var testDb = new TestDb();

        using var db = testDb.NewContext();

        db.Machines.AddRange(
            new Machine { Id = Gaiis1, Name = "GAIIS1" },
            new Machine { Id = Gaiis2, Name = "GAIIS2" });

        db.AppNames.AddRange(
            new AppName { Id = CallCenter, Name = "ProAssist CallCenter" },
            new AppName { Id = Extranet, Name = "Proassist Extranet" });

        db.AppStageNames.AddRange(
            new AppStageName { Id = StageMain, Name = "MAIN", SortOrder = 1 },
            new AppStageName { Id = StageRc0, Name = "RC0", SortOrder = 2 });

        db.ProcessorArchitectures.Add(
            new ProcessorArchitecture { Id = X64, Name = "x64" });

        db.DnsEndpoints.Add(
            new DnsEndpoint { Id = PahaEndpoint, Name = "https://paha.ga.local", IsLoadBalancer = true });

        db.RootPaths.Add(new RootPath { Id = RootSlash, Name = "/" });

        db.PhysicalPaths.Add(new PhysicalPath { Id = DiskDefault, Name = @"c:\inetpub\callcenter" });

        db.Tags.Add(new Tag { Id = TagWeb, Name = "web" });

        db.SaveChanges();

        return testDb;
    }

    /// <summary>
    /// Find-or-create a root path and return its Id. Installations reference paths rather than
    /// storing them, so a test that wants a second deployment has to have somewhere to put it.
    /// </summary>
    public static async Task<int> RootPathIdAsync(ArgusDbContext db, string path)
    {
        var existing = await db.RootPaths.FirstOrDefaultAsync(x => x.Name == path);

        if (existing is not null)
        {
            return existing.Id;
        }

        var created = new RootPath { Name = path };
        db.RootPaths.Add(created);
        await db.SaveChangesAsync();

        return created.Id;
    }

    /// <summary>Find-or-create a physical path and return its Id. See <see cref="RootPathIdAsync"/>.</summary>
    public static async Task<int> PhysicalPathIdAsync(ArgusDbContext db, string path)
    {
        var existing = await db.PhysicalPaths.FirstOrDefaultAsync(x => x.Name == path);

        if (existing is not null)
        {
            return existing.Id;
        }

        var created = new PhysicalPath { Name = path };
        db.PhysicalPaths.Add(created);
        await db.SaveChangesAsync();

        return created.Id;
    }

    /// <summary>
    /// A fresh context over the same database. Assertions use one of these rather than the
    /// context that did the writing, so a stale change tracker cannot make a test pass.
    /// </summary>
    public ArgusDbContext NewContext() => new(options);

    public void Dispose()
    {
        connection.Dispose();
    }
}
