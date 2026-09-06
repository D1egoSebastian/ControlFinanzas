using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Recurring;

/// <summary>
/// Base abstracta para reglas recurrentes. Modela suscripciones
/// (un servicio específico) y gastos fijos (alquiler, internet,
/// etc.).
/// </summary>
public abstract record RecurringRule
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Money Amount { get; init; } = null!;
    public Currency Currency { get; init; }
    public string Frequency { get; init; } = "Monthly";
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public Guid CategoryId { get; init; }
    public Guid PaymentMethodId { get; init; }
    public IReadOnlyList<Guid> TagIds { get; init; } = Array.Empty<Guid>();
    public bool Active { get; init; } = true;
    public DateOnly NextGenerationDate { get; init; }
}
