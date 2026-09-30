using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Infrastructure.Logging;

/// <summary>Formats one log entry as plain text (F-021, DD-1).</summary>
/// <remarks>
/// Shape: <c>2026-09-30T14:03:11.123Z INF Category[EventId] message</c>. The timestamp is always UTC.
/// Every continuation line (multi-line message, exception text, stack) is indented four spaces so each
/// entry starts with a timestamp and the file stays greppable.
/// </remarks>
public static class LogEntryFormatter
{
    private const string _truncationMarker = "…[truncated]";
    private const string _indent = "    ";

    /// <summary>Builds the text of one entry, without a trailing newline.</summary>
    /// <param name="timestamp">Moment of the entry; converted to UTC when it is not already.</param>
    /// <param name="level">Log level.</param>
    /// <param name="category">Logger category.</param>
    /// <param name="eventId">Event id (only the numeric id is written).</param>
    /// <param name="message">Formatted message.</param>
    /// <param name="exception">Optional exception; written with its full chain.</param>
    /// <param name="maxEntryChars">Entries longer than this are cut and end with a truncation marker.</param>
    /// <returns>The formatted entry.</returns>
    public static string Format(
        DateTime timestamp,
        LogLevel level,
        string category,
        EventId eventId,
        string message,
        Exception? exception,
        int maxEntryChars)
    {
        DateTime utc = timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime();

        var builder = new StringBuilder(128);
        builder.Append(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(LevelText(level))
            .Append(' ')
            .Append(category)
            .Append('[')
            .Append(eventId.Id.ToString(CultureInfo.InvariantCulture))
            .Append("] ");
        AppendIndented(builder, message, firstLineInline: true);

        if (exception is not null)
        {
            builder.Append('\n');
            AppendIndented(builder, exception.ToString(), firstLineInline: false);
        }

        if (maxEntryChars > 0 && builder.Length > maxEntryChars)
        {
            builder.Length = maxEntryChars;
            builder.Append(_truncationMarker);
        }

        return builder.ToString();
    }

    private static void AppendIndented(StringBuilder builder, string text, bool firstLineInline)
    {
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            if (i > 0 || !firstLineInline)
            {
                builder.Append(_indent);
            }

            builder.Append(lines[i].TrimEnd());
        }
    }

    private static string LevelText(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???",
    };
}
