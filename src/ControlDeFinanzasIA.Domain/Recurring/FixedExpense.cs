namespace ControlDeFinanzasIA.Domain.Recurring;

/// <summary>
/// Gasto fijo recurrente (alquiler, internet, gimnasio, etc.).
/// Hereda de <see cref="RecurringRule"/>.
/// </summary>
public sealed record FixedExpense(
    Guid Id,
    string Name,
    Domain.ValueObjects.Money Amount,
    Domain.Enums.Currency Currency,
    string Frequency,
    System.DateOnly StartDate,
    System.DateOnly? EndDate,
    Guid CategoryId,
    Guid PaymentMethodId,
    System.Collections.Generic.IReadOnlyList<Guid> TagIds,
    bool Active,
    System.DateOnly NextGenerationDate) : RecurringRule(Id, Name, Amount, Currency, Frequency, StartDate, EndDate, CategoryId, PaymentMethodId, TagIds, Active, NextGenerationDate);
