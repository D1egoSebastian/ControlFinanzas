namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthlyEvolution;

/// <summary>
/// Un punto del gráfico de evolución temporal: totales de un mes
/// calendario, ya normalizados a DOP.
/// </summary>
/// <param name="Year">Año calendario del punto.</param>
/// <param name="Month">Mes calendario del punto (1-12).</param>
/// <param name="ExpensesDop">Total de gastos de ese mes, en DOP. 0 si el mes no tuvo filas (zero-fill).</param>
/// <param name="IncomesDop">Total de ingresos de ese mes, en DOP. 0 si el mes no tuvo filas (zero-fill).</param>
public sealed record EvolutionPoint(int Year, int Month, decimal ExpensesDop, decimal IncomesDop);

/// <summary>
/// Resultado del gráfico de evolución temporal (R-5).
/// </summary>
/// <param name="Points">
/// Exactamente <c>months</c> puntos consecutivos, uno por cada mes de
/// la ventana (<see cref="Common.MonthPeriod.TrailingRange"/>), en
/// orden cronológico ascendente. Los meses sin filas se rellenan con
/// 0 (zero-fill) para mantener el eje X estable entre navegaciones.
/// </param>
/// <param name="HasMissingRates">
/// <see langword="true"/> si al menos un bucket por moneda no pudo
/// convertirse a DOP (R-7, S-6); ese bucket se excluye de la suma de
/// su mes, sin lanzar excepción.
/// </param>
public sealed record MonthlyEvolutionResult(
    IReadOnlyList<EvolutionPoint> Points,
    bool HasMissingRates);
