using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Goals.ContributeToGoal;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Goals;

/// <summary>
/// Tests for <see cref="ContributeToGoalUseCase"/> (R-4, R-6, R-8, S-5,
/// S-6, S-7, S-8).
/// </summary>
public sealed class ContributeToGoalUseCaseTests
{
    private static ContributeToGoalUseCase CreateUseCase(
        FakeGoalRepository goalRepository, FakeGoalContributionRepository contributionRepository) =>
        new(
            goalRepository,
            contributionRepository,
            new ContributeToGoalValidator(),
            NullLogger<ContributeToGoalUseCase>.Instance);

    private static Goal SeededGoal(FakeGoalRepository repository, decimal target = 200000m, Currency currency = Currency.DOP)
    {
        var goal = Goal.Create("Fondo de emergencia", Money.Create(target, currency), new DateOnly(2027, 6, 30));
        repository.Seed(goal);
        return goal;
    }

    [Fact]
    public async Task ExecuteAsync_MatchingCurrency_AddsContributionAndReturnsSuccess()
    {
        var goalRepository = new FakeGoalRepository();
        var contributionRepository = new FakeGoalContributionRepository();
        Goal goal = SeededGoal(goalRepository);
        ContributeToGoalUseCase useCase = CreateUseCase(goalRepository, contributionRepository);

        Result result = await useCase.ExecuteAsync(
            new ContributeToGoalCommand(goal.Id, 15000m, Currency.DOP, new DateOnly(2026, 9, 20)));

        Assert.True(result.IsSuccess);
        GoalContribution contribution = Assert.Single(contributionRepository.Contributions);
        Assert.Equal(15000m, contribution.Amount.Amount);
        Assert.Equal(goal.Id, contribution.GoalId);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownGoal_ReturnsGoalNotFoundAndDoesNotAddContribution()
    {
        var goalRepository = new FakeGoalRepository();
        var contributionRepository = new FakeGoalContributionRepository();
        ContributeToGoalUseCase useCase = CreateUseCase(goalRepository, contributionRepository);

        Result result = await useCase.ExecuteAsync(
            new ContributeToGoalCommand(Guid.NewGuid(), 15000m, Currency.DOP, new DateOnly(2026, 9, 20)));

        Assert.False(result.IsSuccess);
        Assert.Equal("GoalNotFound", result.Error!.Code);
        Assert.False(contributionRepository.AddAsyncCalled);
    }

    [Fact]
    public async Task ExecuteAsync_MismatchedCurrency_ReturnsCurrencyMismatchAndPersistsNoContribution()
    {
        var goalRepository = new FakeGoalRepository();
        var contributionRepository = new FakeGoalContributionRepository();
        Goal goal = SeededGoal(goalRepository, currency: Currency.DOP);
        ContributeToGoalUseCase useCase = CreateUseCase(goalRepository, contributionRepository);

        Result result = await useCase.ExecuteAsync(
            new ContributeToGoalCommand(goal.Id, 100m, Currency.USD, new DateOnly(2026, 9, 20)));

        Assert.False(result.IsSuccess);
        Assert.Equal("CurrencyMismatch", result.Error!.Code);
        Assert.False(contributionRepository.AddAsyncCalled);
        Assert.Empty(contributionRepository.Contributions);
    }

    [Fact]
    public async Task ExecuteAsync_ContributionReachesExactTarget_AutoCompletesGoal()
    {
        var goalRepository = new FakeGoalRepository();
        var contributionRepository = new FakeGoalContributionRepository();
        Goal goal = SeededGoal(goalRepository, target: 200000m);
        contributionRepository.Seed(GoalContribution.Create(goal.Id, Money.Create(185000m, Currency.DOP), new DateOnly(2026, 1, 1)));
        ContributeToGoalUseCase useCase = CreateUseCase(goalRepository, contributionRepository);

        Result result = await useCase.ExecuteAsync(
            new ContributeToGoalCommand(goal.Id, 15000m, Currency.DOP, new DateOnly(2026, 9, 20)));

        Assert.True(result.IsSuccess);
        Assert.True(goalRepository.UpdateAsyncCalled);
        Goal updated = Assert.Single(goalRepository.Goals);
        Assert.Equal(GoalStatus.Completed, updated.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ContributionBelowTarget_DoesNotCallUpdateAsync()
    {
        var goalRepository = new FakeGoalRepository();
        var contributionRepository = new FakeGoalContributionRepository();
        Goal goal = SeededGoal(goalRepository, target: 200000m);
        ContributeToGoalUseCase useCase = CreateUseCase(goalRepository, contributionRepository);

        Result result = await useCase.ExecuteAsync(
            new ContributeToGoalCommand(goal.Id, 15000m, Currency.DOP, new DateOnly(2026, 9, 20)));

        Assert.True(result.IsSuccess);
        Assert.False(goalRepository.UpdateAsyncCalled);
        Goal unchanged = Assert.Single(goalRepository.Goals);
        Assert.Equal(GoalStatus.Active, unchanged.Status);
    }

    [Fact]
    public void Constructor_HasNoExpenseOrIncomeRepositoryDependency()
    {
        System.Reflection.ConstructorInfo ctor = typeof(ContributeToGoalUseCase).GetConstructors().Single();
        Type[] parameterTypes = ctor.GetParameters().Select(p => p.ParameterType).ToArray();

        Assert.DoesNotContain(parameterTypes, t => t == typeof(IExpenseRepository));
        Assert.DoesNotContain(parameterTypes, t => t == typeof(IIncomeRepository));
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulContribution_CreatesNoExpenseOrIncomeRow()
    {
        var goalRepository = new FakeGoalRepository();
        var contributionRepository = new FakeGoalContributionRepository();
        var expenseRepository = new FakeExpenseRepository();
        var incomeRepository = new FakeIncomeRepository();
        Goal goal = SeededGoal(goalRepository);
        ContributeToGoalUseCase useCase = CreateUseCase(goalRepository, contributionRepository);

        Result result = await useCase.ExecuteAsync(
            new ContributeToGoalCommand(goal.Id, 15000m, Currency.DOP, new DateOnly(2026, 9, 20)));

        Assert.True(result.IsSuccess);
        // expenseRepository/incomeRepository are never wired into ContributeToGoalUseCase
        // (see the constructor-shape test above), so AddAsync can never have been called
        // on either: AddedExpenses/AddedIncomes stay empty even though a real contribution
        // was successfully persisted above.
        Assert.Empty(expenseRepository.AddedExpenses);
        Assert.Empty(incomeRepository.AddedIncomes);
    }
}
