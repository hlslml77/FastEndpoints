using Microsoft.Extensions.Logging;

namespace Web.Services;

/// <summary>
/// Filters EF Core logs so that only information level (or above) logs from
/// the Command category are written – and only when they are produced by our
/// <see cref="SlowQueryInterceptor"/>. Other verbose database logs are ignored.
/// </summary>
public sealed class DbLogFilter : ILoggerProvider, ILogger
{
    private readonly ILogger _inner;

    public DbLogFilter(ILoggerFactory innerFactory)
    {
        _inner = innerFactory.CreateLogger("EFCore");
    }

    public ILogger CreateLogger(string categoryName) => this;

    public void Dispose() { }

    /* ----------------------------------------------------------------
     *  ILogger implementation – filter logic
     * --------------------------------------------------------------*/
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Only interested in the Command category
        if (!eventId.Name?.Contains("Command", StringComparison.OrdinalIgnoreCase) ?? true)
            return;

        _inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
