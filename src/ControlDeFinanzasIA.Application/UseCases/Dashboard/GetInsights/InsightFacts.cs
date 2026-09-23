using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetInsights;

/// <summary>
/// Cifras ya calculadas por los queries deterministicos existentes del
/// dashboard (resumen del mes, desglose por categoria, presupuesto vs.
/// real), listas para narrarse por IA (F-014, R-4, D-10). Es el UNICO
/// insumo de <see cref="GetInsightsUseCase.ExecuteAsync"/> — el use case
/// no consulta ningun repositorio; toda cifra que aparece en el prompt
/// viene de este record (S-12).
/// </summary>
/// <param name="Month">Primer dia del mes calendario evaluado.</param>
/// <param name="ExpensesDop">Total de gastos del mes, normalizado a DOP.</param>
/// <param name="IncomesDop">Total de ingresos del mes, normalizado a DOP.</param>
/// <param name="NetBalanceDop">Balance neto del mes (<c>IncomesDop - ExpensesDop</c>).</param>
/// <param name="ExpensesDeltaPercent">
/// Variacion porcentual de gastos vs. el mes anterior, o
/// <see langword="null"/> si no hay mes anterior comparable.
/// </param>
/// <param name="TopCategories">
/// Hasta 3 categorias principales por gasto, "Otras" excluida.
/// </param>
/// <param name="TopBudgetLines">
/// Hasta 3 lineas presupuesto-vs-real determinadas, ya ordenadas por
/// porcentaje consumido descendente.
/// </param>
/// <param name="ProjectedDop">Proyeccion de fin de mes, o <see langword="null"/> si no hay una.</param>
/// <param name="BudgetTotalDop">Presupuesto total del mes, o <see langword="null"/> si no hay presupuesto.</param>
/// <param name="AnomalyCount">Cantidad de anomalias activas detectadas para el mes.</param>
/// <param name="HasMissingRates">
/// <see langword="true"/> si alguna de las cifras anteriores esta
/// incompleta por una conversion de moneda fallida.
/// </param>
public sealed record InsightFacts(
    DateOnly Month,
    decimal ExpensesDop,
    decimal IncomesDop,
    decimal NetBalanceDop,
    decimal? ExpensesDeltaPercent,
    IReadOnlyList<CategorySlice> TopCategories,
    IReadOnlyList<BudgetVsActualLine> TopBudgetLines,
    decimal? ProjectedDop,
    decimal? BudgetTotalDop,
    int AnomalyCount,
    bool HasMissingRates);
