using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Notifications;

namespace ControlDeFinanzasIA.Application.UseCases.Notifications.MarkNotificationsRead;

/// <summary>
/// Use case que marca como leídos los avisos indicados (F-017, R-8, D-6).
/// Abrir el flyout marca solo las filas listadas; los ids que ya no existen
/// (purgados o eliminados por la reconciliación) se ignoran en silencio.
/// </summary>
public sealed class MarkNotificationsReadUseCase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IClock _clock;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public MarkNotificationsReadUseCase(INotificationRepository notificationRepository, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(notificationRepository);
        ArgumentNullException.ThrowIfNull(clock);

        _notificationRepository = notificationRepository;
        _clock = clock;
    }

    /// <summary>
    /// Marca como leídos los avisos con los ids dados. Idempotente: un aviso
    /// ya leído conserva su primer <see cref="Notification.ReadAt"/>.
    /// </summary>
    /// <param name="ids">Ids de los avisos a marcar como leídos.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result.Success(object)"/> aunque ningún id exista o no haya
    /// nada que cambiar; el <see cref="Result"/> del guardado si este falla.
    /// </returns>
    public async Task<Result> ExecuteAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return Result.Success(null!);
        }

        IReadOnlyList<Notification> notifications = await _notificationRepository
            .GetByIdsAsync(ids, ct)
            .ConfigureAwait(false);

        DateTime now = _clock.UtcNow;
        var changed = new List<Notification>();

        foreach (Notification notification in notifications)
        {
            if (notification.IsRead)
            {
                continue;
            }

            notification.MarkRead(now);
            changed.Add(notification);
        }

        if (changed.Count == 0)
        {
            return Result.Success(null!);
        }

        return await _notificationRepository.UpdateRangeAsync(changed, ct).ConfigureAwait(false);
    }
}
