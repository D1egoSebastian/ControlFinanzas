using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Goals;

/// <summary>
/// Meta de ahorro. Modela un objetivo con monto objetivo, moneda,
/// fecha objetivo y nota opcional.
/// </summary>
public sealed record Goal
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Money TargetAmount { get; init; } = null!;
    public Currency Currency { get; init; }
    public DateOnly TargetDate { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? Note { get; init; }
}
