namespace ControlDeFinanzasIA.Domain.Debts;

/// <summary>
/// Estado de una <see cref="Debt"/>. Una deuda nace <see cref="Active"/>
/// y pasa a <see cref="Closed"/> automaticamente cuando se paga su
/// ultima cuota pendiente (R-6).
/// </summary>
public enum DebtStatus
{
    /// <summary>La deuda tiene cuotas pendientes de pago.</summary>
    Active = 0,

    /// <summary>La deuda fue cerrada automaticamente al pagar la ultima cuota.</summary>
    Closed = 1,
}
