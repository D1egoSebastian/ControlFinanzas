using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Recurring;

/// <summary>Gasto fijo recurrente, como alquiler o gimnasio.</summary>
public sealed class FixedExpense : RecurringRule
{
    private FixedExpense() { }

    public FixedExpense(
        Guid id, string name, Money amount, Currency currency, string frequency,
        DateOnly startDate, DateOnly? endDate, Guid categoryId, Guid paymentMethodId,
        IReadOnlyList<Guid> tagIds, bool active, DateOnly nextGenerationDate)
        : base(id, name, amount, currency, frequency, startDate, endDate,
            categoryId, paymentMethodId, tagIds, active, nextGenerationDate)
    {
    }
}
