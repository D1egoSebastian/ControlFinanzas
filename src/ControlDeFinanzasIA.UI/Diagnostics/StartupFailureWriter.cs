using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace ControlDeFinanzasIA.UI.Diagnostics;

/// <summary>
/// Writes <c>startup-error-yyyyMMddTHHmmssZ.log</c> for a failure that happened before the UI and the file logger
/// existed (F-021, DD-3). Never throws.
/// </summary>
internal static class StartupFailureWriter
{
    /// <summary>Writes the report to <paramref name="preferredDir"/>, else to the fallback directory.</summary>
    /// <param name="exception">The startup failure.</param>
    /// <param name="id">Correlation id printed to stderr and written to the file.</param>
    /// <param name="preferredDir">The data directory.</param>
    /// <param name="utc">UTC time of the failure.</param>
    /// <param name="fallbackDir">Fallback folder; defaults to the OS temp folder.</param>
    /// <returns>The written path, or <see langword="null"/> when both locations failed.</returns>
    internal static string? Write(Exception exception, Guid id, string? preferredDir, DateTime utc, string? fallbackDir = null)
    {
        string fileName = "startup-error-"
            + utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)
            + ".log";
        string content = Compose(exception, id, utc);

        foreach (string? directory in new[] { preferredDir, fallbackDir ?? Path.GetTempPath() })
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, fileName);
                File.WriteAllText(path, content, new UTF8Encoding(false));
                return path;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                // Try the next location.
            }
        }

        return null;
    }

    private static string Compose(Exception exception, Guid id, DateTime utc)
    {
        var text = new StringBuilder();
        text.Append("ControlDeFinanzasIA ").AppendLine(AppVersionInfo.Current);
        text.Append("Time (UTC): ").AppendLine(utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        text.Append("OS: ").Append(RuntimeInformation.OSDescription).Append(" / ").AppendLine(RuntimeInformation.RuntimeIdentifier);
        text.Append("Error ID: ").AppendLine(id.ToString("D"));
        text.AppendLine();
        text.AppendLine(exception.ToString());
        return text.ToString();
    }
}
