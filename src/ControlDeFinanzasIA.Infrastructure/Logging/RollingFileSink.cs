using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ControlDeFinanzasIA.Infrastructure.Logging;

/// <summary>
/// Owns the lock, the file stream and the rolling of the log files (F-021, DD-1).
/// </summary>
/// <remarks>
/// Files are named <c>app-YYYYMMDD.log</c>, then <c>app-YYYYMMDD.1.log</c>, <c>.2.log</c>... using the UTC
/// date of the entry. Retention keeps the newest files ordered by the (date, index) parsed from the NAME,
/// never by file-system timestamps. A failure to create, open or write disables the sink for good (one
/// stderr line); a failure to delete an old file is reported once and ignored. No method throws.
/// </remarks>
public sealed partial class RollingFileSink : IDisposable
{
    private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly object _gate = new();
    private readonly RollingFileLoggerOptions _options;
    private FileStream? _stream;
    private string? _currentPath;
    private DateOnly _currentDate;
    private long _currentBytes;
    private bool _disabled;
    private bool _retentionFailureReported;

    /// <summary>Initializes a new instance of the <see cref="RollingFileSink"/> class.</summary>
    /// <param name="options">Directory and limits.</param>
    public RollingFileSink(RollingFileLoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>Gets a value indicating whether the sink stopped writing after a failure.</summary>
    public bool IsDisabled
    {
        get
        {
            lock (_gate)
            {
                return _disabled;
            }
        }
    }

    /// <summary>Appends one entry (a newline is added) to the current file, rolling first when needed.</summary>
    /// <param name="utc">Moment of the entry, used to pick the dated file.</param>
    /// <param name="text">Entry text without a trailing newline.</param>
    public void Write(DateTime utc, string text)
    {
        lock (_gate)
        {
            if (_disabled)
            {
                return;
            }

            try
            {
                byte[] bytes = _utf8NoBom.GetBytes(text + "\n");
                EnsureFileFor(DateOnly.FromDateTime(utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime()), bytes.Length);
                _stream!.Write(bytes, 0, bytes.Length);
                _stream.Flush();
                _currentBytes += bytes.Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Security.SecurityException)
            {
                Disable(ex);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            CloseStream();
            _disabled = true;
        }
    }

    [GeneratedRegex(@"^app-(?<d>\d{8})(?:\.(?<i>\d+))?\.log$", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();

    private static string FileName(DateOnly date, int index) =>
        index == 0
            ? $"app-{date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log"
            : $"app-{date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.{index.ToString(CultureInfo.InvariantCulture)}.log";

    private static bool TryParse(string path, out DateOnly date, out int index)
    {
        date = default;
        index = 0;
        Match match = FileNamePattern().Match(Path.GetFileName(path));
        if (!match.Success
            || !DateOnly.TryParseExact(match.Groups["d"].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return false;
        }

        return !match.Groups["i"].Success || int.TryParse(match.Groups["i"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }

    private void EnsureFileFor(DateOnly date, int entryBytes)
    {
        if (_stream is null)
        {
            System.IO.Directory.CreateDirectory(_options.Directory);
            OpenHighestIndexFor(date);
        }
        else if (date != _currentDate)
        {
            CloseStream();
            OpenHighestIndexFor(date);
        }

        if (_currentBytes > 0 && _currentBytes + entryBytes > _options.MaxFileBytes)
        {
            int next = ParseIndex(_currentPath!) + 1;
            CloseStream();
            Open(date, next);
        }
    }

    private void OpenHighestIndexFor(DateOnly date)
    {
        int highest = 0;
        foreach (string path in System.IO.Directory.EnumerateFiles(_options.Directory, "app-*.log"))
        {
            if (TryParse(path, out DateOnly d, out int i) && d == date && i > highest)
            {
                highest = i;
            }
        }

        Open(date, highest);
    }

    private int ParseIndex(string path) => TryParse(path, out _, out int i) ? i : 0;

    private void Open(DateOnly date, int index)
    {
        string path = Path.Combine(_options.Directory, FileName(date, index));
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        _stream = stream;
        _currentPath = path;
        _currentDate = date;
        _currentBytes = stream.Length;
        Prune();
    }

    private void Prune()
    {
        try
        {
            var files = new List<(string Path, DateOnly Date, int Index)>();
            foreach (string path in System.IO.Directory.EnumerateFiles(_options.Directory, "app-*.log"))
            {
                if (TryParse(path, out DateOnly d, out int i))
                {
                    files.Add((path, d, i));
                }
            }

            foreach ((string path, _, _) in files.OrderByDescending(f => f.Date).ThenByDescending(f => f.Index).Skip(Math.Max(1, _options.MaxFiles)))
            {
                if (string.Equals(path, _currentPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    ReportRetentionFailure(path, ex);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ReportRetentionFailure(_options.Directory, ex);
        }
    }

    private void ReportRetentionFailure(string path, Exception ex)
    {
        if (_retentionFailureReported)
        {
            return;
        }

        _retentionFailureReported = true;
        WriteStderr($"[ControlDeFinanzasIA] Log retention could not delete '{path}': {ex.GetType().Name}: {ex.Message}");
    }

    private void Disable(Exception ex)
    {
        _disabled = true;
        CloseStream();
        WriteStderr($"[ControlDeFinanzasIA] File logging disabled: {ex.GetType().Name}: {ex.Message}");
    }

    private void CloseStream()
    {
        try
        {
            _stream?.Dispose();
        }
        catch (IOException)
        {
            // Closing a log file must never surface an error.
        }
        finally
        {
            _stream = null;
        }
    }

    private static void WriteStderr(string line)
    {
        try
        {
            Console.Error.WriteLine(line);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Nowhere left to report to.
        }
    }
}
