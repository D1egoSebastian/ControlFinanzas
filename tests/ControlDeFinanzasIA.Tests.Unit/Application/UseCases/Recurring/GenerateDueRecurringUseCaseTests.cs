using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Recurring.GenerateDueRecurring;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Recurring;

public sealed class GenerateDueRecurringUseCaseTests
{
    [Fact]
    public async Task PreviewAndConfirm_CreateProvenanceLinkedExpenseOnce()
    {
        Subscription rule = RecurringRule.Create("Rent", Money.Create(800m, Currency.DOP), Currency.DOP,
            "Monthly", new DateOnly(2026, 1, 31), null, Guid.NewGuid(), Guid.NewGuid());
        var rules = new FakeRecurringRuleRepository();
        rules.Seed(rule);
        var expenses = new FakeExpenseRepository();
        var clock = new FakeClock { Today = new DateOnly(2026, 2, 10), UtcNow = new DateTime(2026, 2, 10, 12, 0, 0, DateTimeKind.Utc) };
        var useCase = new GenerateDueRecurringUseCase(rules, expenses, clock, NullLogger<GenerateDueRecurringUseCase>.Instance);

        Result preview = await useCase.PreviewAsync(new DateOnly(2026, 2, 1));
        Result first = await useCase.ExecuteAsync(new DateOnly(2026, 2, 1));
        Result second = await useCase.ExecuteAsync(new DateOnly(2026, 2, 1));

        IReadOnlyList<RecurringGenerationPreview> items = Assert.IsAssignableFrom<IReadOnlyList<RecurringGenerationPreview>>(preview.Value);
        Assert.Single(items);
        Assert.Equal(new DateOnly(2026, 2, 28), items[0].OccurrenceDate);
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Single(expenses.AddedExpenses);
        Assert.Equal(rule.Id, expenses.AddedExpenses[0].RecurringRuleId);
        Assert.Equal(new DateOnly(2026, 2, 1), expenses.AddedExpenses[0].RecurringPeriod);
    }

    [Fact]
    public async Task Preview_ExcludesInactiveAndEndedRules()
    {
        Subscription inactive = RecurringRule.Create("Paused", Money.Create(10m, Currency.DOP), Currency.DOP,
            "Monthly", new DateOnly(2026, 1, 1), null, Guid.NewGuid(), Guid.NewGuid());
        inactive.Deactivate();
        Subscription ended = RecurringRule.Create("Ended", Money.Create(10m, Currency.DOP), Currency.DOP,
            "Monthly", new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid(), Guid.NewGuid());
        var rules = new FakeRecurringRuleRepository();
        rules.Seed(inactive);
        rules.Seed(ended);
        var useCase = new GenerateDueRecurringUseCase(rules, new FakeExpenseRepository(),
            new FakeClock { Today = new DateOnly(2026, 2, 5) }, NullLogger<GenerateDueRecurringUseCase>.Instance);

        Result result = await useCase.PreviewAsync(new DateOnly(2026, 2, 1));

        Assert.True(result.IsSuccess);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<RecurringGenerationPreview>>(result.Value));
    }
}
