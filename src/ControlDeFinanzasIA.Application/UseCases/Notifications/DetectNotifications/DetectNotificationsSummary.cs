namespace ControlDeFinanzasIA.Application.UseCases.Notifications.DetectNotifications;

/// <summary>
/// Resumen del resultado de una corrida de <see cref="DetectNotificationsUseCase"/>
/// (F-017, D-5): cuántas filas se agregaron, refrescaron o eliminaron al
/// aplicar el <c>NotificationReconciliationPlan</c>.
/// </summary>
/// <param name="Added">Cantidad de avisos nuevos insertados.</param>
/// <param name="Updated">Cantidad de avisos existentes refrescados.</param>
/// <param name="Removed">Cantidad de avisos eliminados (purgados o ya no calificados).</param>
public sealed record DetectNotificationsSummary(int Added, int Updated, int Removed);
