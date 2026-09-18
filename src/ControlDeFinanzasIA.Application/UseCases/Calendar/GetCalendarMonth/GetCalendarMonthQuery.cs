using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Aggregates;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.UseCases.Calendar.GetCalendarMonth;

/// <summary>
/// Query que arma la grilla del calendario mensual (R-1, R-2, R-5): une los
/// <see cref="CalendarGrid.CellCount"/> días de <see cref="CalendarGrid.Dates"/>
/// con los totales diarios de
/// <see cref="IExpenseRepository.GetDailyTotalsAsync"/> y normaliza cada día
/// a DOP vía <see cref="ICurrencyConverter.ConvertAsync"/> (no
/// <see cref="ICurrencyConverter.DisplayAsync"/>, ver D-3).
/// </summary>
public sealed class GetCalendarMonthQuery
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly ICurrencyConverter _currencyConverter;
    private readonly IClock _clock;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public GetCalendarMonthQuery(
        IExpenseRepository expenseRepository,
        ICurrencyConverter currencyConverter,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(expenseRepository);
        ArgumentNullException.ThrowIfNull(currencyConverter);
        ArgumentNullException.ThrowIfNull(clock);

        _expenseRepository = expenseRepository;
        _currencyConverter = currencyConverter;
        _clock = clock;
    }

    /// <summary>
    /// Ejecuta la query para el mes que contiene <paramref name="monthAnchor"/>.
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha dentro del mes navegado.</param>
    /// <param name="firstDayOfWeek">
    /// Primer día de la semana usado para armar la grilla (D-2). La UI lo
    /// obtiene de <see cref="System.Globalization.CultureInfo.CurrentCulture"/>;
    /// esta capa no depende de cultura ambiente.
    /// </param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con un <see cref="CalendarMonthResult"/> en
    /// <see cref="Result.Value"/>. Nunca falla por sí misma: un mes sin
    /// gastos produce totales en cero para sus días (R-2), y una tasa de
    /// cambio no disponible degrada a
    /// <see cref="CalendarMonthResult.HasMissingRates"/> en vez de un
    /// <see cref="Error"/>.
    /// </returns>
    public async Task<Result> ExecuteAsync(
        DateOnly monthAnchor,
        DayOfWeek firstDayOfWeek,
        CancellationToken ct = default)
    {
        DateOnly anchor = MonthPeriod.Anchor(monthAnchor);
        IReadOnlyList<DateOnly> gridDates = CalendarGrid.Dates(anchor, firstDayOfWeek);
        IReadOnlyList<DayCurrencyTotal> dailyTotals =
            await _expenseRepository.GetDailyTotalsAsync(MonthPeriod.Range(anchor), ct).ConfigureAwait(false);

        Dictionary<DateOnly, decimal> totalsByDate = new();
        bool hasMissingRates = false;

        foreach (DayCurrencyTotal row in dailyTotals)
        {
            var source = Money.Create(row.Amount, row.Currency);
            Result converted = await _currencyConverter.ConvertAsync(source, Currency.DOP, ct).ConfigureAwait(false);
            if (!converted.IsSuccess)
            {
                hasMissingRates = true;
                continue;
            }

            decimal amountDop = ((Money)converted.Value!).Amount;
            totalsByDate[row.Date] = totalsByDate.TryGetValue(row.Date, out decimal existing)
                ? existing + amountDop
                : amountDop;
        }

        DateOnly today = _clock.Today;
        var days = new List<CalendarDay>(CalendarGrid.CellCount);
        decimal monthTotalDop = 0m;

        foreach (DateOnly date in gridDates)
        {
            bool isCurrentMonth = MonthPeriod.Anchor(date) == anchor;
            decimal? totalDop = null;

            if (isCurrentMonth)
            {
                decimal amount = totalsByDate.TryGetValue(date, out decimal existing) ? existing : 0m;
                totalDop = amount;
                monthTotalDop += amount;
            }

            days.Add(new CalendarDay(date, totalDop, isCurrentMonth, date == today));
        }

        return Result.Success(new CalendarMonthResult(anchor, days, monthTotalDop, hasMissingRates));
    }
}
