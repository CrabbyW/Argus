using Argus.Api.Configuration;
using log4net;
using Microsoft.Extensions.Options;

namespace Argus.Api.Services;

/// <summary>
/// Deletes log files older than <see cref="AuditLogOptions.RetentionDays"/>.
///
/// log4net's own `maxSizeRollBackups` caps the number of files, not their age — with a quiet
/// week and a busy one those are not the same thing, and the retention rule an operator is
/// asked about is always stated in days. So the age rule lives here, reading the same
/// configured value the rest of the app sees.
/// </summary>
public class LogRetentionService : BackgroundService
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(LogRetentionService));

    private readonly AuditLogOptions options;

    public LogRetentionService(IOptions<AuditLogOptions> options)
    {
        this.options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.RetentionDays <= 0)
        {
            logger.Info("Log retention is off (AuditLog:RetentionDays is not positive); no log file will be deleted.");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, options.SweepIntervalHours));

        // Sweep at startup too: a machine that is only switched on now and then would
        // otherwise never reach the scheduled sweep and keep every file it ever wrote.
        while (!stoppingToken.IsCancellationRequested)
        {
            Sweep();

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void Sweep()
    {
        var directory = Path.IsPathRooted(options.Directory)
            ? options.Directory
            : Path.Combine(AppContext.BaseDirectory, options.Directory);

        if (!Directory.Exists(directory))
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-options.RetentionDays);
        var deleted = 0;

        foreach (var pattern in options.FilePatterns)
        {
            foreach (var path in Directory.EnumerateFiles(directory, pattern))
            {
                // The file log4net is writing to right now is the newest one, so the age test
                // excludes it on its own — no special case needed for the active file.
                if (File.GetLastWriteTimeUtc(path) >= cutoff)
                {
                    continue;
                }

                try
                {
                    File.Delete(path);
                    deleted++;
                }
                catch (IOException ex)
                {
                    // A locked file is not a fault worth stopping the sweep for; the next
                    // pass will pick it up.
                    logger.Warn($"Could not delete the expired log file {path}: {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.Warn($"Not allowed to delete the expired log file {path}: {ex.Message}");
                }
            }
        }

        if (deleted > 0)
        {
            logger.Info($"Log retention removed {deleted} file(s) older than {options.RetentionDays} day(s) from {directory}.");
        }
    }
}
