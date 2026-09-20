using ControlDeFinanzasIA.Domain.Budgets;

namespace ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;

/// <summary>
/// Una línea de comparación presupuesto-vs-real para una categoría
/// presupuestada, ya normalizada a DOP (R-5, D-8). Todos los campos
/// numéricos y <see cref="Status"/> son nullable: <see langword="null"/>
/// significa "indeterminado" (la conversión de moneda falló para al
/// menos un lado) y NUNCA debe interpretarse como 0% ni 100% (S-6).
/// </summary>
/// <param name="BudgetId">Identificador del <see cref="Budget"/> de esta línea.</param>
/// <param name="CategoryId">Identificador de la categoría presupuestada.</param>
/// <param name="CategoryName">
/// Nombre de la categoría, resuelto vía <c>ICategoryRepository</c> (una
/// categoría presupuestada sin gasto en el mes no produce fila en
/// <c>GetCategoryTotalsAsync</c> y quedaría sin nombre de lo contrario).
/// </param>
/// <param name="BudgetDop">
/// Monto presupuestado normalizado a DOP, o <see langword="null"/> si su
/// conversión falló.
/// </param>
/// <param name="ActualDop">
/// Gasto real normalizado a DOP, o <see langword="null"/> si la
/// conversión de al menos un bucket de esta categoría falló.
/// </param>
/// <param name="PercentConsumed">
/// Porcentaje consumido (<see cref="BudgetEvaluation.PercentConsumed"/>),
/// o <see langword="null"/> si la línea es indeterminada.
/// </param>
/// <param name="Status">
/// Estado (<see cref="BudgetEvaluator"/>), o <see langword="null"/> si la
/// línea es indeterminada.
/// </param>
public sealed record BudgetVsActualLine(
    Guid BudgetId,
    Guid CategoryId,
    string CategoryName,
    decimal? BudgetDop,
    decimal? ActualDop,
    decimal? PercentConsumed,
    BudgetStatus? Status)
{
    /// <summary>
    /// <see langword="true"/> cuando esta línea no pudo evaluarse porque
    /// la conversión a DOP falló en el lado del presupuesto o del gasto
    /// real (R-5, S-6).
    /// </summary>
    public bool IsIndeterminate => Status is null;
}

/// <summary>
/// Resultado de la comparación presupuesto-vs-real de un mes calendario
/// (R-4, R-5, D-8): una línea por cada categoría presupuestada ese mes.
/// </summary>
/// <param name="MonthAnchor">Primer día del mes calendario evaluado.</param>
/// <param name="Lines">
/// Una línea por presupuesto del mes, ordenadas por
/// <see cref="BudgetVsActualLine.PercentConsumed"/> descendente
/// (indeterminadas al final) y luego por
/// <see cref="BudgetVsActualLine.CategoryName"/> ordinal.
/// </param>
/// <param name="TotalBudgetDop">Suma de <c>BudgetDop</c> de las líneas determinadas.</param>
/// <param name="TotalActualDop">Suma de <c>ActualDop</c> de las líneas determinadas.</param>
/// <param name="HasMissingRates">
/// <see langword="true"/> si al menos una línea es indeterminada por una
/// tasa de cambio faltante (R-5, S-6).
/// </param>
public sealed record BudgetVsActualResult(
    DateOnly MonthAnchor,
    IReadOnlyList<BudgetVsActualLine> Lines,
    decimal TotalBudgetDop,
    decimal TotalActualDop,
    bool HasMissingRates);
