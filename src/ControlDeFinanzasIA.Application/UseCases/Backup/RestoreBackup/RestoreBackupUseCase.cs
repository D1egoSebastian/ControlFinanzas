using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Backup.RestoreBackup;

/// <summary>
/// Replaces the live database with a picked backup (F-019, DD-2). Service failures pass
/// through unchanged; any unexpected exception becomes <see cref="BackupErrorCodes.IoError"/>.
/// Never throws except for cancellation.
/// </summary>
public sealed class RestoreBackupUseCase
{
    private readonly IBackupService _service;
    private readonly ILogger<RestoreBackupUseCase> _logger;

    /// <summary>Creates the use case.</summary>
    public RestoreBackupUseCase(IBackupService service, ILogger<RestoreBackupUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(logger);
        _service = service;
        _logger = logger;
    }

    /// <summary>Restores the backup behind <paramref name="source"/>.</summary>
    /// <returns>Success with a <see cref="RestoreOutcome"/>, or a coded failure.</returns>
    public async Task<Result> ExecuteAsync(OpenTarget source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            return await _service.RestoreAsync(source.OpenReadAsync, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restoring the backup failed");
            return Result.Failure(BackupErrorCodes.IoError, "The backup could not be restored.");
        }
    }
}
