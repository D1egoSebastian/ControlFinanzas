using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;

namespace ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;

/// <summary>
/// Vista de solo lectura de un <see cref="Notification"/> para la UI (F-017,
/// D-6): replica cada columna de hechos más <see cref="Id"/> e
/// <see cref="IsRead"/>. No contiene texto de usuario: la UI lo formatea al
/// momento de mostrar (R-11).
/// </summary>
/// <param name="Id">Identificador del aviso.</param>
/// <param name="Kind">Tipo de aviso.</param>
/// <param name="SourceId">Id de la fuente (cuota, regla, presupuesto o alerta); <see langword="null"/> para un resumen.</param>
/// <param name="ReferenceDate">Fecha de referencia (vencimiento, inicio de mes/periodo o fecha de la alerta).</param>
/// <param name="SubjectLabel">Etiqueta saneada del sujeto; puede ser vacía.</param>
/// <param name="Amount">Monto, solo avisos de vencimiento.</param>
/// <param name="Currency">Moneda del monto, solo avisos de vencimiento.</param>
/// <param name="Percent">Porcentaje consumido, solo avisos de presupuesto.</param>
/// <param name="Tier">Nivel alcanzado, solo avisos de presupuesto.</param>
/// <param name="DigestFrequency">Frecuencia del resumen, solo resúmenes.</param>
/// <param name="DigestPaymentCount">Cantidad de pagos del resumen, solo resúmenes.</param>
/// <param name="DigestBudgetCount">Cantidad de presupuestos en Near o Exceeded, solo resúmenes.</param>
/// <param name="DigestAnomalyCount">Cantidad de anomalías sin reconocer, solo resúmenes.</param>
/// <param name="DigestTotals">Totales por moneda del resumen; vacío para los demás tipos.</param>
/// <param name="IsRead">Indica si el aviso ya fue marcado como leído.</param>
public sealed record NotificationDto(
    Guid Id,
    NotificationKind Kind,
    Guid? SourceId,
    DateOnly ReferenceDate,
    string SubjectLabel,
    decimal? Amount,
    Currency? Currency,
    decimal? Percent,
    NotificationTier? Tier,
    DigestFrequency? DigestFrequency,
    int? DigestPaymentCount,
    int? DigestBudgetCount,
    int? DigestAnomalyCount,
    IReadOnlyList<NotificationCurrencyTotal> DigestTotals,
    bool IsRead)
{
    /// <summary>Proyecta un <see cref="Notification"/> a su DTO.</summary>
    /// <param name="notification">Aviso a proyectar.</param>
    public static NotificationDto From(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return new NotificationDto(
            notification.Id,
            notification.Kind,
            notification.SourceId,
            notification.ReferenceDate,
            notification.SubjectLabel,
            notification.Amount,
            notification.Currency,
            notification.Percent,
            notification.Tier,
            notification.DigestFrequency,
            notification.DigestPaymentCount,
            notification.DigestBudgetCount,
            notification.DigestAnomalyCount,
            notification.DigestTotals,
            notification.IsRead);
    }
}

/// <summary>
/// Lista de avisos para el flyout (F-017, R-10, D-6).
/// </summary>
/// <param name="Items">
/// Como máximo <c>NotificationRules.FlyoutCap</c> avisos no descartados,
/// no leídos primero, luego por fecha de referencia ascendente.
/// </param>
/// <param name="UnreadCount">
/// Cantidad total de avisos no descartados y no leídos, sin importar el tope
/// de <paramref name="Items"/>.
/// </param>
public sealed record NotificationListDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);
