using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;

namespace Web.Services;

/// <summary>
/// Logs only those EF Core SQL commands whose execution time exceeds a certain threshold.
/// Helps keep the logs concise while still surfacing slow queries.
/// </summary>
public sealed class SlowQueryInterceptor : DbCommandInterceptor
{
    private readonly double _thresholdMs;

    /// <param name="thresholdMs">Minimum duration (in milliseconds) that a query must take before it is logged.</param>
    public SlowQueryInterceptor(double thresholdMs = 200)
    {
        _thresholdMs = thresholdMs;
    }

    /* ----------------------------------------------------------------
     * Helper
     * --------------------------------------------------------------*/
    private static string Compact(string sql) => LogSanitizer.CompactWhitespace(sql);

    private void LogIfSlow(DbCommand command, TimeSpan duration)
    {
        if (duration.TotalMilliseconds < _thresholdMs) return;

        Log.Warning("SLOW SQL ({ElapsedMs}ms) -> {Command}",
            Math.Round(duration.TotalMilliseconds, 2),
            Compact(command.CommandText));
    }

    /* ----------------------------------------------------------------
     * Overrides – executed commands
     * --------------------------------------------------------------*/
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        LogIfSlow(command, eventData.Duration);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        LogIfSlow(command, eventData.Duration);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        LogIfSlow(command, eventData.Duration);
        return base.NonQueryExecuted(command, eventData, result);
    }
}
