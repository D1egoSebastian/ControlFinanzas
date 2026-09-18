using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Calendar.GetCalendarMonth;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Calendar;

/// <summary>
/// Unit tests for <see cref="GetCalendarMonthQuery"/> (F-009, slice 1).
/// Covers S-1 (multi-currency day collapses to a single DOP total), D-3
/// (zero vs. null <see cref="CalendarDay.TotalDop"/>), FX conversion
/// failure handling (no throw, <see cref="CalendarMonthResult.HasMissingRates"/>),
/// and <see cref="CalendarDay.IsToday"/>.
/// </summary>
public sealed class GetCalendarMonthQueryTests
{
    // 2026-03-01 es un domingo (verificado con `date -d 2026-03-01 +%A`).
    private static readonly DateOnly _march = new(2026, 3, 1);

    [Fact]
    public async Task ExecuteAsync_MultiCurrencyDay_CollapsesToSingleDopTotal()
    {
        FakeExpenseRepository repo = new();
        await repo.AddAsync(CreateExpense(new DateOnly(2026, 3, 10), 100m, Currency.DOP));
        await repo.AddAsync(CreateExpense(new DateOnly(2026, 3, 10), 50m, Currency.USD));

        GetCalendarMonthQuery query = new(repo, new FakeCurrencyConverter(), new FakeClock());

        Result result = await query.ExecuteAsync(_march, DayOfWeek.Sunday);

        CalendarMonthResult calendar = Assert.IsType<CalendarMonthResult>(result.Value);
        CalendarDay day = calendar.Days.Single(d => d.Date == new DateOnly(2026, 3, 10));
        // FakeCurrencyConverter sin onConvert configurado convierte 1:1.
        Assert.Equal(150m, day.TotalDop);
    }

    [Fact]
    public async Task ExecuteAsync_CurrentMonthDayWithNoExpenses_ReturnsZeroTotal()
    {
        FakeExpenseRepository repo = new();
        GetCalendarMonthQuery query = new(repo, new FakeCurrencyConverter(), new FakeClock());

        Result result = await query.ExecuteAsync(_march, DayOfWeek.Sunday);

        CalendarMonthResult calendar = Assert.IsType<CalendarMonthResult>(result.Value);
        CalendarDay day = calendar.Days.Single(d => d.Date == new DateOnly(2026, 3, 15));
        Assert.True(day.IsCurrentMonth);
        Assert.Equal(0m, day.TotalDop);
    }

    [Fact]
    public async Task ExecuteAsync_AdjacentMonthDay_ReturnsNullTotal()
    {
        FakeExpenseRepository repo = new();
        GetCalendarMonthQuery query = new(repo, new FakeCurrencyConverter(), new FakeClock());

        // Marzo 2026 arranca en domingo; con firstDayOfWeek = lunes la
        // primera celda de la grilla cae en febrero (mes adyacente).
        Result result = await query.ExecuteAsync(_march, DayOfWeek.Monday);

        CalendarMonthResult calendar = Assert.IsType<CalendarMonthResult>(result.Value);
        CalendarDay leading = calendar.Days[0];
        Assert.False(leading.IsCurrentMonth);
        Assert.Null(leading.TotalDop);
    }

    [Fact]
    public async Task ExecuteAsync_FxConversionFails_SetsHasMissingRatesWithoutThrowing()
    {
        FakeExpenseRepository repo = new();
        await repo.AddAsync(CreateExpense(new DateOnly(2026, 3, 10), 100m, Currency.USD));

        FakeCurrencyConverter converter = new(onConvert: (_, _) =>
            Result.Failure("ExchangeRateUnavailable", "Provider unavailable."));
        GetCalendarMonthQuery query = new(repo, converter, new FakeClock());

        Result result = await query.ExecuteAsync(_march, DayOfWeek.Sunday);

        Assert.True(result.IsSuccess);
        CalendarMonthResult calendar = Assert.IsType<CalendarMonthResult>(result.Value);
        Assert.True(calendar.HasMissingRates);

        CalendarDay day = calendar.Days.Single(d => d.Date == new DateOnly(2026, 3, 10));
        Assert.True(day.IsCurrentMonth);
        Assert.Equal(0m, day.TotalDop); // el bucket fallido se excluye, sin lanzar
    }

    [Fact]
    public async Task ExecuteAsync_TodayInRange_ExactlyOneDayIsToday()
    {
        FakeExpenseRepository repo = new();
        FakeClock clock = new() { Today = new DateOnly(2026, 3, 15) };
        GetCalendarMonthQuery query = new(repo, new FakeCurrencyConverter(), clock);

        Result result = await query.ExecuteAsync(_march, DayOfWeek.Sunday);

        CalendarMonthResult calendar = Assert.IsType<CalendarMonthResult>(result.Value);
        CalendarDay today = Assert.Single(calendar.Days, d => d.IsToday);
        Assert.Equal(new DateOnly(2026, 3, 15), today.Date);
    }

    [Fact]
    public async Task ExecuteAsync_AlwaysReturnsFortyTwoDays()
    {
        FakeExpenseRepository repo = new();
        GetCalendarMonthQuery query = new(repo, new FakeCurrencyConverter(), new FakeClock());

        Result result = await query.ExecuteAsync(_march, DayOfWeek.Monday);

        CalendarMonthResult calendar = Assert.IsType<CalendarMonthResult>(result.Value);
        Assert.Equal(CalendarGrid.CellCount, calendar.Days.Count);
    }

    private static Expense CreateExpense(DateOnly date, decimal amount, Currency currency)
        => Expense.Create(
            Guid.NewGuid(),
            date,
            Money.Create(amount, currency),
            "Test expense",
            null,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
}
