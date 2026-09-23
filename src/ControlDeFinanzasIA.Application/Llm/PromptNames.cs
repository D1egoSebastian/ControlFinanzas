namespace ControlDeFinanzasIA.Application.Llm;

/// <summary>
/// Nombres de las plantillas de prompt usadas por el pipeline de IA
/// (F-013, D-4). Ver
/// <see cref="ControlDeFinanzasIA.Application.Abstractions.Llm.IPromptProvider"/>.
/// </summary>
public static class PromptNames
{
    /// <summary>Prompt de sistema para categorización automática de gastos.</summary>
    public const string CategorizeExpenseSystem = "categorize-expense.system";

    /// <summary>Prompt de usuario para categorización automática de gastos.</summary>
    public const string CategorizeExpenseUser = "categorize-expense.user";

    /// <summary>Prompt de sistema para el resumen de insights del dashboard (F-014).</summary>
    public const string GenerateInsightsSystem = "generate-insights.system";

    /// <summary>Prompt de usuario para el resumen de insights del dashboard (F-014).</summary>
    public const string GenerateInsightsUser = "generate-insights.user";

    /// <summary>Prompt de sistema para la narración por lotes de anomalías (F-014).</summary>
    public const string ExplainAnomaliesSystem = "explain-anomalies.system";

    /// <summary>Prompt de usuario para la narración por lotes de anomalías (F-014).</summary>
    public const string ExplainAnomaliesUser = "explain-anomalies.user";
}
