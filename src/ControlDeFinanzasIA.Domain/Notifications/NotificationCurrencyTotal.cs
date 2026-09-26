using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Domain.Notifications;

/// <summary>
/// Total de pagos pendientes en una moneda, dentro de la ventana del
/// resumen periódico (<see cref="NotificationKind.Digest"/>, F-017, D-1).
/// Se persiste como una lista JSON (<c>DigestTotals</c>), siguiendo el
/// precedente de <c>RecurringRuleConfiguration.TagIds</c>.
/// </summary>
/// <param name="Currency">Moneda del total.</param>
/// <param name="Amount">Suma de los montos pendientes en esa moneda.</param>
public sealed record NotificationCurrencyTotal(Currency Currency, decimal Amount);
