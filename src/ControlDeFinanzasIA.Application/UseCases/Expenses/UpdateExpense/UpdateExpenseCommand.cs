using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Expenses.UpdateExpense;

/// <summary>
/// Immutable command to update an existing expense. Carries the
/// aggregate identity (<see cref="Id"/>) plus the mutable fields.
/// <see cref="CategoryId"/> is required (<c>Guid</c>, not nullable):
/// an expense always belongs to a category (mirrors
/// <c>RegisterExpenseCommand</c>). <c>Source</c> is not modeled —
/// <c>Expense</c> has no <c>Source</c> property.
/// </summary>
public sealed record UpdateExpenseCommand(
    Guid Id,
    Guid CategoryId,
    DateOnly Date,
    decimal Amount,
    Currency Currency,
    string Description,
    string? Note,
    Guid? PaymentMethodId = null);
