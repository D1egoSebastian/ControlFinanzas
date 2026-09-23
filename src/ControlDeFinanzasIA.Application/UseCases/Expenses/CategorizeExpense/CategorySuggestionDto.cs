namespace ControlDeFinanzasIA.Application.UseCases.Expenses.CategorizeExpense;

/// <summary>
/// Resultado de un round trip exitoso de <see cref="CategorizeExpenseUseCase"/>
/// (F-013, slice 5b, R-3, D-7). Un round trip limpio que no encontró
/// categoría coincidente es <c>Matched = false</c> — sigue siendo
/// <c>Result.Success</c>, nunca <c>Result.Failure</c> (D-7).
/// </summary>
/// <param name="CategoryId">Id de la categoría emparejada, o <c>null</c> si <paramref name="Matched"/> es <c>false</c>.</param>
/// <param name="CategoryName">Nombre exacto de la categoría emparejada, o <c>null</c> si <paramref name="Matched"/> es <c>false</c>.</param>
/// <param name="Matched">
/// <see langword="true"/> únicamente cuando el candidato del modelo
/// emparejó exactamente (D-6) con UNA sola categoría existente.
/// </param>
/// <param name="RawModelAnswer">
/// Contenido crudo devuelto por el modelo, truncado a 200 caracteres.
/// Existe para ajustar el prompt más adelante; se registra en Debug junto
/// al correlation id (D-7).
/// </param>
public sealed record CategorySuggestionDto(
    Guid? CategoryId,
    string? CategoryName,
    bool Matched,
    string RawModelAnswer);
