using ControlDeFinanzasIA.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Infrastructure.Logging;

/// <summary>Logger created by <see cref="RollingFileLoggerProvider"/> for one category (F-021, DD-1).</summary>
/// <remarks>Scopes are ignored. <see cref="Log{TState}"/> never throws.</remarks>
internal sealed class RollingFileLogger(string category, RollingFileSink sink, IClock clock, int maxEntryChars) : ILogger
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && !sink.IsDisabled;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            DateTime now = clock.UtcNow;
            string message = formatter(state, exception);
            sink.Write(now, LogEntryFormatter.Format(now, logLevel, category, eventId, message, exception, maxEntryChars));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A logger must never throw into application code (F-021 R-6).
            _ = ex;
        }
    }
}
