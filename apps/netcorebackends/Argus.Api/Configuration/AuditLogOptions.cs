namespace Argus.Api.Configuration;

/// <summary>
/// The action log: what was done, by which request, and how it ended.
///
/// Kept separate from the diagnostic log because it answers a different question. The
/// diagnostic log is read when something is broken; this one is read when someone asks
/// "who changed this row, and what exactly did they send?" — so it stays parseable
/// (one action per line, four bracketed fields) and it is not allowed to grow forever.
/// </summary>
public class AuditLogOptions
{
    public const string SectionName = "AuditLog";

    /// <summary>
    /// Directory holding the log files, relative to the application base directory unless
    /// an absolute path is given. Must match the paths in `log4net.config`, because that is
    /// what writes the files and this is what deletes them.
    /// </summary>
    public string Directory { get; set; } = "logs";

    /// <summary>
    /// How many days of logs to keep. Anything last written before that is deleted.
    /// Zero or less turns the cleanup off entirely — an explicit "keep everything", so a
    /// misconfigured value cannot quietly delete a site's whole audit trail.
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Only files matching these patterns are considered for deletion. A conservative list,
    /// so the retention sweep can never touch something else that happens to sit in the
    /// directory.
    /// </summary>
    public string[] FilePatterns { get; set; } = new[] { "argus-api.log*", "argus-actions.log*" };

    /// <summary>How often the retention sweep runs. Daily is enough for a day-grained rule.</summary>
    public int SweepIntervalHours { get; set; } = 24;
}
