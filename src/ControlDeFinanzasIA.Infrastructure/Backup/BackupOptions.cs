namespace ControlDeFinanzasIA.Infrastructure.Backup;

/// <summary>
/// Tunables of <see cref="SqliteBackupService"/> (F-019, DD-3). Public because Infrastructure has no
/// <c>InternalsVisibleTo</c>; tests replace <see cref="Delay"/> to avoid real waiting.
/// </summary>
/// <param name="AppVersion">Application version written into every manifest.</param>
public sealed record BackupOptions(string AppVersion)
{
    /// <summary>
    /// Waits between attempts to snapshot the live database when SQLite reports it busy or locked.
    /// The number of entries is the number of retries after the first attempt.
    /// </summary>
    public IReadOnlyList<TimeSpan> SnapshotRetryDelays { get; init; } =
    [
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1000),
        TimeSpan.FromMilliseconds(2000),
    ];

    /// <summary>Asynchronous wait used between retries; tests substitute a no-op.</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;
}
