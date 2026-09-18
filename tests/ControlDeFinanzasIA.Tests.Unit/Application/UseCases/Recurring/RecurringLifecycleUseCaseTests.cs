using ControlDeFinanzasIA.Application.UseCases.Recurring.DeactivateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeleteFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.SkipNextOccurrence;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Recurring;

public sealed class RecurringLifecycleUseCaseTests
{
    private static FixedExpense CreateRule() => new(Guid.NewGuid(), "Gym", Money.Create(25m, Currency.DOP), Currency.DOP,
        "Monthly", new DateOnly(2026, 1, 31), null, Guid.NewGuid(), Guid.NewGuid(), [], true, new DateOnly(2026, 1, 31));

    [Fact]
    public async Task DeactivateFixedExpense_StopsRule()
    {
        var repository = new FakeRecurringRuleRepository();
        FixedExpense rule = CreateRule(); repository.Seed(rule);
        ControlDeFinanzasIA.Application.Common.Result result = await new DeactivateFixedExpenseUseCase(repository).ExecuteAsync(rule.Id);
        Assert.True(result.IsSuccess);
        Assert.False(rule.Active);
    }

    [Fact]
    public async Task DeleteFixedExpense_RemovesOnlyFixedExpense()
    {
        var repository = new FakeRecurringRuleRepository();
        FixedExpense rule = CreateRule(); repository.Seed(rule);
        ControlDeFinanzasIA.Application.Common.Result result = await new DeleteFixedExpenseUseCase(repository).ExecuteAsync(rule.Id);
        Assert.True(result.IsSuccess);
        Assert.Null(await repository.GetByIdAsync(rule.Id));
    }

    [Fact]
    public async Task SkipNextOccurrence_AdvancesToFollowingMonth()
    {
        var repository = new FakeRecurringRuleRepository();
        FixedExpense rule = CreateRule(); repository.Seed(rule);
        ControlDeFinanzasIA.Application.Common.Result result = await new SkipNextOccurrenceUseCase(repository).ExecuteAsync(rule.Id);
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 2, 28), rule.NextGenerationDate);
    }
}
