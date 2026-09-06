using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Debts;

/// <summary>
/// Deuda. Modela una obligación financiera con un plan de cuotas
/// finito.
/// </summary>
public sealed record Debt
{
    public Guid Id { get; init; }
    public string Description { get; init; } = string.Empty;
    public Money OriginalAmount { get; init; } = null!;
    public Currency Currency { get; init; }
    public int TotalInstallments { get; init; }
    public Money InstallmentAmount { get; init; } = null!;
    public string Frequency { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public string? CounterpartyName { get; init; }
    public string? Note { get; init; }
    public string Status { get; init; } = "Active";
}
