using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Common;

/// <summary>
/// Tests para <see cref="CalendarGrid"/> (Fase 9, F-009, slice 1):
/// desfase inicial (leading offset) para cada <see cref="DayOfWeek"/>, mes
/// que arranca exactamente en <c>firstDayOfWeek</c> (offset 0), mes de 31
/// días, febrero bisiesto/no bisiesto, y que la grilla siempre devuelve
/// exactamente <see cref="CalendarGrid.CellCount"/> fechas contiguas (R-1,
/// R-3, S-5).
/// </summary>
public sealed class CalendarGridTests
{
    // 2026-04-01 es un miércoles (verificado con `date -d 2026-04-01 +%A`).
    private static readonly DateOnly _aprilAnchor = new(2026, 4, 1);

    [Theory]
    [InlineData(DayOfWeek.Wednesday, 0)] // el mes arranca justo en firstDayOfWeek
    [InlineData(DayOfWeek.Tuesday, 1)]
    [InlineData(DayOfWeek.Monday, 2)]
    [InlineData(DayOfWeek.Sunday, 3)]
    [InlineData(DayOfWeek.Saturday, 4)]
    [InlineData(DayOfWeek.Friday, 5)]
    [InlineData(DayOfWeek.Thursday, 6)]
    public void LeadingOffset_EveryFirstDayOfWeek_ReturnsExpectedOffset(DayOfWeek firstDayOfWeek, int expectedOffset)
    {
        int offset = CalendarGrid.LeadingOffset(_aprilAnchor, firstDayOfWeek);

        Assert.Equal(expectedOffset, offset);
    }

    [Fact]
    public void LeadingOffset_MonthStartsExactlyOnFirstDayOfWeek_ReturnsZero()
    {
        // 2026-03-01 es un domingo.
        var marchAnchor = new DateOnly(2026, 3, 1);

        int offset = CalendarGrid.LeadingOffset(marchAnchor, DayOfWeek.Sunday);

        Assert.Equal(0, offset);
    }

    [Fact]
    public void GridStart_MonthStartsExactlyOnFirstDayOfWeek_EqualsMonthAnchor()
    {
        var marchAnchor = new DateOnly(2026, 3, 1);

        DateOnly start = CalendarGrid.GridStart(marchAnchor, DayOfWeek.Sunday);

        Assert.Equal(marchAnchor, start);
    }

    [Fact]
    public void GridStart_MidWeekStart_SubtractsLeadingOffsetDays()
    {
        // 2026-04-01 (miércoles) con semana que empieza en lunes: offset 2.
        DateOnly start = CalendarGrid.GridStart(_aprilAnchor, DayOfWeek.Monday);

        Assert.Equal(new DateOnly(2026, 3, 30), start);
    }

    [Fact]
    public void Dates_ThirtyOneDayMonth_ContainsFirstAndLastDayOfMonth()
    {
        // Marzo 2026 tiene 31 días.
        var marchAnchor = new DateOnly(2026, 3, 15);

        IReadOnlyList<DateOnly> dates = CalendarGrid.Dates(marchAnchor, DayOfWeek.Monday);

        Assert.Contains(new DateOnly(2026, 3, 1), dates);
        Assert.Contains(new DateOnly(2026, 3, 31), dates);
    }

    [Fact]
    public void Dates_LeapYearFebruary_ContainsTwentyNinth()
    {
        var leapFebruaryAnchor = new DateOnly(2024, 2, 10);

        IReadOnlyList<DateOnly> dates = CalendarGrid.Dates(leapFebruaryAnchor, DayOfWeek.Monday);

        Assert.Contains(new DateOnly(2024, 2, 29), dates);
    }

    [Fact]
    public void Dates_NonLeapYearFebruary_DoesNotContainTwentyNinth()
    {
        var nonLeapFebruaryAnchor = new DateOnly(2026, 2, 10);

        IReadOnlyList<DateOnly> dates = CalendarGrid.Dates(nonLeapFebruaryAnchor, DayOfWeek.Monday);

        Assert.Contains(new DateOnly(2026, 2, 28), dates);
        // 2026-02-29 no existe (no es bisiesto); si CalendarGrid derivara mal
        // el mes siguiente, esto se vería como March 1 seguido de un salto,
        // no como una fecha inválida — se verifica con Month/Day en vez de
        // construir un DateOnly imposible.
        Assert.DoesNotContain(dates, d => d.Month == 2 && d.Day == 29);
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Tuesday)]
    [InlineData(DayOfWeek.Wednesday)]
    [InlineData(DayOfWeek.Thursday)]
    [InlineData(DayOfWeek.Friday)]
    [InlineData(DayOfWeek.Saturday)]
    public void Dates_AnyFirstDayOfWeek_ReturnsExactlyFortyTwoContiguousDates(DayOfWeek firstDayOfWeek)
    {
        IReadOnlyList<DateOnly> dates = CalendarGrid.Dates(_aprilAnchor, firstDayOfWeek);

        Assert.Equal(CalendarGrid.CellCount, dates.Count);
        for (int i = 1; i < dates.Count; i++)
        {
            Assert.Equal(dates[i - 1].AddDays(1), dates[i]);
        }
    }
}
