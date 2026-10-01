using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateSubscription;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateSubscription;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Recurring;

/// <summary>
/// Pre-1.0 slice C: the real create and update use cases carry the first-charge date to the stored rule, and an
/// update that does not send one never moves the schedule.
/// </summary>
public sealed class RecurringNextChargeUseCaseTests
{
    private static readonly Guid _category = Guid.NewGuid();
    private static readonly Guid _method = Guid.NewGuid();

    [Fact]
    public async Task CreateSubscription_WithAFirstChargeDate_StoresItAsTheCursor()
    {
        var repo = new FakeRecurringRuleRepository();
        var useCase = new CreateSubscriptionUseCase(repo, new CreateSubscriptionValidator(), NullLogger<CreateSubscriptionUseCase>.Instance);

        ControlDeFinanzasIA.Application.Common.Result result = await useCase.ExecuteAsync(new CreateSubscriptionCommand(
            "Rent", 900m, Currency.DOP, "Monthly", new DateOnly(2026, 10, 31), null, _category, _method, null, new DateOnly(2026, 11, 30)));

        Assert.True(result.IsSuccess);
        RecurringRule stored = Assert.Single(repo.Added);
        Assert.Equal(new DateOnly(2026, 10, 31), stored.StartDate);
        Assert.Equal(new DateOnly(2026, 11, 30), stored.NextGenerationDate);
    }

    [Fact]
    public async Task CreateFixedExpense_WithAFirstChargeDate_StoresItAsTheCursor()
    {
        var repo = new FakeRecurringRuleRepository();
        var useCase = new CreateFixedExpenseUseCase(repo, new CreateFixedExpenseValidator(), NullLogger<CreateFixedExpenseUseCase>.Instance);

        ControlDeFinanzasIA.Application.Common.Result result = await useCase.ExecuteAsync(new CreateFixedExpenseCommand(
            "Rent", 900m, Currency.DOP, "Monthly", new DateOnly(2026, 10, 31), null, _category, _method, null, new DateOnly(2026, 11, 30)));

        Assert.True(result.IsSuccess);
        RecurringRule stored = Assert.Single(repo.Added);
        Assert.IsType<FixedExpense>(stored);
        Assert.Equal(new DateOnly(2026, 11, 30), stored.NextGenerationDate);
    }

    [Fact]
    public async Task CreateSubscription_WithAFirstChargeBeforeTheStart_IsRejected()
    {
        var repo = new FakeRecurringRuleRepository();
        var useCase = new CreateSubscriptionUseCase(repo, new CreateSubscriptionValidator(), NullLogger<CreateSubscriptionUseCase>.Instance);

        ControlDeFinanzasIA.Application.Common.Result result = await useCase.ExecuteAsync(new CreateSubscriptionCommand(
            "Rent", 900m, Currency.DOP, "Monthly", new DateOnly(2026, 10, 31), null, _category, _method, null, new DateOnly(2026, 10, 1)));

        Assert.False(result.IsSuccess);
        Assert.Empty(repo.Added);
    }

    [Fact]
    public async Task UpdateSubscription_WithoutAFirstChargeDate_KeepsTheScheduleOfAnAlreadyAdvancedRule()
    {
        var repo = new FakeRecurringRuleRepository();
        Subscription rule = RecurringRule.Create("Gym", Money.Create(30m, Currency.DOP), Currency.DOP, "Monthly", new DateOnly(2026, 8, 25), null, _category, _method);
        rule.AdvanceAfterGeneration(new DateOnly(2026, 8, 25), 1);
        repo.Seed(rule);
        var useCase = new UpdateSubscriptionUseCase(repo, new UpdateSubscriptionValidator(), NullLogger<UpdateSubscriptionUseCase>.Instance);

        ControlDeFinanzasIA.Application.Common.Result result = await useCase.ExecuteAsync(new UpdateSubscriptionCommand(
            rule.Id, "Gym Pro", 35m, Currency.DOP, "Monthly", rule.StartDate, null, _category, _method));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 8, 25), rule.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 25), rule.NextGenerationDate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_WithAFirstChargeDate_MovesTheCursorToIt_EvenBackwards(bool fixedExpense)
    {
        var repo = new FakeRecurringRuleRepository();
        Subscription seed = RecurringRule.Create("Gym", Money.Create(30m, Currency.DOP), Currency.DOP, "Monthly", new DateOnly(2026, 8, 25), null, _category, _method);
        seed.AdvanceAfterGeneration(new DateOnly(2026, 8, 25), 1);
        seed.AdvanceAfterGeneration(new DateOnly(2026, 9, 25), 1);
        RecurringRule rule = fixedExpense
            ? new FixedExpense(seed.Id, seed.Name, seed.Amount, seed.Currency, seed.Frequency, seed.StartDate, null, _category, _method, [], true, seed.NextGenerationDate)
            : seed;
        repo.Seed(rule);
        var newStart = new DateOnly(2026, 9, 15);

        ControlDeFinanzasIA.Application.Common.Result result = fixedExpense
            ? await new UpdateFixedExpenseUseCase(repo, new UpdateFixedExpenseValidator(), NullLogger<UpdateFixedExpenseUseCase>.Instance)
                .ExecuteAsync(new UpdateFixedExpenseCommand(rule.Id, "Gym", 30m, Currency.DOP, "Monthly", newStart, null, _category, _method, null, newStart))
            : await new UpdateSubscriptionUseCase(repo, new UpdateSubscriptionValidator(), NullLogger<UpdateSubscriptionUseCase>.Instance)
                .ExecuteAsync(new UpdateSubscriptionCommand(rule.Id, "Gym", 30m, Currency.DOP, "Monthly", newStart, null, _category, _method, null, newStart));

        Assert.True(result.IsSuccess);
        Assert.Equal(newStart, rule.StartDate);
        Assert.Equal(newStart, rule.NextGenerationDate);
    }
}
