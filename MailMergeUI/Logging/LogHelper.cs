using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMergeUI.Logging
{
    public static class LogHelper
    {
        private const string LogFolder = "Logs";
        private const string LogFileTemplate = "app-{Date}.log";
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFolder);

        public static ILogger Configure()
        {
            // Ensure folder exists
            Directory.CreateDirectory(LogPath);

            // Clean old logs **before** creating the logger (date-based, for precision)
            DeleteOldLogs();

            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .WriteTo.File(
                    path: Path.Combine(LogPath, LogFileTemplate),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,              // Built-in: Keeps newest 7 files (≈7 days)
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(5))
                .CreateLogger();

            Serilog.Log.Logger = logger;
            Log.Information("Logger configured with daily rolling and 7-day retention");

            return logger;
        }

        private static void DeleteOldLogs()
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-7);
                var files = Directory.GetFiles(LogPath, "app-*.log")
                    .Where(f => {
                        // Parse date from filename for accuracy (better than creation time, which may not update on rollover)
                        var filename = Path.GetFileNameWithoutExtension(f);
                        if (DateTime.TryParseExact(filename.Substring(4), "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
                        {
                            return fileDate < cutoff;
                        }
                        // Fallback to creation time if parsing fails
                        return File.GetCreationTimeUtc(f) < cutoff;
                    });

                var deletedCount = 0;
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        var filename = Path.GetFileName(file);
                        Log.Information("Deleted old log: {File}", filename);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to delete old log: {File}", Path.GetFileName(file));
                    }
                }
                if (deletedCount > 0)
                {
                    Log.Information("Cleanup completed: {Count} old files deleted", deletedCount);
                }
            }
            catch (Exception ex)
            {
                // Fail gracefully—app startup shouldn't crash on cleanup
                // (Log here only if logger is already configured; otherwise, skip)
                try { Log.Warning(ex, "Log cleanup failed at startup"); } catch { /* Logger not ready */ }
            }
        }
    }
}
