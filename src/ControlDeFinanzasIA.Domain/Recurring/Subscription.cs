using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Recurring;

/// <summary>Suscripción recurrente, como Netflix o internet.</summary>
public sealed class Subscription : RecurringRule
{
    private Subscription() { }

    public Subscription(
        Guid id, string name, Money amount, Currency currency, string frequency,
        DateOnly startDate, DateOnly? endDate, Guid categoryId, Guid paymentMethodId,
        IReadOnlyList<Guid> tagIds, bool active, DateOnly nextGenerationDate)
        : base(id, name, amount, currency, frequency, startDate, endDate,
            categoryId, paymentMethodId, tagIds, active, nextGenerationDate)
    {
    }
}
