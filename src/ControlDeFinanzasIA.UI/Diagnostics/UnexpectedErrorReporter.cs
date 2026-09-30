using ControlDeFinanzasIA.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.Diagnostics;

/// <summary>
/// Default <see cref="IUnexpectedErrorReporter"/>: one Guid per failure, the full exception in the local log and a
/// best-effort stderr line so the id survives a disabled file logger (F-021, DD-2). It never throws and uses no network.
/// </summary>
public sealed class UnexpectedErrorReporter : IUnexpectedErrorReporter
{
    private const string _template = "Unexpected error {CorrelationId} in {Source}";

    private readonly ILogger<UnexpectedErrorReporter> _logger;
    private readonly IClock _clock;
    private readonly IErrorPresenter _presenter;

    /// <summary>Initializes a new instance of the <see cref="UnexpectedErrorReporter"/> class.</summary>
    /// <param name="logger">Logger whose output reaches the rolling file.</param>
    /// <param name="clock">Time source.</param>
    /// <param name="presenter">Overlay presenter.</param>
    public UnexpectedErrorReporter(ILogger<UnexpectedErrorReporter> logger, IClock clock, IErrorPresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(presenter);
        _logger = logger;
        _clock = clock;
        _presenter = presenter;
    }

    /// <inheritdoc />
    public Guid Report(Exception exception, string source)
    {
        Guid id = Write(exception, source, LogLevel.Error);
        try
        {
            _presenter.Present(id, _clock.UtcNow);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            WriteStderr($"Error overlay failed for {id}: {ex.GetType().Name}");
        }

        return id;
    }

    /// <inheritdoc />
    public Guid LogOnly(Exception exception, string source, LogLevel level) => Write(exception, source, level);

    private Guid Write(Exception exception, string source, LogLevel level)
    {
        var id = Guid.NewGuid();
        string origin = string.IsNullOrWhiteSpace(source) ? "unknown" : source;
        try
        {
            _logger.Log(level, exception, _template, id, origin);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            WriteStderr($"Logging failed for {id}: {ex.GetType().Name}");
        }

        WriteStderr($"{level}: Unexpected error {id} in {origin}: {exception?.GetType().FullName}");
        return id;
    }

    private static void WriteStderr(string line)
    {
        try
        {
            Console.Error.WriteLine(line);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Nothing left to try: stderr itself is gone.
        }
    }
}
