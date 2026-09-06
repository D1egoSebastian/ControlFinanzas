using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Budgets;

/// <summary>
/// Presupuesto mensual. Un presupuesto está asociado a una
/// categoría (o es global) y tiene un monto y un periodo.
/// </summary>
public sealed record Budget
{
    public Guid Id { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public Guid? CategoryId { get; init; }
    public Money Amount { get; init; } = null!;
    public Currency Currency { get; init; }
    public DateTime CreatedAt { get; init; }
}
