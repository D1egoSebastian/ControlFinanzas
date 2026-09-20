using ControlDeFinanzasIA.Domain.Budgets;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Budgets;

/// <summary>
/// Tests para los limites de umbral de <see cref="BudgetEvaluator"/> (R-6, S-7).
/// </summary>
public sealed class BudgetEvaluatorTests
{
    [Fact]
    public void Evaluate_ZeroSpend_ReturnsZeroPercentAndOk()
    {
        BudgetEvaluation evaluation = BudgetEvaluator.Evaluate(15000m, 0m);

        Assert.Equal(0.00m, evaluation.PercentConsumed);
        Assert.Equal(BudgetStatus.Ok, evaluation.Status);
    }

    [Fact]
    public void Evaluate_JustBelow80Percent_ReturnsOk()
    {
        // 11999.99 / 15000 = 79.99993...% -> rounds to 80.00, so use an exact 79.99% input.
        BudgetEvaluation evaluation = BudgetEvaluator.Evaluate(10000m, 7999m);

        Assert.Equal(79.99m, evaluation.PercentConsumed);
        Assert.Equal(BudgetStatus.Ok, evaluation.Status);
    }

    [Fact]
    public void Evaluate_Exactly80Percent_ReturnsNear()
    {
        BudgetEvaluation evaluation = BudgetEvaluator.Evaluate(15000m, 12000m);

        Assert.Equal(80.00m, evaluation.PercentConsumed);
        Assert.Equal(BudgetStatus.Near, evaluation.Status);
    }

    [Fact]
    public void Evaluate_JustBelow100Percent_ReturnsNear()
    {
        BudgetEvaluation evaluation = BudgetEvaluator.Evaluate(10000m, 9999m);

        Assert.Equal(99.99m, evaluation.PercentConsumed);
        Assert.Equal(BudgetStatus.Near, evaluation.Status);
    }

    [Fact]
    public void Evaluate_Exactly100Percent_ReturnsExceeded()
    {
        BudgetEvaluation evaluation = BudgetEvaluator.Evaluate(15000m, 15000m);

        Assert.Equal(100.00m, evaluation.PercentConsumed);
        Assert.Equal(BudgetStatus.Exceeded, evaluation.Status);
    }

    [Fact]
    public void Evaluate_OverBudget_ReturnsExceededWithUnclampedPercent()
    {
        BudgetEvaluation evaluation = BudgetEvaluator.Evaluate(15000m, 20000m);

        Assert.Equal(133.33m, evaluation.PercentConsumed);
        Assert.Equal(BudgetStatus.Exceeded, evaluation.Status);
    }

    [Fact]
    public void Evaluate_ThresholdAppliesToRoundedPercentNotRawRatio()
    {
        // 79.996% rounds to 80.00 (AwayFromZero at 2 decimals), so the status must be
        // Near, not Ok -- the UI must never show "80.00%" next to an Ok badge.
        BudgetEvaluation evaluation = BudgetEvaluator.Evaluate(100000m, 79996m);

        Assert.Equal(80.00m, evaluation.PercentConsumed);
        Assert.Equal(BudgetStatus.Near, evaluation.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Evaluate_BudgetAmountNotPositive_ThrowsArgumentOutOfRangeException(decimal budgetAmount) => Assert.Throws<ArgumentOutOfRangeException>(() => BudgetEvaluator.Evaluate(budgetAmount, 100m));

    [Fact]
    public void Evaluate_NegativeActualSpend_ThrowsArgumentOutOfRangeException() => Assert.Throws<ArgumentOutOfRangeException>(() => BudgetEvaluator.Evaluate(15000m, -1m));
}
