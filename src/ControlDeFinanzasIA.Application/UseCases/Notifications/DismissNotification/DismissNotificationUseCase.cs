using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Notifications;

namespace ControlDeFinanzasIA.Application.UseCases.Notifications.DismissNotification;

/// <summary>
/// Use case que descarta un aviso de forma permanente (F-017, R-8, D-6).
/// Sigue la forma de <c>AcknowledgeAnomalyUseCase</c>, salvo que un aviso
/// inexistente es un no-op exitoso y no un <c>NotFound</c>: la fila puede
/// desaparecer legítimamente (purga o reconciliación) entre la carga de la
/// lista y el clic. Descartar un aviso nunca reconoce la alerta de anomalía
/// subyacente (R-6).
/// </summary>
public sealed class DismissNotificationUseCase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IClock _clock;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public DismissNotificationUseCase(INotificationRepository notificationRepository, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(notificationRepository);
        ArgumentNullException.ThrowIfNull(clock);

        _notificationRepository = notificationRepository;
        _clock = clock;
    }

    /// <summary>
    /// Descarta el aviso identificado por <paramref name="id"/>. Idempotente:
    /// descartar un aviso ya descartado conserva el timestamp original.
    /// </summary>
    /// <param name="id">Identificador del aviso a descartar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result.Success(object)"/> si el aviso fue descartado, ya
    /// estaba descartado o ya no existe; el <see cref="Result"/> del
    /// guardado si este falla.
    /// </returns>
    public async Task<Result> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        Notification? notification = await _notificationRepository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (notification is null || notification.IsDismissed)
        {
            return Result.Success(null!);
        }

        notification.Dismiss(_clock.UtcNow);
        return await _notificationRepository.UpdateRangeAsync([notification], ct).ConfigureAwait(false);
    }
}
