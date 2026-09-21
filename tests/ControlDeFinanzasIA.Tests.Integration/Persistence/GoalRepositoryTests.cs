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
/// Tests de integracion de round-trip para <see cref="GoalRepository"/>
/// contra SQLite real (no in-memory provider), mismo patron que
/// <c>BudgetRepositoryTests</c>.
/// </summary>
public sealed class GoalRepositoryTests
{
    private static Goal CreateGoal(
        string name = "Fondo de emergencia",
        decimal targetAmount = 200000m,
        Currency currency = Currency.DOP,
        DateOnly? targetDate = null,
        string? note = null) =>
        Goal.Create(name, Money.Create(targetAmount, currency), targetDate ?? new DateOnly(2027, 6, 30), note);

    [Fact]
    public async Task AddAsync_ThenGetById_RoundTripsTargetAmountAndCurrencyThroughOwnsOne()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new GoalRepository(context);
        Goal goal = CreateGoal();

        Result addResult = await repository.AddAsync(goal);
        Goal? loaded = await repository.GetByIdAsync(goal.Id);

        Assert.True(addResult.IsSuccess);
        Assert.NotNull(loaded);
        Assert.Equal(200000m, loaded!.TargetAmount.Amount);
        Assert.Equal(Currency.DOP, loaded.TargetAmount.Currency);
        Assert.Equal(Currency.DOP, loaded.Currency);
        Assert.Equal("Fondo de emergencia", loaded.Name);
        Assert.Equal(new DateOnly(2027, 6, 30), loaded.TargetDate);
        Assert.Equal(GoalStatus.Active, loaded.Status);
    }

    [Fact]
    public async Task UpdateAsync_TargetAmountChange_PersistsTheNewAmount()
    {
        // Regression guard for the CurrentValues.SetValues owned-type gap (D-9): a
        // naive DebtRepository-style copy would silently discard this edit. This is
        // the exact bug class the spec's acceptance criteria call out for
        // GoalRepository.UpdateAsync.
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new GoalRepository(context);
        Goal goal = CreateGoal(targetAmount: 200000m);
        await repository.AddAsync(goal);

        // Goal has no public mutator for TargetAmount (v1 has no edit-target-amount UI
        // flow per the spec's Out of Scope section). This clones the goal via its
        // private ctor with the SAME Id and a different TargetAmount, exactly modeling
        // what a persisted-then-edited Goal instance would look like at the repository
        // boundary — the layer this test actually exercises.
        Goal edited = CloneWithTargetAmount(goal, Money.Create(250000m, Currency.DOP));

        Result updateResult = await repository.UpdateAsync(edited);

        await using Infrastructure.Persistence.AppDbContext freshContext = factory.CreateContext();
        var freshRepository = new GoalRepository(freshContext);
        Goal? reloaded = await freshRepository.GetByIdAsync(goal.Id);

        Assert.True(updateResult.IsSuccess);
        Assert.NotNull(reloaded);
        Assert.Equal(250000m, reloaded!.TargetAmount.Amount);
    }

    [Fact]
    public async Task UpdateAsync_StatusTransition_Persists()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new GoalRepository(context);
        Goal goal = CreateGoal(targetAmount: 200000m);
        await repository.AddAsync(goal);

        var contribution = GoalContribution.Create(goal.Id, Money.Create(200000m, Currency.DOP), new DateOnly(2026, 9, 20));
        bool completed = goal.TryAutoComplete([contribution]);
        Result updateResult = await repository.UpdateAsync(goal);

        await using Infrastructure.Persistence.AppDbContext freshContext = factory.CreateContext();
        var freshRepository = new GoalRepository(freshContext);
        Goal? reloaded = await freshRepository.GetByIdAsync(goal.Id);

        Assert.True(completed);
        Assert.True(updateResult.IsSuccess);
        Assert.NotNull(reloaded);
        Assert.Equal(GoalStatus.Completed, reloaded!.Status);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new GoalRepository(context);
        Goal goal = CreateGoal();

        Result updateResult = await repository.UpdateAsync(goal);

        Assert.False(updateResult.IsSuccess);
        Assert.Equal("NotFound", updateResult.Error!.Code);
    }

    [Fact]
    public async Task RemoveAsync_ExistingGoal_DeletesTheRow()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new GoalRepository(context);
        Goal goal = CreateGoal();
        await repository.AddAsync(goal);

        Result removeResult = await repository.RemoveAsync(goal.Id);
        Goal? loaded = await repository.GetByIdAsync(goal.Id);

        Assert.True(removeResult.IsSuccess);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task RemoveAsync_UnknownId_ReturnsNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new GoalRepository(context);

        Result removeResult = await repository.RemoveAsync(Guid.NewGuid());

        Assert.False(removeResult.IsSuccess);
        Assert.Equal("NotFound", removeResult.Error!.Code);
    }

    [Fact]
    public async Task GetAllAsync_OrdersByTargetDateThenName()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new GoalRepository(context);
        Goal later = CreateGoal(name: "Viaje", targetDate: new DateOnly(2028, 1, 1));
        Goal sooner = CreateGoal(name: "Emergencia", targetDate: new DateOnly(2027, 1, 1));
        await repository.AddAsync(later);
        await repository.AddAsync(sooner);

        IReadOnlyList<Goal> results = await repository.GetAllAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(sooner.Id, results[0].Id);
        Assert.Equal(later.Id, results[1].Id);
    }

    private static Goal CloneWithTargetAmount(Goal original, Money newTargetAmount)
    {
        ConstructorInfo ctor = typeof(Goal).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: [typeof(Guid), typeof(string), typeof(Money), typeof(Currency), typeof(DateOnly), typeof(string), typeof(GoalStatus)],
            modifiers: null)!;

        return (Goal)ctor.Invoke(
        [
            original.Id, original.Name, newTargetAmount, newTargetAmount.Currency,
            original.TargetDate, original.Note, original.Status,
        ]);
    }
}
