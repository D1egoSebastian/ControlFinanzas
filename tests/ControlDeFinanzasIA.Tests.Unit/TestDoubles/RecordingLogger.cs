using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled <see cref="ILogger{TCategoryName}"/> that records every
/// call so a test can assert a warning was logged without a mocking
/// library, matching this codebase's hand-rolled test-double convention.
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<LogLevel> _levels = new();

    /// <summary>Log levels recorded, in call order.</summary>
    public IReadOnlyList<LogLevel> Levels => _levels;

    /// <summary>True if <see cref="LogLevel.Warning"/> was logged at least once.</summary>
    public bool HasWarning => _levels.Contains(LogLevel.Warning);

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) => _levels.Add(logLevel);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
