using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Aggregates;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthlyEvolution;

/// <summary>
/// Query que arma el gráfico de evolución temporal (R-5): totales
/// mensuales de gastos e ingresos sobre una ventana de
/// <see cref="DashboardDefaults.EvolutionMonths"/> meses consecutivos
/// terminando en (e incluyendo) el mes seleccionado, normalizados a
/// DOP y con zero-fill para los meses sin filas.
/// </summary>
public sealed class GetMonthlyEvolutionQuery
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IIncomeRepository _incomeRepository;
    private readonly ICurrencyConverter _currencyConverter;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public GetMonthlyEvolutionQuery(
        IExpenseRepository expenseRepository,
        IIncomeRepository incomeRepository,
        ICurrencyConverter currencyConverter)
    {
        ArgumentNullException.ThrowIfNull(expenseRepository);
        ArgumentNullException.ThrowIfNull(incomeRepository);
        ArgumentNullException.ThrowIfNull(currencyConverter);

        _expenseRepository = expenseRepository;
        _incomeRepository = incomeRepository;
        _currencyConverter = currencyConverter;
    }

    /// <summary>
    /// Ejecuta la query para la ventana de <paramref name="months"/>
    /// meses terminando en el mes que contiene <paramref name="monthAnchor"/>.
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha dentro del último mes de la ventana.</param>
    /// <param name="months">
    /// Tamaño de la ventana en meses. Por defecto
    /// <see cref="DashboardDefaults.EvolutionMonths"/>.
    /// </param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con un <see cref="MonthlyEvolutionResult"/>
    /// en <see cref="Result.Value"/>, con exactamente <paramref name="months"/>
    /// puntos (zero-filled) sin importar cuántos meses tengan datos reales.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <paramref name="months"/> no es positivo (propagada desde
    /// <see cref="MonthPeriod.TrailingRange"/>).
    /// </exception>
    public async Task<Result> ExecuteAsync(
        DateOnly monthAnchor,
        int months = DashboardDefaults.EvolutionMonths,
        CancellationToken ct = default)
    {
        DateRange range = MonthPeriod.TrailingRange(monthAnchor, months);

        Task<IReadOnlyList<MonthCurrencyTotal>> expenseMonthlyTask =
            _expenseRepository.GetMonthlyTotalsAsync(range, ct);
        Task<IReadOnlyList<MonthCurrencyTotal>> incomeMonthlyTask =
            _incomeRepository.GetMonthlyTotalsAsync(range, ct);

        await Task.WhenAll(expenseMonthlyTask, incomeMonthlyTask).ConfigureAwait(false);

        Dictionary<(int Year, int Month), decimal> expensesByMonth = new();
        Dictionary<(int Year, int Month), decimal> incomesByMonth = new();
        bool hasMissingRates = false;

        hasMissingRates |= await AccumulateConvertedAsync(expenseMonthlyTask.Result, expensesByMonth, ct)
            .ConfigureAwait(false);
        hasMissingRates |= await AccumulateConvertedAsync(incomeMonthlyTask.Result, incomesByMonth, ct)
            .ConfigureAwait(false);

        var points = new List<EvolutionPoint>(months);
        DateOnly cursor = MonthPeriod.Anchor(range.Start);
        for (int i = 0; i < months; i++)
        {
            (int Year, int Month) key = (cursor.Year, cursor.Month);
            decimal expensesDop = expensesByMonth.GetValueOrDefault(key, 0m);
            decimal incomesDop = incomesByMonth.GetValueOrDefault(key, 0m);
            points.Add(new EvolutionPoint(cursor.Year, cursor.Month, expensesDop, incomesDop));
            cursor = cursor.AddMonths(1);
        }

        return Result.Success(new MonthlyEvolutionResult(points, hasMissingRates));
    }

    /// <summary>
    /// Convierte cada <see cref="MonthCurrencyTotal"/> a DOP y acumula
    /// el resultado en <paramref name="accumulator"/>, agrupando por
    /// (Año, Mes). Un bucket cuya conversión falla se excluye (no
    /// lanza) y se reporta en el valor de retorno.
    /// </summary>
    /// <returns><see langword="true"/> si al menos un bucket no pudo convertirse.</returns>
    private async Task<bool> AccumulateConvertedAsync(
        IReadOnlyList<MonthCurrencyTotal> totals,
        Dictionary<(int Year, int Month), decimal> accumulator,
        CancellationToken ct)
    {
        bool hasMissingRates = false;

        foreach (MonthCurrencyTotal total in totals)
        {
            var source = Money.Create(total.Amount, total.Currency);
            Result converted = await _currencyConverter.ConvertAsync(source, Currency.DOP, ct).ConfigureAwait(false);
            if (!converted.IsSuccess)
            {
                hasMissingRates = true;
                continue;
            }

            (int Year, int Month) key = (total.Year, total.Month);
            decimal amountDop = ((Money)converted.Value!).Amount;
            accumulator[key] = accumulator.GetValueOrDefault(key, 0m) + amountDop;
        }

        return hasMissingRates;
    }
}
