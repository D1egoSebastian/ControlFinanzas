using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.CreateFixedExpense;

public sealed record CreateFixedExpenseCommand(
    string Name, decimal Amount, Currency Currency, string Frequency,
    DateOnly StartDate, DateOnly? EndDate, Guid CategoryId, Guid PaymentMethodId,
    IReadOnlyList<Guid>? TagIds = null);
