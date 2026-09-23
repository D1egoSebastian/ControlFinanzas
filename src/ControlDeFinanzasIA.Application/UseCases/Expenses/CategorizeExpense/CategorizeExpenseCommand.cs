namespace ControlDeFinanzasIA.Application.UseCases.Expenses.CategorizeExpense;

/// <summary>
/// Comando de entrada para <see cref="CategorizeExpenseUseCase"/> (F-013,
/// slice 5b, D-7).
/// </summary>
/// <param name="Description">
/// Descripción del gasto tal como la escribió el usuario, sin recortar ni
/// validar. El use case aplica los guardas de longitud y truncado.
/// </param>
public sealed record CategorizeExpenseCommand(string Description);
