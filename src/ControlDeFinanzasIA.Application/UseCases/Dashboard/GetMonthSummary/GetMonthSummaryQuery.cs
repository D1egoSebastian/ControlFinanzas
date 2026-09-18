using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Aggregates;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;

/// <summary>
/// Query que calcula el resumen del mes seleccionado del Dashboard
/// (R-2): total de gastos, total de ingresos, balance neto y delta
/// porcentual vs. el mes anterior, todos normalizados a DOP.
/// </summary>
/// <remarks>
/// La normalización usa <see cref="ICurrencyConverter.ConvertAsync"/>
/// — no <see cref="ICurrencyConverter.DisplayAsync"/>, que convierte
/// siempre a la moneda "contraria" (DOP → USD incluido) y por lo
/// tanto no sirve para normalizar todo a DOP (ver design.md,
/// "Correction to the proposal"). Un bucket cuya conversión falla
/// (<c>Error.Code == "ExchangeRateUnavailable"</c>) se excluye de la
/// suma y marca <see cref="MonthSummaryResult.HasMissingRates"/>;
/// nunca lanza (R-7, S-6).
/// </remarks>
public sealed class GetMonthSummaryQuery
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IIncomeRepository _incomeRepository;
    private readonly ICurrencyConverter _currencyConverter;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public GetMonthSummaryQuery(
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
    /// Ejecuta la query para el mes que contiene <paramref name="monthAnchor"/>.
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha dentro del mes seleccionado.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con un <see cref="MonthSummaryResult"/> en
    /// <see cref="Result.Value"/>. Nunca falla por sí misma: un mes
    /// sin transacciones produce un resumen en cero (S-3), y una
    /// tasa de cambio no disponible degrada a
    /// <see cref="MonthSummaryResult.HasMissingRates"/> en vez de un
    /// <see cref="Error"/> (R-7).
    /// </returns>
    public async Task<Result> ExecuteAsync(DateOnly monthAnchor, CancellationToken ct = default)
    {
        DateRange selectedRange = MonthPeriod.Range(monthAnchor);
        DateRange previousRange = MonthPeriod.Range(MonthPeriod.Previous(monthAnchor));

        Task<IReadOnlyList<CurrencyTotal>> expenseTotalsTask =
            _expenseRepository.GetPeriodTotalsAsync(selectedRange, ct);
        Task<IReadOnlyList<CurrencyTotal>> incomeTotalsTask =
            _incomeRepository.GetPeriodTotalsAsync(selectedRange, ct);
        Task<IReadOnlyList<CurrencyTotal>> previousExpenseTotalsTask =
            _expenseRepository.GetPeriodTotalsAsync(previousRange, ct);
        Task<IReadOnlyList<CurrencyTotal>> previousIncomeTotalsTask =
            _incomeRepository.GetPeriodTotalsAsync(previousRange, ct);

        await Task.WhenAll(
            expenseTotalsTask, incomeTotalsTask, previousExpenseTotalsTask, previousIncomeTotalsTask)
            .ConfigureAwait(false);

        (decimal expensesDop, bool expensesMissing) =
            await SumConvertedToDopAsync(expenseTotalsTask.Result, ct).ConfigureAwait(false);
        (decimal incomesDop, bool incomesMissing) =
            await SumConvertedToDopAsync(incomeTotalsTask.Result, ct).ConfigureAwait(false);
        (decimal previousExpensesDop, bool previousExpensesMissing) =
            await SumConvertedToDopAsync(previousExpenseTotalsTask.Result, ct).ConfigureAwait(false);
        (decimal previousIncomesDop, bool previousIncomesMissing) =
            await SumConvertedToDopAsync(previousIncomeTotalsTask.Result, ct).ConfigureAwait(false);

        var result = new MonthSummaryResult(
            ExpensesDop: expensesDop,
            IncomesDop: incomesDop,
            NetBalanceDop: incomesDop - expensesDop,
            PreviousExpensesDop: previousExpensesDop,
            PreviousIncomesDop: previousIncomesDop,
            ExpensesDeltaPercent: CalculateDeltaPercent(expensesDop, previousExpensesDop),
            IncomesDeltaPercent: CalculateDeltaPercent(incomesDop, previousIncomesDop),
            HasMissingRates: expensesMissing || incomesMissing || previousExpensesMissing || previousIncomesMissing);

        return Result.Success(result);
    }

    /// <summary>
    /// Calcula el delta porcentual entre <paramref name="current"/> y
    /// <paramref name="previous"/>. Devuelve <see langword="null"/>
    /// cuando <paramref name="previous"/> es 0 para evitar una
    /// división por cero (spec R-2, S-1).
    /// </summary>
    private static decimal? CalculateDeltaPercent(decimal current, decimal previous)
    {
        if (previous == 0m)
        {
            return null;
        }

        return (current - previous) / previous * 100m;
    }

    /// <summary>
    /// Convierte cada <see cref="CurrencyTotal"/> a DOP y suma los que
    /// tuvieron éxito. Un bucket cuya conversión falla se excluye de
    /// la suma (no lanza) y se reporta vía el segundo elemento de la
    /// tupla.
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
}
