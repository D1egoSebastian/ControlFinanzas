using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.Diagnostics;

/// <summary>
/// Turns an unexpected exception into one correlation id that is written in full to the local log and, for UI-thread
/// failures, shown in the error overlay (F-021, DD-2). Implementations never throw.
/// </summary>
public interface IUnexpectedErrorReporter
{
    /// <summary>Logs the exception at Error level and asks the presenter to show the overlay. UI thread only.</summary>
    /// <param name="exception">The unexpected failure.</param>
    /// <param name="source">Short origin label (for example <c>ui</c>).</param>
    /// <returns>The correlation id that appears in the log entry.</returns>
    Guid Report(Exception exception, string source);

    /// <summary>Logs the exception at <paramref name="level"/> without touching any UI. Any thread.</summary>
    /// <param name="exception">The unexpected failure.</param>
    /// <param name="source">Short origin label.</param>
    /// <param name="level">Log level of the entry.</param>
    /// <returns>The correlation id that appears in the log entry.</returns>
    Guid LogOnly(Exception exception, string source, LogLevel level);
}
