namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;

/// <summary>
/// Resumen del mes seleccionado del Dashboard, ya normalizado a DOP
/// (R-2). Incluye los totales del mes anterior porque el delta
/// porcentual (<see cref="ExpensesDeltaPercent"/>,
/// <see cref="IncomesDeltaPercent"/>) solo puede calcularse después
/// de convertir ambos periodos a una moneda común.
/// </summary>
/// <param name="ExpensesDop">Total de gastos del mes seleccionado, en DOP.</param>
/// <param name="IncomesDop">Total de ingresos del mes seleccionado, en DOP.</param>
/// <param name="NetBalanceDop">
/// Balance neto del mes seleccionado (<see cref="IncomesDop"/> −
/// <see cref="ExpensesDop"/>).
/// </param>
/// <param name="PreviousExpensesDop">Total de gastos del mes anterior, en DOP.</param>
/// <param name="PreviousIncomesDop">Total de ingresos del mes anterior, en DOP.</param>
/// <param name="ExpensesDeltaPercent">
/// Delta porcentual de gastos vs. el mes anterior. <see langword="null"/>
/// cuando <see cref="PreviousExpensesDop"/> es 0 (división por cero
/// evitada explícitamente, no lanza excepción).
/// </param>
/// <param name="IncomesDeltaPercent">
/// Delta porcentual de ingresos vs. el mes anterior. <see langword="null"/>
/// cuando <see cref="PreviousIncomesDop"/> es 0.
/// </param>
/// <param name="HasMissingRates">
/// <see langword="true"/> si al menos un bucket por moneda (de
/// cualquiera de los 4 periodos/tipos agregados) no pudo convertirse
/// a DOP (R-7, S-6). El bucket afectado se excluye de la suma; el
/// resto del resumen se calcula igual, sin lanzar excepción.
/// </param>
public sealed record MonthSummaryResult(
    decimal ExpensesDop,
    decimal IncomesDop,
    decimal NetBalanceDop,
    decimal PreviousExpensesDop,
    decimal PreviousIncomesDop,
    decimal? ExpensesDeltaPercent,
    decimal? IncomesDeltaPercent,
    bool HasMissingRates);
