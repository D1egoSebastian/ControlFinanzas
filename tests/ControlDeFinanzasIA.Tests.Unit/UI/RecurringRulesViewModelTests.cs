using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.UI.ViewModels;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

public sealed class RecurringRulesViewModelTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEditableRule_SupportsSubscriptionAndFixedExpense(bool fixedExpense)
    {
        RecurringRule rule = fixedExpense
            ? new FixedExpense(Guid.NewGuid(), "Gym", Money.Create(25m, Currency.DOP), Currency.DOP, "Monthly",
                new DateOnly(2026, 1, 1), null, Guid.NewGuid(), Guid.NewGuid(), [], true, new DateOnly(2026, 1, 1))
            : RecurringRule.Create("Netflix", Money.Create(15m, Currency.DOP), Currency.DOP, "Monthly",
                new DateOnly(2026, 1, 1), null, Guid.NewGuid(), Guid.NewGuid());

        Assert.True(RecurringRulesViewModel.IsEditableRule(rule));
    }
}
