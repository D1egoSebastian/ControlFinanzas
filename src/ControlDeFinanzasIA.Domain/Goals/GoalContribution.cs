using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Goals;

/// <summary>
/// Aporte a una meta. Cada vez que el usuario transfiere dinero a
/// una meta, se crea un registro de tipo <c>GoalContribution</c>.
/// </summary>
public sealed record GoalContribution
{
    public Guid Id { get; init; }
    public Guid GoalId { get; init; }
    public DateOnly Date { get; init; }
    public Money Amount { get; init; } = null!;
    public string Source { get; init; } = "Manual";
    public DateTime CreatedAt { get; init; }
}
