using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Incomes.UpdateIncome;

/// <summary>
/// Immutable command to update an existing income. Carries the
/// aggregate identity (<see cref="Id"/>) plus the mutable fields.
/// <see cref="CategoryId"/> may be null (an income without a category
/// is valid — mirrors <c>RegisterIncomeCommand</c>). <c>Source</c> is
/// not modeled here: <c>UpdateIncomeUseCase</c> resets it to null
/// (setting the source is deferred — see spec §7).
/// </summary>
public sealed record UpdateIncomeCommand(
    Guid Id,
    Guid? CategoryId,
    DateOnly Date,
    decimal Amount,
    Currency Currency,
    string Description,
    string? Note,
    Guid? PaymentMethodId = null);
