using Argus.Api.Database;
using Argus.Api.Database.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Argus.Api.Tests;

/// <summary>
/// A throwaway database for one test, built from the real model with the real constraints.
///
/// SQLite in memory rather than the InMemory provider on purpose: the behaviour under test is
/// relational. The unique deployment index and its <c>IsEnabled = 1</c> filter only exist in a
/// provider that has indexes, and <c>EF.Functions.Like</c> only translates in one that has SQL.
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
    public const int ProAssistNet = 1;
    public const int VipSprava = 2;
    public const int StageMain = 1;
    public const int StageRc0 = 2;
    public const int X64 = 1;
    public const int PahaEndpoint = 1;

    public static TestDb CreateSeeded()
    {
        var testDb = new TestDb();

        using var db = testDb.NewContext();

        db.Machines.AddRange(
            new Machine { Id = Gaiis1, MachineName = "GAIIS1" },
            new Machine { Id = Gaiis2, MachineName = "GAIIS2" });

        db.Applications.AddRange(
            new Application { Id = ProAssistNet, AppName = "ProAssistNet" },
            new Application { Id = VipSprava, AppName = "VipSprava" });

        db.AppStages.AddRange(
            new AppStage { Id = StageMain, StageName = "Main", SortOrder = 1 },
            new AppStage { Id = StageRc0, StageName = "RC0", SortOrder = 2 });

        db.ProcessorArchitectures.Add(
            new ProcessorArchitecture { Id = X64, ArchitectureName = "x64" });

        db.DnsEndpoints.Add(
            new DnsEndpoint { Id = PahaEndpoint, DnsName = "https://paha.ga.local", IsLoadBalancer = true });

        db.SaveChanges();

        return testDb;
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
