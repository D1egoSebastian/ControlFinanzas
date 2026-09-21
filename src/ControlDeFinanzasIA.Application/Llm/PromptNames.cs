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
}
