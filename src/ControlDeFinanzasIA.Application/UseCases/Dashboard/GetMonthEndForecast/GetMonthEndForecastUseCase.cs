using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Aggregates;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Forecasting;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthEndForecast;

/// <summary>
/// Use case que calcula la proyección de fin de mes con <b>cero</b>
/// dependencia de <c>ILLMClient</c> (F-014, R-1, D-6). Orquesta
/// <see cref="IExpenseRepository"/>/<see cref="IBudgetRepository"/> +
/// <see cref="ICurrencyConverter"/> para normalizar a DOP, delega el
/// cálculo puro en <c>MonthEndForecaster</c>, y reutiliza
/// <see cref="BudgetEvaluator.Evaluate"/> para la comparación
/// proyección-vs-presupuesto (misma fuente de verdad que Fase 10).
/// </summary>
/// <remarks>
/// Nunca devuelve <see cref="Result.Failure(string, string)"/> por
/// razones de datos: un mes sin historial, sin presupuesto, o con una
/// tasa de cambio caída degrada el resultado (campos <see langword="null"/>
/// / <see cref="MonthEndForecastDto.HasMissingRates"/>) en vez de fallar.
/// </remarks>
public sealed class GetMonthEndForecastUseCase
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly ICurrencyConverter _currencyConverter;
    private readonly IClock _clock;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public GetMonthEndForecastUseCase(
        IExpenseRepository expenseRepository,
        IBudgetRepository budgetRepository,
        ICurrencyConverter currencyConverter,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(expenseRepository);
        ArgumentNullException.ThrowIfNull(budgetRepository);
        ArgumentNullException.ThrowIfNull(currencyConverter);
        ArgumentNullException.ThrowIfNull(clock);

        _expenseRepository = expenseRepository;
        _budgetRepository = budgetRepository;
        _currencyConverter = currencyConverter;
        _clock = clock;
    }

    /// <summary>
    /// Ejecuta la proyección de fin de mes para el mes que contiene
    /// <paramref name="monthAnchor"/>.
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha dentro del mes evaluado.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con un <see cref="MonthEndForecastDto"/> en
    /// <see cref="Result.Value"/>. Nunca falla por razones de datos.
    /// </returns>
    public async Task<Result> ExecuteAsync(DateOnly monthAnchor, CancellationToken ct = default)
    {
        DateOnly anchor = MonthPeriod.Anchor(monthAnchor);
        DateOnly today = _clock.Today;
        int daysInMonth = DateTime.DaysInMonth(anchor.Year, anchor.Month);

        (int Year, int Month) anchorMonth = (anchor.Year, anchor.Month);
        (int Year, int Month) todayMonth = (today.Year, today.Month);

        // Future month: MonthEndForecaster returns null-projected regardless
        // of inputs, so skip every query (D-6 step 2 — "no queries").
        if (todayMonth.CompareTo(anchorMonth) < 0)
        {
            MonthEndForecast futureForecast = MonthEndForecaster.Project(anchor, today, 0m, null);
            return Result.Success(ToDto(anchor, futureForecast, null, null, null, false));
        }

        bool isClosedMonth = todayMonth.CompareTo(anchorMonth) > 0;
        bool hasMissingRates = false;

        // Spent-to-date range: current month only up to "today" (a
        // future-dated expense later this month is not "spent to date");
        // closed month: the full calendar range (D10 "past = actual").
        DateRange spentRange = isClosedMonth ? MonthPeriod.Range(anchor) : DateRange.Create(anchor, today);
        IReadOnlyList<CurrencyTotal> spentTotals = await _expenseRepository
            .GetPeriodTotalsAsync(spentRange, ct)
            .ConfigureAwait(false);
        (decimal spentToDateDop, bool spentMissing) = await SumConvertedToDopAsync(spentTotals, ct).ConfigureAwait(false);
        hasMissingRates |= spentMissing;

        // History (trailing 3 complete months): only relevant for the
        // current month — MonthEndForecaster ignores it for closed/future
        // states, so skip the query for a closed month.
        decimal? historicalDailyAverageDop = null;
        if (!isClosedMonth)
        {
            (historicalDailyAverageDop, bool historyMissing) = await ComputeHistoricalDailyAverageAsync(anchor, ct).ConfigureAwait(false);
            hasMissingRates |= historyMissing;
        }

        MonthEndForecast forecast = MonthEndForecaster.Project(anchor, today, spentToDateDop, historicalDailyAverageDop);

        // Budget comparison (D-6 step 6): reuses BudgetEvaluator, same
        // threshold source of truth as Phase 10.
        IReadOnlyList<Budget> budgets = await _budgetRepository
            .GetByMonthAsync(anchor.Year, anchor.Month, ct)
            .ConfigureAwait(false);

        decimal? budgetTotalDop = null;
        decimal? projectedPercent = null;
        BudgetStatus? projectedStatus = null;

        if (budgets.Count > 0)
        {
            decimal sum = 0m;
            bool budgetConversionFailed = false;

            foreach (Budget budget in budgets)
            {
                Result converted = await _currencyConverter
                    .ConvertAsync(budget.Amount, Currency.DOP, ct)
                    .ConfigureAwait(false);

                if (!converted.IsSuccess)
                {
                    budgetConversionFailed = true;
                    continue;
                }

                sum += ((Money)converted.Value!).Amount;
            }

            if (budgetConversionFailed)
            {
                // A partially-summed budget total looks trustworthy but is
                // wrong — poison the whole total rather than show a
                // misleadingly low number (same rule as GetBudgetVsActualQuery).
                hasMissingRates = true;
            }
            else
            {
                budgetTotalDop = sum;

                if (forecast.ProjectedDop is not null && budgetTotalDop.Value > 0)
                {
                    BudgetEvaluation evaluation = BudgetEvaluator.Evaluate(budgetTotalDop.Value, forecast.ProjectedDop.Value);
                    projectedPercent = evaluation.PercentConsumed;
                    projectedStatus = evaluation.Status;
                }
            }
        }

        return Result.Success(ToDto(anchor, forecast, budgetTotalDop, projectedPercent, projectedStatus, hasMissingRates));
    }

    /// <summary>
    /// Calcula el promedio diario de los 3 meses completos previos al mes
    /// evaluado (D-6 step 4). Meses sin gasto (total DOP == 0, tras excluir
    /// buckets cuya conversión falló) se excluyen del promedio: ni su
    /// monto ni sus días cuentan. Sin meses elegibles → <see langword="null"/>.
    /// </summary>
    private async Task<(decimal? Average, bool HasMissingRates)> ComputeHistoricalDailyAverageAsync(
        DateOnly anchor, CancellationToken ct)
    {
        DateRange historyRange = MonthPeriod.TrailingRange(MonthPeriod.Previous(anchor), MonthEndForecaster.HistoryMonths);
        IReadOnlyList<MonthCurrencyTotal> monthlyTotals = await _expenseRepository
            .GetMonthlyTotalsAsync(historyRange, ct)
            .ConfigureAwait(false);

        bool hasMissingRates = false;
        Dictionary<(int Year, int Month), decimal> perMonthDop = new();

        foreach (MonthCurrencyTotal row in monthlyTotals)
        {
            var source = Money.Create(row.Amount, row.Currency);
            Result converted = await _currencyConverter.ConvertAsync(source, Currency.DOP, ct).ConfigureAwait(false);
            if (!converted.IsSuccess)
            {
                hasMissingRates = true;
                continue;
            }

            (int Year, int Month) key = (row.Year, row.Month);
            decimal amountDop = ((Money)converted.Value!).Amount;
            perMonthDop[key] = perMonthDop.GetValueOrDefault(key) + amountDop;
        }

        decimal totalSpend = 0m;
        int totalDays = 0;

        foreach (KeyValuePair<(int Year, int Month), decimal> kvp in perMonthDop)
        {
            if (kvp.Value <= 0m)
            {
                continue;
            }

            totalSpend += kvp.Value;
            totalDays += DateTime.DaysInMonth(kvp.Key.Year, kvp.Key.Month);
        }

        decimal? average = totalDays > 0 ? totalSpend / totalDays : null;
        return (average, hasMissingRates);
    }

    /// <summary>
    /// Convierte cada <see cref="CurrencyTotal"/> a DOP y suma los que
    /// tuvieron éxito. Un bucket cuya conversión falla se excluye de la
    /// suma (no lanza) y se reporta vía el segundo elemento de la tupla —
    /// mismo patrón que <c>GetMonthSummaryQuery</c>.
    /// </summary>
    private async Task<(decimal Total, bool HasMissingRates)> SumConvertedToDopAsync(
        IReadOnlyList<CurrencyTotal> totals, CancellationToken ct)
    {
        decimal sum = 0m;
        bool hasMissingRates = false;

        foreach (CurrencyTotal total in totals)
        {
            var source = Money.Create(total.Amount, total.Currency);
            Result converted = await _currencyConverter.ConvertAsync(source, Currency.DOP, ct).ConfigureAwait(false);
            if (!converted.IsSuccess)
            {
                hasMissingRates = true;
                continue;
            }

            sum += ((Money)converted.Value!).Amount;
        }

        return (sum, hasMissingRates);
    }

    private static MonthEndForecastDto ToDto(
        DateOnly anchor,
        MonthEndForecast forecast,
        decimal? budgetTotalDop,
        decimal? projectedPercent,
        BudgetStatus? projectedStatus,
        bool hasMissingRates) =>
        new(
            anchor,
            forecast.State,
            forecast.SpentToDateDop,
            forecast.ProjectedDop,
            forecast.ElapsedDays,
            forecast.DaysInMonth,
            budgetTotalDop,
            projectedPercent,
            projectedStatus,
            hasMissingRates);
}
