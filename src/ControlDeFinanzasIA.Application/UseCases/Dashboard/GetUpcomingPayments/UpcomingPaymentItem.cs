using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;

/// <summary>
/// Distingue el origen de un <see cref="UpcomingPaymentItem"/>: una cuota
/// de deuda pendiente o la pr&#243;xima generaci&#243;n de una regla recurrente (R-6).
/// </summary>
public enum UpcomingPaymentKind
{
    /// <summary>El elemento proviene de una <see cref="Domain.Debts.DebtPayment"/> pendiente.</summary>
    DebtInstallment,

    /// <summary>El elemento proviene de la <c>NextGenerationDate</c> de una <see cref="Domain.Recurring.RecurringRule"/> activa.</summary>
    RecurringRule,
}

/// <summary>
/// Un elemento individual de la tarjeta "Pr&#243;ximos pagos" del Dashboard
/// (R-6): combina cuotas de deuda pendientes y pr&#243;ximas generaciones de
/// reglas recurrentes dentro del horizonte configurado
/// (<see cref="Common.DashboardDefaults.UpcomingHorizonDays"/>).
/// </summary>
/// <param name="Kind">Origen del elemento.</param>
/// <param name="Label">
/// Etiqueta a mostrar: la <c>Description</c> de la <see cref="Domain.Debts.Debt"/>
/// due&#241;a de la cuota, o el <c>Name</c> de la regla recurrente.
/// </param>
/// <param name="Date">
/// Fecha en que el pago vence: <c>ScheduledDate</c> de la cuota, o
/// <c>NextGenerationDate</c> de la regla.
/// </param>
/// <param name="Amount">
/// Monto del pago en su moneda original. NO se normaliza a DOP: esta
/// tarjeta es una lista de &#237;tems individuales, no un agregado, y usa el
/// mismo patr&#243;n "&#8776;" por-&#237;tem existente en el resto de la app.
/// </param>
public sealed record UpcomingPaymentItem(
    UpcomingPaymentKind Kind,
    string Label,
    DateOnly Date,
    Money Amount);
