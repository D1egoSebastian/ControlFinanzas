namespace ControlDeFinanzasIA.Application.UseCases.Expenses.GetTransactions;

/// <summary>
/// Request inmutable para <see cref="GetTransactionsQuery"/>.
/// Expone paginación y filtros opcionales por categoría, rango de fecha
/// y tags; los valores por defecto <c>null</c> mantienen el comportamiento
/// previo a Phase 4 para callers existentes.
/// </summary>
/// <param name="Page">Número de página solicitada (1-based). Valor por defecto: 1.</param>
/// <param name="PageSize">Cantidad de items por página. Valor por defecto: 50.</param>
/// <param name="CategoryId">Identificador de categoría opcional.</param>
/// <param name="DateFrom">Fecha inicial inclusive opcional.</param>
/// <param name="DateTo">Fecha final inclusive opcional.</param>
/// <param name="TagIds">Identificadores de tags para OR-matching opcional.</param>
/// <param name="Text">Optional text searched inside the description (partial match).</param>
public sealed record GetTransactionsQueryRequest(
    int Page = 1,
    int PageSize = 50,
    Guid? CategoryId = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    IReadOnlyList<Guid>? TagIds = null,
    string? Text = null);
