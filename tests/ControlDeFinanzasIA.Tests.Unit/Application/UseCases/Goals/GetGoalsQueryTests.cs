using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Goals.GetGoals;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Goals;

/// <summary>
/// Tests for <see cref="GetGoalsQuery"/> (R-5, D-7).
/// </summary>
public sealed class GetGoalsQueryTests
{
    private static readonly DateOnly _today = new(2026, 9, 20);

    private static GetGoalsQuery CreateQuery(
        FakeGoalRepository goals, FakeGoalContributionRepository contributions, FakeClock? clock = null)
        => new(goals, contributions, clock ?? new FakeClock { Today = _today });

    [Fact]
    public async Task ExecuteAsync_EmptyRepository_ReturnsEmptyListWithoutQueryingContributions()
    {
        var goals = new FakeGoalRepository();
        var contributions = new FakeGoalContributionRepository();
        GetGoalsQuery query = CreateQuery(goals, contributions);

        Result result = await query.ExecuteAsync();

        Assert.True(result.IsSuccess);
        IReadOnlyList<GoalPlanItem> items = Assert.IsAssignableFrom<IReadOnlyList<GoalPlanItem>>(result.Value);
        Assert.Empty(items);
        Assert.Equal(0, contributions.GetByGoalIdCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_GoalWithContributions_ComposesProgressAndPlanFromDomain()
    {
        var goals = new FakeGoalRepository();
        var contributions = new FakeGoalContributionRepository();
        var goal = Goal.Create("Fondo de emergencia", Money.Create(200000m, Currency.DOP), new DateOnly(2027, 6, 30));
        goals.Seed(goal);
        var contribution = GoalContribution.Create(goal.Id, Money.Create(15000m, Currency.DOP), new DateOnly(2026, 1, 1));
        contributions.Seed(contribution);
        GetGoalsQuery query = CreateQuery(goals, contributions);

        Result result = await query.ExecuteAsync();

        IReadOnlyList<GoalPlanItem> items = Assert.IsAssignableFrom<IReadOnlyList<GoalPlanItem>>(result.Value);
        GoalPlanItem item = Assert.Single(items);

        // Cross-check against the domain's own arithmetic (D-7: single arithmetic path).
        GoalProgress expectedProgress = goal.CalculateProgress(new[] { contribution });
        SavingsPlan expectedPlan = SavingsPlanCalculator.Calculate(
            goal.TargetAmount.Amount, expectedProgress.Contributed.Amount, goal.TargetDate, _today);

        Assert.Equal(expectedProgress.Contributed.Amount, item.ContributedAmount);
        Assert.Equal(expectedProgress.PercentComplete, item.PercentComplete);
        Assert.Equal(expectedPlan.MonthsRemaining, item.MonthsRemaining);
        Assert.Equal(expectedPlan.MonthlyContributionRequired, item.MonthlyContributionRequired);
        Assert.Equal(1, contributions.GetByGoalIdCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_GoalWithNoContributions_ReportsZeroProgressWithoutCrashing()
    {
        var goals = new FakeGoalRepository();
        var contributions = new FakeGoalContributionRepository();
        var goal = Goal.Create("Vacaciones", Money.Create(50000m, Currency.DOP), new DateOnly(2027, 1, 1));
        goals.Seed(goal);
        GetGoalsQuery query = CreateQuery(goals, contributions);

        Result result = await query.ExecuteAsync();

        IReadOnlyList<GoalPlanItem> items = Assert.IsAssignableFrom<IReadOnlyList<GoalPlanItem>>(result.Value);
        GoalPlanItem item = Assert.Single(items);
        Assert.Equal(0m, item.ContributedAmount);
        Assert.Equal(0m, item.PercentComplete);
        Assert.False(item.AlreadyMet);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleGoals_OrdersByStatusThenTargetDateThenOrdinalName()
    {
        var goals = new FakeGoalRepository();
        var contributions = new FakeGoalContributionRepository();

        var completed = Goal.Create("Zapatos", Money.Create(1000m, Currency.DOP), new DateOnly(2026, 1, 1));
        var fullContribution = GoalContribution.Create(completed.Id, Money.Create(1000m, Currency.DOP), new DateOnly(2026, 1, 1));
        contributions.Seed(fullContribution);
        completed.TryAutoComplete(new[] { fullContribution });

        var activeLater = Goal.Create("Bicicleta", Money.Create(5000m, Currency.DOP), new DateOnly(2027, 3, 1));
        var activeSooner = Goal.Create("Auto", Money.Create(50000m, Currency.DOP), new DateOnly(2027, 1, 1));

        goals.Seed(completed);
        goals.Seed(activeLater);
        goals.Seed(activeSooner);

        GetGoalsQuery query = CreateQuery(goals, contributions);

        Result result = await query.ExecuteAsync();

        IReadOnlyList<GoalPlanItem> items = Assert.IsAssignableFrom<IReadOnlyList<GoalPlanItem>>(result.Value);
        Assert.Equal(3, items.Count);
        Assert.Equal("Auto", items[0].Name);       // Active, soonest target date
        Assert.Equal("Bicicleta", items[1].Name);  // Active, later target date
        Assert.Equal("Zapatos", items[2].Name);    // Completed, sorts last
    }
}
