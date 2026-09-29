using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Backup.CreateBackup;

/// <summary>
/// Exports the whole database to the picked destination (F-019, DD-2). Service failures pass
/// through unchanged; any unexpected exception becomes <see cref="BackupErrorCodes.IoError"/>.
/// Never throws except for cancellation.
/// </summary>
public sealed class CreateBackupUseCase
{
    private readonly IBackupService _service;
    private readonly ILogger<CreateBackupUseCase> _logger;

    /// <summary>Creates the use case.</summary>
    public CreateBackupUseCase(IBackupService service, ILogger<CreateBackupUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(logger);
        _service = service;
        _logger = logger;
    }

    /// <summary>Creates a backup and writes it to <paramref name="target"/>.</summary>
    /// <returns>Success with a <see cref="BackupCreated"/>, or a coded failure.</returns>
    public async Task<Result> ExecuteAsync(SaveTarget target, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        try
        {
            return await _service.CreateAsync(target.OpenWriteAsync, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Creating the backup failed");
            return Result.Failure(BackupErrorCodes.IoError, "The backup could not be created.");
        }
    }
}
