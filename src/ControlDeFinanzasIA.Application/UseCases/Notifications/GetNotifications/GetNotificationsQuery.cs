using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Notifications;
using ControlDeFinanzasIA.Domain.Notifications;

namespace ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;

/// <summary>
/// Query que lista los avisos vigentes para el flyout (F-017, R-10, D-6).
/// Lee los avisos no descartados, los ordena (no leídos primero, luego
/// <see cref="Notification.ReferenceDate"/> ascendente, luego
/// <see cref="Notification.Kind"/>, luego <see cref="Notification.Id"/>) y
/// los recorta a <see cref="NotificationRules.FlyoutCap"/>. El contador de
/// no leídos cuenta todos los avisos no descartados y no leídos, sin
/// importar el tope.
/// </summary>
public sealed class GetNotificationsQuery
{
    private readonly INotificationRepository _notificationRepository;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public GetNotificationsQuery(INotificationRepository notificationRepository)
    {
        ArgumentNullException.ThrowIfNull(notificationRepository);
        _notificationRepository = notificationRepository;
    }

    /// <summary>
    /// Ejecuta la query.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> exitoso con un <see cref="NotificationListDto"/>
    /// en <see cref="Result.Value"/>.
    /// </returns>
    public async Task<Result> ExecuteAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Notification> undismissed = await _notificationRepository
            .GetUndismissedAsync(ct)
            .ConfigureAwait(false);

        int unreadCount = undismissed.Count(n => !n.IsRead);

        var items = undismissed
            .OrderBy(n => n.IsRead)
            .ThenBy(n => n.ReferenceDate)
            .ThenBy(n => n.Kind)
            .ThenBy(n => n.Id)
            .Take(NotificationRules.FlyoutCap)
            .Select(NotificationDto.From)
            .ToList();

        return Result.Success(new NotificationListDto(items, unreadCount));
    }
}
