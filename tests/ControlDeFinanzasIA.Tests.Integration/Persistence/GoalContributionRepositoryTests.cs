using System.Reflection;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integracion de round-trip para <see cref="GoalContributionRepository"/>
/// contra SQLite real (no in-memory provider), mismo patron que
/// <c>BudgetRepositoryTests</c> / <c>GoalRepositoryTests</c>.
/// </summary>
public sealed class GoalContributionRepositoryTests
{
    private static Goal CreateGoal(decimal targetAmount = 200000m, Currency currency = Currency.DOP) =>
        Goal.Create("Fondo de emergencia", Money.Create(targetAmount, currency), new DateOnly(2027, 6, 30));

    private static async Task<Goal> SeedGoalAsync(Infrastructure.Persistence.AppDbContext context, decimal targetAmount = 200000m)
    {
        var goalRepository = new GoalRepository(context);
        Goal goal = CreateGoal(targetAmount);
        await goalRepository.AddAsync(goal);
        return goal;
    }

    [Fact]
    public async Task AddAsync_ThenGetById_RoundTripsAmountAndCurrencyThroughOwnsOne()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        Goal goal = await SeedGoalAsync(context);

        var repository = new GoalContributionRepository(context);
        var contribution = GoalContribution.Create(goal.Id, Money.Create(15000m, Currency.DOP), new DateOnly(2026, 9, 20));

        Result addResult = await repository.AddAsync(contribution);
        GoalContribution? loaded = await repository.GetByIdAsync(contribution.Id);

        Assert.True(addResult.IsSuccess);
        Assert.NotNull(loaded);
        Assert.Equal(15000m, loaded!.Amount.Amount);
        Assert.Equal(Currency.DOP, loaded.Amount.Currency);
        Assert.Equal(goal.Id, loaded.GoalId);
        Assert.Equal(new DateOnly(2026, 9, 20), loaded.Date);
    }

    [Fact]
    public async Task GetByGoalIdAsync_ReturnsOnlyThatGoalsContributionsOrderedByDate()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        Goal goalA = await SeedGoalAsync(context);
        Goal goalB = await SeedGoalAsync(context);

        var repository = new GoalContributionRepository(context);
        var later = GoalContribution.Create(goalA.Id, Money.Create(5000m, Currency.DOP), new DateOnly(2026, 9, 25));
        var earlier = GoalContribution.Create(goalA.Id, Money.Create(10000m, Currency.DOP), new DateOnly(2026, 9, 10));
        var otherGoal = GoalContribution.Create(goalB.Id, Money.Create(1000m, Currency.DOP), new DateOnly(2026, 9, 1));
        await repository.AddAsync(later);
        await repository.AddAsync(earlier);
        await repository.AddAsync(otherGoal);

        IReadOnlyList<GoalContribution> results = await repository.GetByGoalIdAsync(goalA.Id);

        Assert.Equal(2, results.Count);
        Assert.Equal(earlier.Id, results[0].Id);
        Assert.Equal(later.Id, results[1].Id);
        Assert.All(results, contribution => Assert.Equal(goalA.Id, contribution.GoalId));
    }

    [Fact]
    public async Task UpdateAsync_AmountChange_PersistsTheNewAmount()
    {
        // Regression guard for the CurrentValues.SetValues owned-type gap (D-9), the
        // same pattern as GoalRepositoryTests.UpdateAsync_TargetAmountChange. A naive
        // DebtPaymentRepository-style copy would silently discard this edit.
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        Goal goal = await SeedGoalAsync(context);

        var repository = new GoalContributionRepository(context);
        var contribution = GoalContribution.Create(goal.Id, Money.Create(15000m, Currency.DOP), new DateOnly(2026, 9, 20));
        await repository.AddAsync(contribution);

        GoalContribution edited = CloneWithAmount(contribution, Money.Create(9000m, Currency.DOP));
        Result updateResult = await repository.UpdateAsync(edited);

        await using Infrastructure.Persistence.AppDbContext freshContext = factory.CreateContext();
        var freshRepository = new GoalContributionRepository(freshContext);
        GoalContribution? reloaded = await freshRepository.GetByIdAsync(contribution.Id);

        Assert.True(updateResult.IsSuccess);
        Assert.NotNull(reloaded);
        Assert.Equal(9000m, reloaded!.Amount.Amount);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        Goal goal = await SeedGoalAsync(context);

        var repository = new GoalContributionRepository(context);
        var contribution = GoalContribution.Create(goal.Id, Money.Create(15000m, Currency.DOP), new DateOnly(2026, 9, 20));

        Result updateResult = await repository.UpdateAsync(contribution);

        Assert.False(updateResult.IsSuccess);
        Assert.Equal("NotFound", updateResult.Error!.Code);
    }

    [Fact]
    public async Task RemoveAsync_ExistingContribution_DeletesTheRow()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        Goal goal = await SeedGoalAsync(context);

        var repository = new GoalContributionRepository(context);
        var contribution = GoalContribution.Create(goal.Id, Money.Create(15000m, Currency.DOP), new DateOnly(2026, 9, 20));
        await repository.AddAsync(contribution);

        Result removeResult = await repository.RemoveAsync(contribution.Id);
        GoalContribution? loaded = await repository.GetByIdAsync(contribution.Id);

        Assert.True(removeResult.IsSuccess);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeletingGoal_CascadesItsContributionsAway()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        Goal goal = await SeedGoalAsync(context);

        var contributionRepository = new GoalContributionRepository(context);
        var contribution = GoalContribution.Create(goal.Id, Money.Create(15000m, Currency.DOP), new DateOnly(2026, 9, 20));
        await contributionRepository.AddAsync(contribution);

        var goalRepository = new GoalRepository(context);
        await goalRepository.RemoveAsync(goal.Id);

        GoalContribution? loaded = await contributionRepository.GetByIdAsync(contribution.Id);
        IReadOnlyList<GoalContribution> remaining = await contributionRepository.GetByGoalIdAsync(goal.Id);

        Assert.Null(loaded);
        Assert.Empty(remaining);
    }

    private static GoalContribution CloneWithAmount(GoalContribution original, Money newAmount)
    {
        ConstructorInfo ctor = typeof(GoalContribution).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: [typeof(Guid), typeof(Guid), typeof(DateOnly), typeof(Money)],
            modifiers: null)!;

        return (GoalContribution)ctor.Invoke(
            [original.Id, original.GoalId, original.Date, newAmount]);
    }
}
