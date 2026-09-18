namespace ControlDeFinanzasIA.Domain.Debts;

/// <summary>
/// Estado de una <see cref="DebtPayment"/> individual dentro del plan
/// de cuotas de una <see cref="Debt"/>.
/// </summary>
public enum DebtPaymentStatus
{
    /// <summary>La cuota aun no fue pagada ni saltada.</summary>
    Pending = 0,

    /// <summary>La cuota fue pagada en su totalidad (pago parcial fuera de alcance v1).</summary>
    Paid = 1,

    /// <summary>La cuota fue saltada explicitamente por el usuario, sin re-numerar el plan.</summary>
    Skipped = 2,
}
