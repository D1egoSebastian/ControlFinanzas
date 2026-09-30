namespace ControlDeFinanzasIA.Infrastructure.Logging;

/// <summary>Options of the rolling file logger (F-021, DD-1).</summary>
/// <param name="Directory">Folder that holds the <c>app-YYYYMMDD[.n].log</c> files.</param>
/// <param name="MaxFileBytes">Soft cap of one file; the next entry that would exceed it opens the next index.</param>
/// <param name="MaxFiles">Number of log files kept (newest by parsed date and index).</param>
/// <param name="MaxEntryChars">Entries longer than this are truncated with a marker.</param>
public sealed record RollingFileLoggerOptions(
    string Directory,
    long MaxFileBytes = 5 * 1024 * 1024,
    int MaxFiles = 5,
    int MaxEntryChars = 64 * 1024);
