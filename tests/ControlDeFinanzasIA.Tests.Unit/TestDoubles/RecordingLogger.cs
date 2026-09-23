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
    private readonly List<string> _messages = new();
    private readonly List<KeyValuePair<string, object?>> _properties = new();

    /// <summary>Log levels recorded, in call order.</summary>
    public IReadOnlyList<LogLevel> Levels => _levels;

    /// <summary>
    /// Formatted messages recorded, in call order (F-013, D-8). Added
    /// additively so no existing test needed to change.
    /// </summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <summary>
    /// Structured state key/value pairs recorded across every call, in
    /// call order (F-013, D-8). Only entries from a state that implements
    /// <see cref="IEnumerable{T}"/> of <see cref="KeyValuePair{TKey, TValue}"/>
    /// (the shape the framework's structured-logging templates produce) are
    /// captured.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, object?>> Properties => _properties;

    /// <summary>True if <see cref="LogLevel.Warning"/> was logged at least once.</summary>
    public bool HasWarning => _levels.Contains(LogLevel.Warning);

    /// <summary>
    /// True if <see cref="LogLevel.Error"/> was logged at least once
    /// (F-014, slice S7). Added additively so no existing test needed to
    /// change.
    /// </summary>
    public bool HasError => _levels.Contains(LogLevel.Error);

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _levels.Add(logLevel);
        _messages.Add(formatter(state, exception));

        if (state is IEnumerable<KeyValuePair<string, object?>> properties)
        {
            _properties.AddRange(properties);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
