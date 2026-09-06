using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Debts;

/// <summary>
/// Cuota individual de una deuda. Se modela como entidad separada
/// (no como regla recurrente) porque tiene una cantidad finita y un
/// plan.
/// </summary>
public sealed record DebtPayment
{
    public Guid Id { get; init; }
    public Guid DebtId { get; init; }
    public int InstallmentNumber { get; init; }
    public DateOnly ScheduledDate { get; init; }
    public DateOnly? PaidDate { get; init; }
    public Money Amount { get; init; } = null!;
    public string Status { get; init; } = "Pending";
}
