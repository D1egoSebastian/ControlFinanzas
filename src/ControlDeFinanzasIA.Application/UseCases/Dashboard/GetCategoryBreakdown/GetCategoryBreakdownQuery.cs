using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Aggregates;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;

/// <summary>
/// Query que arma el gráfico de categorías del mes seleccionado
/// (R-4): trae <em>todas</em> las categorías del periodo desde
/// <see cref="IExpenseRepository.GetCategoryTotalsAsync"/>, normaliza
/// cada bucket a DOP y rankea + folda "Otras" aquí, no en SQL (D-2 —
/// rankear montos sin normalizar mezclaría monedas incorrectamente).
/// </summary>
public sealed class GetCategoryBreakdownQuery
{
    /// <summary>
    /// Etiqueta de la porción sintética que agrupa las categorías
    /// fuera del top-N (R-4). Se define en Application porque la
    /// capa no tiene acceso a localización (esa es responsabilidad
    /// de la UI/Avalonia, prohibida aquí por ArchitectureTests).
    /// </summary>
    private const string _otherCategoryLabel = "Otras";

    private readonly IExpenseRepository _expenseRepository;
    private readonly ICurrencyConverter _currencyConverter;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public GetCategoryBreakdownQuery(IExpenseRepository expenseRepository, ICurrencyConverter currencyConverter)
    {
        ArgumentNullException.ThrowIfNull(expenseRepository);
        ArgumentNullException.ThrowIfNull(currencyConverter);

        _expenseRepository = expenseRepository;
        _currencyConverter = currencyConverter;
    }

    /// <summary>
    /// Ejecuta la query para el mes que contiene <paramref name="monthAnchor"/>.
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha dentro del mes seleccionado.</param>
    /// <param name="topCount">
    /// Cantidad de categorías individuales a mostrar antes de agrupar
    /// el resto en "Otras". Por defecto <see cref="DashboardDefaults.TopCategories"/>.
    /// </param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con un <see cref="CategoryBreakdownResult"/>
    /// en <see cref="Result.Value"/>. Un mes sin gastos produce
    /// <see cref="CategoryBreakdownResult.Slices"/> vacío (S-3).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <paramref name="topCount"/> no es positivo.
    /// </exception>
    public async Task<Result> ExecuteAsync(
        DateOnly monthAnchor,
        int topCount = DashboardDefaults.TopCategories,
        CancellationToken ct = default)
    {
        if (topCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(topCount), topCount, "topCount must be greater than zero.");
        }

        DateRange range = MonthPeriod.Range(monthAnchor);
        IReadOnlyList<CategoryCurrencyTotal> categoryTotals =
            await _expenseRepository.GetCategoryTotalsAsync(range, ct).ConfigureAwait(false);

        Dictionary<Guid, (string Name, decimal AmountDop)> byCategory = new();
        bool hasMissingRates = false;

        foreach (CategoryCurrencyTotal row in categoryTotals)
        {
            var source = Money.Create(row.Amount, row.Currency);
            Result converted = await _currencyConverter.ConvertAsync(source, Currency.DOP, ct).ConfigureAwait(false);
            if (!converted.IsSuccess)
            {
                hasMissingRates = true;
                continue;
            }

            decimal amountDop = ((Money)converted.Value!).Amount;
            byCategory[row.CategoryId] = byCategory.TryGetValue(row.CategoryId, out (string Name, decimal AmountDop) existing)
                ? (existing.Name, existing.AmountDop + amountDop)
                : (row.CategoryName, amountDop);
        }

        // Orden determinístico: monto descendente, empates por nombre
        // (evita que el orden de un Dictionary decida el ranking en un empate).
        var ranked = byCategory
            .Select(kv => new CategorySlice(kv.Key, kv.Value.Name, kv.Value.AmountDop))
            .OrderByDescending(c => c.AmountDop)
            .ThenBy(c => c.Label, StringComparer.Ordinal)
            .ToList();

        decimal totalDop = ranked.Sum(c => c.AmountDop);

        List<CategorySlice> slices;
        if (ranked.Count <= topCount)
        {
            slices = ranked;
        }
        else
        {
            slices = ranked.Take(topCount).ToList();
            decimal otherAmount = ranked.Skip(topCount).Sum(c => c.AmountDop);
            slices.Add(new CategorySlice(null, _otherCategoryLabel, otherAmount));
        }

        return Result.Success(new CategoryBreakdownResult(slices, totalDop, hasMissingRates));
    }
}
