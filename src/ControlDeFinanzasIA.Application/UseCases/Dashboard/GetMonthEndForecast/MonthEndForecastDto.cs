using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Forecasting;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthEndForecast;

/// <summary>
/// Resultado de <see cref="GetMonthEndForecastUseCase.ExecuteAsync"/>
/// (F-014, D-6): la proyección de fin de mes ya normalizada a DOP, más la
/// comparación opcional contra el presupuesto total del mes (R-1, S-7, S-8).
/// </summary>
/// <param name="Month">Primer día del mes evaluado.</param>
/// <param name="State"><see cref="ForecastState"/> devuelto por <c>MonthEndForecaster</c>.</param>
/// <param name="SpentToDateDop">Gasto acumulado a la fecha, normalizado a DOP.</param>
/// <param name="ProjectedDop">
/// Proyección de fin de mes, normalizada a DOP. <see langword="null"/>
/// para <see cref="ForecastState.InsufficientData"/> y
/// <see cref="ForecastState.FutureMonth"/>.
/// </param>
/// <param name="ElapsedDays">Días transcurridos del mes (0 para <see cref="ForecastState.FutureMonth"/>).</param>
/// <param name="DaysInMonth">Días totales del mes evaluado.</param>
/// <param name="BudgetTotalDop">
/// Suma de los presupuestos del mes, normalizada a DOP.
/// <see langword="null"/> cuando el mes no tiene presupuestos (S-8) O
/// cuando alguna conversión de presupuesto falló (poison rule, D-6 paso 6).
/// </param>
/// <param name="ProjectedPercentOfBudget">
/// <see cref="BudgetEvaluation.PercentConsumed"/> de comparar
/// <see cref="ProjectedDop"/> contra <see cref="BudgetTotalDop"/>.
/// <see langword="null"/> si no hay presupuesto, la proyección no es
/// numérica, o el total presupuestado es cero.
/// </param>
/// <param name="ProjectedBudgetStatus">
/// <see cref="BudgetEvaluation.Status"/> de la misma comparación.
/// <see langword="null"/> en las mismas condiciones que
/// <see cref="ProjectedPercentOfBudget"/>.
/// </param>
/// <param name="HasMissingRates">
/// <see langword="true"/> si alguna conversión de moneda (gasto o
/// presupuesto) falló durante el cálculo.
/// </param>
public sealed record MonthEndForecastDto(
    DateOnly Month,
    ForecastState State,
    decimal SpentToDateDop,
    decimal? ProjectedDop,
    int ElapsedDays,
    int DaysInMonth,
    decimal? BudgetTotalDop,
    decimal? ProjectedPercentOfBudget,
    BudgetStatus? ProjectedBudgetStatus,
    bool HasMissingRates);
