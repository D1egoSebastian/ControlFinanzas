using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Aggregates;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;

/// <summary>
/// Query que compara el presupuesto de cada categoría contra su gasto
/// real del mes (R-4, R-5, R-6, D-8). Lee <see cref="IBudgetRepository"/>
/// directamente — NO delega en <c>GetBudgetsForMonthQuery</c> (D-5), ya
/// que esa query no aporta lógica adicional y componer <see cref="Result"/>
/// anidados sería ruido sin beneficio.
/// </summary>
/// <remarks>
/// Clona el cuerpo de normalización DOP + <c>hasMissingRates</c> de
/// <c>GetCategoryBreakdownQuery</c>, aplicado a AMBOS lados de la
/// comparación (presupuesto y gasto real), con una diferencia clave:
/// aquí una conversión fallida en el lado del gasto real envenena TODA
/// la categoría (no solo el bucket de moneda que falló), porque un
/// porcentaje parcialmente sumado se vería como un número confiable
/// pero incorrecto (R-5, S-6).
/// </remarks>
public sealed class GetBudgetVsActualQuery
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICurrencyConverter _currencyConverter;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public GetBudgetVsActualQuery(
        IBudgetRepository budgetRepository,
        IExpenseRepository expenseRepository,
        ICategoryRepository categoryRepository,
        ICurrencyConverter currencyConverter)
    {
        ArgumentNullException.ThrowIfNull(budgetRepository);
        ArgumentNullException.ThrowIfNull(expenseRepository);
        ArgumentNullException.ThrowIfNull(categoryRepository);
        ArgumentNullException.ThrowIfNull(currencyConverter);

        _budgetRepository = budgetRepository;
        _expenseRepository = expenseRepository;
        _categoryRepository = categoryRepository;
        _currencyConverter = currencyConverter;
    }

    /// <summary>
    /// Ejecuta la comparación presupuesto-vs-real para el mes que
    /// contiene <paramref name="monthAnchor"/>.
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha dentro del mes evaluado.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con un <see cref="BudgetVsActualResult"/> en
    /// <see cref="Result.Value"/>. Un mes sin presupuestos produce un
    /// resultado vacío sin consultar <c>GetCategoryTotalsAsync</c> (S-4).
    /// </returns>
    public async Task<Result> ExecuteAsync(DateOnly monthAnchor, CancellationToken ct = default)
    {
        int year = monthAnchor.Year;
        int month = monthAnchor.Month;

        IReadOnlyList<Budget> budgets = await _budgetRepository
            .GetByMonthAsync(year, month, ct)
            .ConfigureAwait(false);

        if (budgets.Count == 0)
        {
            return Result.Success(new BudgetVsActualResult(MonthPeriod.Anchor(monthAnchor), [], 0m, 0m, false));
        }

        var budgetedCategoryIds = budgets.Select(b => b.CategoryId!.Value).ToHashSet();

        IReadOnlyList<CategoryCurrencyTotal> totals = await _expenseRepository
            .GetCategoryTotalsAsync(MonthPeriod.Range(monthAnchor), ct)
            .ConfigureAwait(false);

        bool hasMissingRates = false;

        // Lado real: filtra a categorías presupuestadas ANTES de convertir
        // (una falla en una categoría sin presupuesto no debe encender
        // HasMissingRates ni asustar sobre una cifra que Fase 10 no
        // muestra). Una falla parcial dentro de una categoría presupuestada
        // envenena la categoría entera (actualFailed), no solo el bucket.
        Dictionary<Guid, decimal> actualDop = new();
        HashSet<Guid> actualFailed = new();

        foreach (CategoryCurrencyTotal row in totals)
        {
            if (!budgetedCategoryIds.Contains(row.CategoryId))
            {
                continue;
            }

            if (actualFailed.Contains(row.CategoryId))
            {
                continue;
            }

            var source = Money.Create(row.Amount, row.Currency);
            Result converted = await _currencyConverter.ConvertAsync(source, Currency.DOP, ct).ConfigureAwait(false);
            if (!converted.IsSuccess)
            {
                hasMissingRates = true;
                actualFailed.Add(row.CategoryId);
                actualDop.Remove(row.CategoryId);
                continue;
            }

            decimal amountDop = ((Money)converted.Value!).Amount;
            actualDop[row.CategoryId] = actualDop.GetValueOrDefault(row.CategoryId) + amountDop;
        }

        // Nombres de categoría: GetCategoryTotalsAsync solo trae una fila
        // por categoría CON gasto en el periodo, así que una categoría
        // presupuestada con gasto cero no aparecería en `totals` y
        // quedaría sin nombre. Un solo round-trip cubre ambos casos.
        IReadOnlyList<Category> allCategories = await _categoryRepository.GetAllAsync(ct).ConfigureAwait(false);
        var categoryNames = allCategories.ToDictionary(c => c.Id, c => c.Name);

        List<BudgetVsActualLine> lines = new(budgets.Count);
        decimal totalBudgetDop = 0m;
        decimal totalActualDop = 0m;

        foreach (Budget budget in budgets)
        {
            Guid categoryId = budget.CategoryId!.Value;
            string categoryName = categoryNames.TryGetValue(categoryId, out string? name) ? name : string.Empty;

            Result convertedBudget = await _currencyConverter
                .ConvertAsync(budget.Amount, Currency.DOP, ct)
                .ConfigureAwait(false);

            bool budgetConverted = convertedBudget.IsSuccess;
            if (!budgetConverted)
            {
                hasMissingRates = true;
            }

            bool actualConverted = !actualFailed.Contains(categoryId);
            bool isDeterminate = budgetConverted && actualConverted;

            if (!isDeterminate)
            {
                lines.Add(new BudgetVsActualLine(
                    BudgetId: budget.Id,
                    CategoryId: categoryId,
                    CategoryName: categoryName,
                    BudgetDop: budgetConverted ? ((Money)convertedBudget.Value!).Amount : null,
                    ActualDop: actualConverted ? actualDop.GetValueOrDefault(categoryId) : null,
                    PercentConsumed: null,
                    Status: null));
                continue;
            }

            decimal budgetDop = ((Money)convertedBudget.Value!).Amount;
            decimal categoryActualDop = actualDop.GetValueOrDefault(categoryId);
            BudgetEvaluation evaluation = BudgetEvaluator.Evaluate(budgetDop, categoryActualDop);

            totalBudgetDop += budgetDop;
            totalActualDop += categoryActualDop;

            lines.Add(new BudgetVsActualLine(
                BudgetId: budget.Id,
                CategoryId: categoryId,
                CategoryName: categoryName,
                BudgetDop: budgetDop,
                ActualDop: categoryActualDop,
                PercentConsumed: evaluation.PercentConsumed,
                Status: evaluation.Status));
        }

        // Orden determinístico: problemas flotan arriba (PercentConsumed
        // descendente), indeterminadas al fondo (-1 las hunde), empate por
        // nombre ordinal para no depender del orden de iteración interno.
        var orderedLines = lines
            .OrderByDescending(l => l.PercentConsumed ?? -1m)
            .ThenBy(l => l.CategoryName, StringComparer.Ordinal)
            .ToList();

        return Result.Success(new BudgetVsActualResult(
            MonthPeriod.Anchor(monthAnchor), orderedLines, totalBudgetDop, totalActualDop, hasMissingRates));
    }
}
