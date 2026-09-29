using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Backup.InspectBackup;

/// <summary>
/// Validates a picked backup file without changing anything (F-019, DD-2). Service failures pass
/// through unchanged; any unexpected exception becomes <see cref="BackupErrorCodes.IoError"/>.
/// Never throws except for cancellation.
/// </summary>
public sealed class InspectBackupUseCase
{
    private readonly IBackupService _service;
    private readonly ILogger<InspectBackupUseCase> _logger;

    /// <summary>Creates the use case.</summary>
    public InspectBackupUseCase(IBackupService service, ILogger<InspectBackupUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(logger);
        _service = service;
        _logger = logger;
    }

    /// <summary>Inspects the backup behind <paramref name="source"/>.</summary>
    /// <returns>Success with a <see cref="BackupInfo"/>, or a coded failure.</returns>
    public async Task<Result> ExecuteAsync(OpenTarget source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            return await _service.InspectAsync(source.OpenReadAsync, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inspecting the backup failed");
            return Result.Failure(BackupErrorCodes.IoError, "The backup could not be read.");
        }
    }
}
