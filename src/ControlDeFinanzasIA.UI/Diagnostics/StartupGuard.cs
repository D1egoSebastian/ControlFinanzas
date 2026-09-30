using ControlDeFinanzasIA.UI.System;

namespace ControlDeFinanzasIA.UI.Diagnostics;

/// <summary>
/// Runs the whole application start-up and converts anything that escapes it into a report file, one line on stderr
/// and exit code 1 (F-021, DD-3). No UI is attempted and the host logger is not used (it may be the broken part).
/// </summary>
internal static class StartupGuard
{
    /// <summary>Runs <paramref name="body"/>; returns its exit code or 1 after reporting a failure.</summary>
    /// <param name="body">The start-up body.</param>
    /// <param name="dataDirOverride">Reads the data-directory override (environment variable).</param>
    /// <param name="stderr">Where the one-line failure message goes.</param>
    /// <param name="utcNow">UTC clock.</param>
    /// <param name="fallbackDirectory">Fallback folder for the report; defaults to the OS temp folder.</param>
    /// <returns>The process exit code.</returns>
    internal static int Run(
        Func<int> body,
        Func<string?> dataDirOverride,
        TextWriter stderr,
        Func<DateTime> utcNow,
        string? fallbackDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(dataDirOverride);
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(utcNow);

        try
        {
            return body();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Report(ex, dataDirOverride, stderr, utcNow, fallbackDirectory);
        }
    }

    private static int Report(
        Exception exception,
        Func<string?> dataDirOverride,
        TextWriter stderr,
        Func<DateTime> utcNow,
        string? fallbackDirectory)
    {
        var id = Guid.NewGuid();
        string? preferred = null;
        try
        {
            preferred = SystemPathResolver.ResolveDataDirectory(dataDirOverride());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // An unreadable override only loses the preferred folder; the fallback is still tried.
        }

        string? path = null;
        try
        {
            path = StartupFailureWriter.Write(exception, id, preferred, utcNow(), fallbackDirectory);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The report is best-effort; the stderr line below still carries the id.
        }

        try
        {
            if (path is null)
            {
                stderr.WriteLine($"ControlDeFinanzasIA failed to start. Error ID: {id:D}. The report file could not be written.");
                stderr.WriteLine(exception.ToString());
            }
            else
            {
                stderr.WriteLine($"ControlDeFinanzasIA failed to start. Error ID: {id:D}. Details: {path}");
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // stderr is gone; the exit code is the only signal left.
        }

        return 1;
    }
}
