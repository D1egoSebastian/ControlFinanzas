using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Creates, inspects and restores full-database backups (F-019). All members return coded
/// <see cref="Result"/> failures for data problems; none of them throws across this boundary
/// except for cancellation.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Builds a backup and, only after every check passed, writes it to the stream returned by
    /// <paramref name="openDestination"/> so a failed backup never creates or truncates the file.
    /// </summary>
    /// <returns>Success with a <c>BackupCreated</c> value, or a coded failure.</returns>
    Task<Result> CreateAsync(Func<CancellationToken, Task<Stream>> openDestination, CancellationToken ct = default);

    /// <summary>Validates a backup without changing anything outside the service's own scratch space.</summary>
    /// <returns>Success with a <c>BackupInfo</c> value, or a coded failure.</returns>
    Task<Result> InspectAsync(Func<CancellationToken, Task<Stream>> openRead, CancellationToken ct = default);

    /// <summary>Re-validates a backup in full and replaces the live database with it.</summary>
    /// <returns>Success with a <c>RestoreOutcome</c> value, or a coded failure.</returns>
    Task<Result> RestoreAsync(Func<CancellationToken, Task<Stream>> openRead, CancellationToken ct = default);

    /// <summary>
    /// Startup hook: removes the leftovers of a completed restore. Never throws; failures are logged.
    /// </summary>
    void CompletePendingRestore();
}
