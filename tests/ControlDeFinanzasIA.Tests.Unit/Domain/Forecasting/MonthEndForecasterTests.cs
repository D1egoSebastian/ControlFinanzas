using ControlDeFinanzasIA.Domain.Forecasting;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Forecasting;

/// <summary>
/// Tests para <see cref="MonthEndForecaster"/> (F-014, R-1, D-2): el
/// blend dia-1, el fallback sin historial, los estados explicitos
/// (<c>InsufficientData</c>/<c>ClosedMonth</c>/<c>FutureMonth</c>), el
/// ultimo dia del mes (<c>remaining = 0</c>) y las guardas de valores
/// negativos.
/// </summary>
public sealed class MonthEndForecasterTests
{
    // April 2026: 30 days. Anchor is the first day of the evaluated month unless a test states
    // otherwise.
    private static readonly DateOnly _aprilAnchor = new(2026, 4, 1);

    [Fact]
    public void Project_Day1WithHistory_BlendsRunRateAndHistoricalAverage()
    {
        // Design D-2 worked example: (1/30)*30000 + (29/30)*1000 = 1966.666... -> projected
        // 87,033.33 (NOT 900,000 -- proves the blend, not a naive run-rate extrapolation).
        MonthEndForecast forecast = MonthEndForecaster.Project(_aprilAnchor, _aprilAnchor, 30000m, 1000m);

        Assert.Equal(ForecastState.Projected, forecast.State);
        Assert.Equal(30000m, forecast.SpentToDateDop);
        Assert.Equal(87033.33m, forecast.ProjectedDop);
        Assert.Equal(1966.67m, forecast.DailyRateDop);
        Assert.Equal(1, forecast.ElapsedDays);
        Assert.Equal(30, forecast.DaysInMonth);
    }

    [Fact]
    public void Project_NoHistoryDay2_ReturnsInsufficientData()
    {
        DateOnly today = new(2026, 4, 2);

        MonthEndForecast forecast = MonthEndForecaster.Project(_aprilAnchor, today, 500m, null);

        Assert.Equal(ForecastState.InsufficientData, forecast.State);
        Assert.Null(forecast.ProjectedDop);
        Assert.Null(forecast.DailyRateDop);
        Assert.Equal(500m, forecast.SpentToDateDop);
        Assert.Equal(2, forecast.ElapsedDays);
        Assert.Equal(30, forecast.DaysInMonth);
    }

    [Fact]
    public void Project_NoHistoryDay3_ReturnsPureRunRate()
    {
        DateOnly today = new(2026, 4, 3);

        MonthEndForecast forecast = MonthEndForecaster.Project(_aprilAnchor, today, 900m, null);

        // runRate = 900 / 3 = 300; remaining = 27; projected = 900 + 27*300 = 9000.
        Assert.Equal(ForecastState.Projected, forecast.State);
        Assert.Equal(9000.00m, forecast.ProjectedDop);
        Assert.Equal(300.00m, forecast.DailyRateDop);
        Assert.Equal(3, forecast.ElapsedDays);
    }

    [Fact]
    public void Project_TodayAfterMonthEnd_ReturnsClosedMonthWithProjectedEqualToSpent()
    {
        DateOnly today = new(2026, 5, 15);

        MonthEndForecast forecast = MonthEndForecaster.Project(_aprilAnchor, today, 45000.75m, null);

        Assert.Equal(ForecastState.ClosedMonth, forecast.State);
        Assert.Equal(45000.75m, forecast.SpentToDateDop);
        Assert.Equal(45000.75m, forecast.ProjectedDop);
        Assert.Null(forecast.DailyRateDop);
        Assert.Equal(30, forecast.ElapsedDays);
        Assert.Equal(30, forecast.DaysInMonth);
    }

    [Fact]
    public void Project_TodayBeforeMonthStart_ReturnsFutureMonthWithNullProjected()
    {
        DateOnly juneAnchor = new(2026, 6, 1);
        DateOnly today = new(2026, 4, 15);

        MonthEndForecast forecast = MonthEndForecaster.Project(juneAnchor, today, 0m, null);

        Assert.Equal(ForecastState.FutureMonth, forecast.State);
        Assert.Equal(0m, forecast.SpentToDateDop);
        Assert.Null(forecast.ProjectedDop);
        Assert.Null(forecast.DailyRateDop);
        Assert.Equal(0, forecast.ElapsedDays);
        Assert.Equal(30, forecast.DaysInMonth);
    }

    [Fact]
    public void Project_LastDayOfMonth_RemainingZero_ProjectedEqualsSpent()
    {
        DateOnly today = new(2026, 4, 30);

        MonthEndForecast forecast = MonthEndForecaster.Project(_aprilAnchor, today, 15000m, null);

        // remaining = 30 - 30 = 0, so projected == spent regardless of the computed rate --
        // no special-cased branch needed in the implementation (design D-2).
        Assert.Equal(ForecastState.Projected, forecast.State);
        Assert.Equal(15000.00m, forecast.ProjectedDop);
        Assert.Equal(500.00m, forecast.DailyRateDop);
        Assert.Equal(30, forecast.ElapsedDays);
        Assert.Equal(30, forecast.DaysInMonth);
    }

    [Fact]
    public void Project_AnchorNotFirstDayOfMonth_StillTreatsSameYearMonthAsCurrent()
    {
        // The anchor's own day-of-month must not matter: only (Year, Month) identifies "the
        // evaluated month". A day-15 anchor with a day-3 "today" in the SAME month must still be
        // the current month, not FutureMonth.
        DateOnly anchor = new(2026, 4, 15);
        DateOnly today = new(2026, 4, 3);

        MonthEndForecast forecast = MonthEndForecaster.Project(anchor, today, 900m, null);

        Assert.Equal(ForecastState.Projected, forecast.State);
        Assert.Equal(9000.00m, forecast.ProjectedDop);
    }

    [Fact]
    public void Project_NegativeSpentToDate_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MonthEndForecaster.Project(_aprilAnchor, _aprilAnchor, -1m, null));

    [Fact]
    public void Project_NegativeHistoricalAverage_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MonthEndForecaster.Project(_aprilAnchor, _aprilAnchor, 0m, -1m));
}
