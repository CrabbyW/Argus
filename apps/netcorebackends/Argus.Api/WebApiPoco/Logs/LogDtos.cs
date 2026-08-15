using Argus.Api.WebApiPoco.Common;

namespace Argus.Api.WebApiPoco.Logs;

/// <summary>One file in the log directory, as offered to the log viewer.</summary>
public class LogFileDto
{
    /// <summary>File name only, never a path — it is what the read endpoint takes back.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>"action" for the audit trail, "diagnostic" for everything else.</summary>
    public string Kind { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTime LastWriteUtc { get; set; }
}

/// <summary>Criteria for reading one log file. All optional; the defaults are the tail of it.</summary>
public class LogReadRequestDto : ReadRequestDto
{
    /// <summary>
    /// How many lines from the end to return. Clamped by the service, because a log file is the
    /// one thing in Argus that can be gigabytes and a client asking for all of it would be
    /// answered with all of it.
    /// </summary>
    public int MaxLines { get; set; } = 500;

    /// <summary>Case-insensitive substring; only matching lines are returned.</summary>
    public string? SearchTerm { get; set; }
}

/// <summary>The tail of one log file, newest line last, plus what had to be dropped to fit.</summary>
public class LogContentDto
{
    public string Name { get; set; } = string.Empty;

    public IReadOnlyList<string> Lines { get; set; } = Array.Empty<string>();

    /// <summary>Lines the file holds after filtering — more than <see cref="Lines"/> when truncated.</summary>
    public int TotalLines { get; set; }

    /// <summary>True when older lines were left out because <c>MaxLines</c> was reached.</summary>
    public bool IsTruncated { get; set; }

    public DateTime LastWriteUtc { get; set; }
}
