namespace ControlDeFinanzasIA.Application.Common;

/// <summary>
/// Envoltorio para resultados paginados de queries. Incluye los
/// items de la página actual más los metadatos de paginación
/// necesarios para que la UI construya controles de paginación.
/// </summary>
/// <typeparam name="T">
/// Tipo de los items en la página.
/// </typeparam>
/// <remarks>
/// La convención de paginación es <b>1-based</b> (la primera página
/// es 1, no 0). Esto es consistente con la convención de la UI y de
/// la mayoría de APIs REST.
/// </remarks>
/// <param name="Items">Items de la página actual.</param>
/// <param name="Total">Total de items en todas las páginas.</param>
/// <param name="Page">Número de página actual (1-based).</param>
/// <param name="PageSize">Items por página.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize)
{
    /// <summary>
    /// Total de páginas. Calculado como ceiling division:
    /// <c>(Total + PageSize - 1) / PageSize</c>.
    /// </summary>
    /// <remarks>
    /// Si <see cref="PageSize"/> es 0, retorna 0 (defensivo, evita
    /// <see cref="DivideByZeroException"/>). Esta es una validación
    /// defensiva: <see cref="PageSize"/> = 0 es un bug del caller,
    /// no un caso de uso.
    /// </remarks>
    public int TotalPages => PageSize == 0
        ? 0
        : (Total + PageSize - 1) / PageSize;
}
