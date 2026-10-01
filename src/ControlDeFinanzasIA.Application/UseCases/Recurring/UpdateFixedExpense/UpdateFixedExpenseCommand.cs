using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateFixedExpense;

public sealed record UpdateFixedExpenseCommand(
    Guid Id, string Name, decimal Amount, Currency Currency, string Frequency,
    DateOnly StartDate, DateOnly? EndDate, Guid CategoryId, Guid PaymentMethodId,
    IReadOnlyList<Guid>? TagIds = null,
    DateOnly? NextChargeDate = null);
