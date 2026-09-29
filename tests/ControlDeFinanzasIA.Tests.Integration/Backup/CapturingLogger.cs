using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>Logger that keeps every entry (level and formatted message) so tests can assert on them.</summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];
    private readonly object _sync = new();

    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (_sync)
            {
                return _entries.ToArray();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, exception);
        lock (_sync)
        {
            _entries.Add((logLevel, message));
        }
    }
}
