using ControlDeFinanzasIA.Domain.Goals;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Goals;

/// <summary>
/// Tests para <see cref="SavingsPlanCalculator"/> (R-7, S-9, S-10, S-11).
/// Fija <c>today = 2026-09-20</c> en todos los casos, siguiendo la tabla
/// de limites de D-4 del diseno. Row 9 (100/3 meses -> 33.34) es la fila
/// que ancla el redondeo por TECHO (ceiling), NO
/// <see cref="MidpointRounding.AwayFromZero"/>: 100/3 = 33.3333...,
/// AwayFromZero redondearia a 33.33 (3 x 33.33 = 99.99, un centavo corto
/// de la meta), mientras que ceiling produce 33.34.
/// </summary>
public sealed class SavingsPlanCalculatorTests
{
    private static readonly DateOnly _today = new(2026, 9, 20);

    [Fact]
    public void Calculate_NormalFutureGoal_ReturnsMonthsAndCeilingRoundedContribution()
    {
        // Proposal's own worked example: 185,000 remaining over 9 months.
        SavingsPlan plan = SavingsPlanCalculator.Calculate(200000m, 15000m, new DateOnly(2027, 6, 30), _today);

        Assert.Equal(9, plan.MonthsRemaining);
        Assert.Equal(20555.56m, plan.MonthlyContributionRequired);
        Assert.False(plan.AlreadyMet);
        Assert.False(plan.TargetDateInPast);
    }

    [Fact]
    public void Calculate_CeilingRoundingPinningCase_100Over3MonthsRoundsUpToNearestCent()
    {
        // 100 / 3 = 33.3333...; AwayFromZero would yield 33.33 (short by a cent over
        // 3 months: 3 x 33.33 = 99.99). Ceiling must yield 33.34 instead.
        SavingsPlan plan = SavingsPlanCalculator.Calculate(100m, 0m, new DateOnly(2026, 12, 20), _today);

        Assert.Equal(3, plan.MonthsRemaining);
        Assert.Equal(33.34m, plan.MonthlyContributionRequired);
        Assert.False(plan.AlreadyMet);
        Assert.False(plan.TargetDateInPast);

        // Prove the ceiling decision materially differs from AwayFromZero for this input.
        decimal awayFromZero = Math.Round(100m / 3, 2, MidpointRounding.AwayFromZero);
        Assert.NotEqual(awayFromZero, plan.MonthlyContributionRequired);
        Assert.Equal(33.33m, awayFromZero);
    }

    [Fact]
    public void Calculate_AlreadyMetExactly_ReturnsAlreadyMetWithoutDividing()
    {
        SavingsPlan plan = SavingsPlanCalculator.Calculate(200000m, 200000m, new DateOnly(2027, 6, 30), _today);

        Assert.True(plan.AlreadyMet);
        Assert.False(plan.TargetDateInPast);
        Assert.Equal(0, plan.MonthsRemaining);
        Assert.Equal(0m, plan.MonthlyContributionRequired);
    }

    [Fact]
    public void Calculate_OverFunded_ReturnsAlreadyMet()
    {
        SavingsPlan plan = SavingsPlanCalculator.Calculate(200000m, 250000m, new DateOnly(2027, 6, 30), _today);

        Assert.True(plan.AlreadyMet);
        Assert.False(plan.TargetDateInPast);
        Assert.Equal(0, plan.MonthsRemaining);
        Assert.Equal(0m, plan.MonthlyContributionRequired);
    }

    [Fact]
    public void Calculate_TargetDateEqualsToday_FloorsMonthsRemainingAtOne()
    {
        SavingsPlan plan = SavingsPlanCalculator.Calculate(200000m, 15000m, _today, _today);

        Assert.False(plan.AlreadyMet);
        Assert.False(plan.TargetDateInPast);
        Assert.Equal(1, plan.MonthsRemaining);
        Assert.Equal(185000.00m, plan.MonthlyContributionRequired);
    }

    [Fact]
    public void Calculate_TargetDateLaterThisMonth_FloorsMonthsRemainingAtOne()
    {
        SavingsPlan plan = SavingsPlanCalculator.Calculate(200000m, 15000m, new DateOnly(2026, 9, 30), _today);

        Assert.False(plan.AlreadyMet);
        Assert.False(plan.TargetDateInPast);
        Assert.Equal(1, plan.MonthsRemaining);
        Assert.Equal(185000.00m, plan.MonthlyContributionRequired);
    }

    [Fact]
    public void Calculate_ZeroCurrentAmountNextMonth_RequiresFullTargetInOneMonth()
    {
        SavingsPlan plan = SavingsPlanCalculator.Calculate(200000m, 0m, new DateOnly(2026, 10, 20), _today);

        Assert.False(plan.AlreadyMet);
        Assert.False(plan.TargetDateInPast);
        Assert.Equal(1, plan.MonthsRemaining);
        Assert.Equal(200000.00m, plan.MonthlyContributionRequired);
    }

    [Fact]
    public void Calculate_TargetDateInThePast_ReturnsTargetDateInPastWithoutDividingByZero()
    {
        SavingsPlan plan = SavingsPlanCalculator.Calculate(200000m, 15000m, new DateOnly(2026, 9, 19), _today);

        Assert.False(plan.AlreadyMet);
        Assert.True(plan.TargetDateInPast);
        Assert.Equal(0, plan.MonthsRemaining);
        Assert.Equal(0m, plan.MonthlyContributionRequired);
    }

    [Fact]
    public void Calculate_BothAlreadyMetAndTargetDateInPast_ReportsBothFlagsTrue()
    {
        // The two flags are independent facts, not a mutually exclusive state machine.
        SavingsPlan plan = SavingsPlanCalculator.Calculate(200000m, 200000m, new DateOnly(2026, 9, 19), _today);

        Assert.True(plan.AlreadyMet);
        Assert.True(plan.TargetDateInPast);
        Assert.Equal(0, plan.MonthsRemaining);
        Assert.Equal(0m, plan.MonthlyContributionRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Calculate_TargetAmountNotPositive_ThrowsArgumentOutOfRangeException(decimal targetAmount) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SavingsPlanCalculator.Calculate(targetAmount, 0m, new DateOnly(2027, 1, 1), _today));

    [Fact]
    public void Calculate_NegativeCurrentAmount_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SavingsPlanCalculator.Calculate(200000m, -1m, new DateOnly(2027, 1, 1), _today));
}
