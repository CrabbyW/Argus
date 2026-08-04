using Argus.Api.Configuration;
using Argus.Api.Services;
using Microsoft.Extensions.Options;

namespace Argus.Api.Tests;

/// <summary>
/// The log viewer serves files from disk by name, which is the one place in Argus where a
/// request string turns into a file path. What is asserted here is mostly that it does not:
/// a name only ever selects from the files the listing already offers.
/// </summary>
public class LogFileServiceTests : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), $"argus-logs-{Guid.NewGuid():N}");

    public LogFileServiceTests()
    {
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private LogFileService Service() => new(Options.Create(new AuditLogOptions
    {
        Directory = directory
    }));

    private void WriteLog(string name, params string[] lines) =>
        File.WriteAllLines(Path.Combine(directory, name), lines);

    [Fact]
    public void ListFiles_ReturnsOnlyTheConfiguredPatterns()
    {
        WriteLog("argus-actions.log", "one");
        WriteLog("argus-api.log", "two");
        File.WriteAllText(Path.Combine(directory, "appsettings.json"), "{}");

        var names = Service().ListFiles().Select(file => file.Name).ToList();

        Assert.Equal(2, names.Count);
        Assert.DoesNotContain("appsettings.json", names);
    }

    [Fact]
    public void ListFiles_MarksTheActionLogApartFromTheDiagnosticOne()
    {
        WriteLog("argus-actions.log", "one");
        WriteLog("argus-api.log", "two");

        var files = Service().ListFiles();

        Assert.Equal("action", files.Single(file => file.Name == "argus-actions.log").Kind);
        Assert.Equal("diagnostic", files.Single(file => file.Name == "argus-api.log").Kind);
    }

    [Fact]
    public void Read_ReturnsTheTailNewestLast()
    {
        WriteLog("argus-api.log", "1", "2", "3", "4");

        var content = Service().Read("argus-api.log", maxLines: 2, searchTerm: null);

        Assert.NotNull(content);
        Assert.Equal(new[] { "3", "4" }, content!.Lines);
        Assert.Equal(4, content.TotalLines);
        Assert.True(content.IsTruncated);
    }

    [Fact]
    public void Read_FiltersCaseInsensitivelyAndCountsOnlyMatches()
    {
        WriteLog("argus-actions.log", "[Users_SearchUsers] ok", "[Installations_Create] ok", "users again");

        var content = Service().Read("argus-actions.log", maxLines: 500, searchTerm: "USERS");

        Assert.NotNull(content);
        Assert.Equal(2, content!.TotalLines);
        Assert.False(content.IsTruncated);
    }

    [Theory]
    [InlineData("../appsettings.json")]
    [InlineData("..\\appsettings.json")]
    [InlineData("appsettings.json")]
    [InlineData("argus-api.log/../../secrets.txt")]
    public void Read_RefusesAnythingOutsideTheOfferedFiles(string name)
    {
        WriteLog("argus-api.log", "one");
        File.WriteAllText(Path.Combine(directory, "appsettings.json"), "{}");

        Assert.Null(Service().Read(name, maxLines: 500, searchTerm: null));
    }

    /// <summary>
    /// The file log4net is writing to right now is the only one anyone wants to look at, and it
    /// is held open for writing the whole time the API is up.
    /// </summary>
    [Fact]
    public void Read_WorksWhileTheFileIsOpenForWriting()
    {
        var path = Path.Combine(directory, "argus-api.log");

        using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        using var streamWriter = new StreamWriter(writer) { AutoFlush = true };
        streamWriter.WriteLine("live line");

        var content = Service().Read("argus-api.log", maxLines: 500, searchTerm: null);

        Assert.NotNull(content);
        Assert.Equal(new[] { "live line" }, content!.Lines);
    }
}
