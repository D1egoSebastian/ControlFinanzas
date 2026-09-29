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

    /// <summary>
    /// Waits between attempts to swap the live database in (200 ms, 600 ms, 1.8 s by default). The number of
    /// entries is the number of retries after the first attempt, so the default makes at most four attempts.
    /// The same schedule paces the rollback attempts after a failed verification.
    /// </summary>
    public IReadOnlyList<TimeSpan> SwapRetryDelays { get; init; } =
    [
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(600),
        TimeSpan.FromMilliseconds(1800),
    ];

    /// <summary>Largest database, in bytes, that a backup may declare or actually inflate to (1 GiB).</summary>
    public long MaxDatabaseBytes { get; init; } = 1L << 30;

    /// <summary>Largest manifest, in bytes, that a backup may declare or actually inflate to (64 KiB).</summary>
    public long MaxManifestBytes { get; init; } = 64 * 1024;

    /// <summary>Asynchronous wait used between retries; tests substitute a no-op.</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;

    /// <summary>How many <c>pre-restore-*.zip</c> safety backups are kept after a restore (the newest ones).</summary>
    public int SafetyBackupsToKeep { get; init; } = 3;
}
