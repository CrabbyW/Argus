using Argus.Api.Configuration;
using Argus.Api.WebApiPoco.Logs;
using Microsoft.Extensions.Options;

namespace Argus.Api.Services;

public interface ILogFileService
{
    IReadOnlyList<LogFileDto> ListFiles();

    /// <summary>Null when the name does not match a file this service is allowed to serve.</summary>
    LogContentDto? Read(string name, int maxLines, string? searchTerm);
}

/// <summary>
/// Reads the files log4net writes, so the action log can be looked at without a remote desktop
/// session and a text editor on the server.
///
/// Read-only by design: there is no endpoint here that deletes or rotates anything. Expiry is
/// <see cref="LogRetentionService"/>'s job, and an audit trail that its own UI can erase is not
/// an audit trail.
/// </summary>
public class LogFileService : ILogFileService
{
    /// <summary>
    /// Hard ceiling on a single response regardless of what was asked for. A log file is the one
    /// thing here that can reach gigabytes; without this, one request could pull all of it
    /// through the API and into a browser.
    /// </summary>
    private const int MaxLinesCeiling = 5000;

    private readonly AuditLogOptions options;

    public LogFileService(IOptions<AuditLogOptions> options)
    {
        this.options = options.Value;
    }

    public IReadOnlyList<LogFileDto> ListFiles()
    {
        var directory = ResolveDirectory();

        if (!Directory.Exists(directory))
        {
            return Array.Empty<LogFileDto>();
        }

        return EnumerateAllowed(directory)
            .Select(path => new FileInfo(path))
            .Select(file => new LogFileDto
            {
                Name = file.Name,
                Kind = KindOf(file.Name),
                SizeBytes = file.Length,
                LastWriteUtc = file.LastWriteTimeUtc
            })
            // Newest first: the file someone opens the screen to look at is almost always today's.
            .OrderByDescending(file => file.LastWriteUtc)
            .ToList();
    }

    public LogContentDto? Read(string name, int maxLines, string? searchTerm)
    {
        var directory = ResolveDirectory();
        var path = ResolveAllowedFile(directory, name);

        if (path is null)
        {
            return null;
        }

        var limit = Math.Clamp(maxLines, 1, MaxLinesCeiling);

        // FileShare.ReadWrite because log4net holds the active file open for writing; without it
        // the newest file — the only one anybody wants — is the one that cannot be read.
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        // A ring buffer of the last `limit` lines, so the file is streamed once and never held in
        // memory whole no matter how large it is.
        var tail = new Queue<string>(limit);
        var matched = 0;

        while (reader.ReadLine() is { } line)
        {
            if (!Matches(line, searchTerm))
            {
                continue;
            }

            matched++;

            if (tail.Count == limit)
            {
                tail.Dequeue();
            }

            tail.Enqueue(line);
        }

        return new LogContentDto
        {
            Name = Path.GetFileName(path),
            Lines = tail.ToList(),
            TotalLines = matched,
            IsTruncated = matched > tail.Count,
            LastWriteUtc = File.GetLastWriteTimeUtc(path)
        };
    }

    private static bool Matches(string line, string? searchTerm) =>
        string.IsNullOrWhiteSpace(searchTerm)
        || line.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The audit file has its own format and its own reason for being read, so the UI is told
    /// which is which rather than having to recognise the file name itself.
    /// </summary>
    private static string KindOf(string fileName) =>
        fileName.StartsWith("argus-actions", StringComparison.OrdinalIgnoreCase)
            ? "action"
            : "diagnostic";

    private string ResolveDirectory() =>
        Path.IsPathRooted(options.Directory)
            ? options.Directory
            : Path.Combine(AppContext.BaseDirectory, options.Directory);

    private IEnumerable<string> EnumerateAllowed(string directory) =>
        options.FilePatterns
            .SelectMany(pattern => Directory.EnumerateFiles(directory, pattern))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Turns a requested name into a path, or nothing.
    ///
    /// The name is never combined with the directory and opened. It is matched against the files
    /// the listing already offers, so "..\..\appsettings.json" — or any other traversal — simply
    /// fails to match instead of having to be detected.
    /// </summary>
    private string? ResolveAllowedFile(string directory, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !Directory.Exists(directory))
        {
            return null;
        }

        return EnumerateAllowed(directory)
            .FirstOrDefault(path =>
                string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase));
    }
}
