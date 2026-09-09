namespace ControlDeFinanzasIA.Domain.Recurring;

/// <summary>
/// Suscripción. Una por servicio (Netflix, internet, gimnasio, etc.).
/// Hereda de <see cref="RecurringRule"/>.
/// </summary>
/// <remarks>
/// El record expone un constructor primario con todos los campos
/// del padre para que <see cref="RecurringRule.Create"/> pueda
/// inicializar la instancia tras validar invariantes.
/// </remarks>
public sealed record Subscription(
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
