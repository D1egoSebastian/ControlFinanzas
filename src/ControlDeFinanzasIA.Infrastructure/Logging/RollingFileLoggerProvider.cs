using ControlDeFinanzasIA.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Infrastructure.Logging;

/// <summary>
/// Logger provider that appends to rolling plain-text files in a local folder (F-021, DD-1).
/// </summary>
/// <remarks>
/// Public because Infrastructure has no <c>InternalsVisibleTo</c>. One provider owns one
/// <see cref="RollingFileSink"/>; all loggers it creates share it.
/// </remarks>
[ProviderAlias("RollingFile")]
public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private readonly RollingFileSink _sink;
    private readonly IClock _clock;
    private readonly int _maxEntryChars;

    /// <summary>Initializes a new instance of the <see cref="RollingFileLoggerProvider"/> class.</summary>
    /// <param name="options">Directory and limits.</param>
    /// <param name="clock">Time source (UTC).</param>
    public RollingFileLoggerProvider(RollingFileLoggerOptions options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _sink = new RollingFileSink(options);
        _clock = clock;
        _maxEntryChars = options.MaxEntryChars;
    }

    /// <summary>Gets a value indicating whether file logging stopped after a failure (test seam).</summary>
    public bool IsDisabled => _sink.IsDisabled;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) =>
        new RollingFileLogger(categoryName, _sink, _clock, _maxEntryChars);

    /// <inheritdoc />
    public void Dispose() => _sink.Dispose();
}
