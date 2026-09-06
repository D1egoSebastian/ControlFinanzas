namespace ControlDeFinanzasIA.Domain.Recurring;

/// <summary>
/// Gasto fijo recurrente (alquiler, internet, gimnasio, etc.).
/// Hereda de <see cref="RecurringRule"/>.
/// </summary>
public sealed record FixedExpense : RecurringRule
{
}
