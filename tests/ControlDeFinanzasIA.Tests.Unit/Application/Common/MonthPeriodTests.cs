using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Common;

/// <summary>
/// Tests para <see cref="MonthPeriod"/> (Fase 8, F-008): anchor/range,
/// rollover de mes en ambos sentidos, febrero en año bisiesto y el
/// tamaño de la ventana de <see cref="MonthPeriod.TrailingRange"/>.
/// </summary>
public sealed class MonthPeriodTests
{
    [Fact]
    public void Anchor_AnyDayInMonth_ReturnsFirstDayOfThatMonth()
    {
        DateOnly result = MonthPeriod.Anchor(new DateOnly(2026, 3, 17));

        Assert.Equal(new DateOnly(2026, 3, 1), result);
    }

    [Fact]
    public void Anchor_AlreadyFirstDay_ReturnsSameDate()
    {
        DateOnly result = MonthPeriod.Anchor(new DateOnly(2026, 3, 1));

        Assert.Equal(new DateOnly(2026, 3, 1), result);
    }

    [Fact]
    public void Range_ForMonth_ReturnsFirstToLastDayInclusive()
    {
        DateRange range = MonthPeriod.Range(new DateOnly(2026, 3, 10));

        Assert.Equal(new DateOnly(2026, 3, 1), range.Start);
        Assert.Equal(new DateOnly(2026, 3, 31), range.End);
    }

    [Fact]
    public void Range_FebruaryLeapYear_EndsOnTwentyNine()
    {
        DateRange range = MonthPeriod.Range(new DateOnly(2024, 2, 5));

        Assert.Equal(new DateOnly(2024, 2, 1), range.Start);
        Assert.Equal(new DateOnly(2024, 2, 29), range.End);
    }

    [Fact]
    public void Range_FebruaryNonLeapYear_EndsOnTwentyEight()
    {
        DateRange range = MonthPeriod.Range(new DateOnly(2026, 2, 5));

        Assert.Equal(new DateOnly(2026, 2, 1), range.Start);
        Assert.Equal(new DateOnly(2026, 2, 28), range.End);
    }

    [Fact]
    public void Previous_January_RollsBackToDecemberOfPriorYear()
    {
        DateOnly result = MonthPeriod.Previous(new DateOnly(2026, 1, 15));

        Assert.Equal(new DateOnly(2025, 12, 1), result);
    }

    [Fact]
    public void Previous_MidYearMonth_RollsBackOneMonthSameYear()
    {
        DateOnly result = MonthPeriod.Previous(new DateOnly(2026, 6, 15));

        Assert.Equal(new DateOnly(2026, 5, 1), result);
    }

    [Fact]
    public void Next_December_RollsForwardToJanuaryOfNextYear()
    {
        DateOnly result = MonthPeriod.Next(new DateOnly(2026, 12, 5));

        Assert.Equal(new DateOnly(2027, 1, 1), result);
    }

    [Fact]
    public void Next_MidYearMonth_RollsForwardOneMonthSameYear()
    {
        DateOnly result = MonthPeriod.Next(new DateOnly(2026, 6, 15));

        Assert.Equal(new DateOnly(2026, 7, 1), result);
    }

    [Fact]
    public void TrailingRange_SixMonthsEndingInJune_StartsInJanuarySameYear()
    {
        DateRange range = MonthPeriod.TrailingRange(new DateOnly(2026, 6, 15), 6);

        Assert.Equal(new DateOnly(2026, 1, 1), range.Start);
        Assert.Equal(new DateOnly(2026, 6, 30), range.End);
    }

    [Fact]
    public void TrailingRange_SixMonthsCrossingYearBoundary_StartsInPriorYear()
    {
        DateRange range = MonthPeriod.TrailingRange(new DateOnly(2026, 2, 1), 6);

        Assert.Equal(new DateOnly(2025, 9, 1), range.Start);
        Assert.Equal(new DateOnly(2026, 2, 28), range.End);
    }

    [Fact]
    public void TrailingRange_OneMonth_EqualsMonthRange()
    {
        DateRange range = MonthPeriod.TrailingRange(new DateOnly(2026, 5, 10), 1);

        Assert.Equal(new DateOnly(2026, 5, 1), range.Start);
        Assert.Equal(new DateOnly(2026, 5, 31), range.End);
    }

    [Fact]
    public void TrailingRange_ZeroMonths_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonthPeriod.TrailingRange(new DateOnly(2026, 5, 10), 0));
    }

    [Fact]
    public void TrailingRange_NegativeMonths_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonthPeriod.TrailingRange(new DateOnly(2026, 5, 10), -1));
    }
}
