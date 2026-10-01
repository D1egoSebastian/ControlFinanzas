using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateSubscription;

public sealed record UpdateSubscriptionCommand(
    Guid Id,
    string Name,
    decimal Amount,
    Currency Currency,
    string Frequency,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid CategoryId,
    Guid PaymentMethodId,
    IReadOnlyList<Guid>? TagIds = null,
    DateOnly? NextChargeDate = null);
