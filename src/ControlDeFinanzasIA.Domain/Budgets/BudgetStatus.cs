namespace ControlDeFinanzasIA.Domain.Budgets;

/// <summary>
/// Estado de consumo de un presupuesto mensual (F-010, R-6), calculado
/// por <see cref="BudgetEvaluator"/> a partir del porcentaje consumido.
/// </summary>
public enum BudgetStatus
{
    /// <summary>Menos del 80% del presupuesto consumido.</summary>
    Ok = 0,

    /// <summary>Entre 80% (inclusive) y 100% (exclusive) del presupuesto consumido.</summary>
    Near = 1,

    /// <summary>100% (inclusive) o mas del presupuesto consumido.</summary>
    Exceeded = 2
}
