using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Transactions;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Repositorio para gestionar agregados <see cref="Expense"/>.
/// </summary>
public interface IExpenseRepository
{
    /// <summary>
    /// Añade un nuevo <see cref="Expense"/> al store.
    /// </summary>
    /// <param name="expense">Entidad a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si falló (p. ej. la entidad ya existe).
    /// </returns>
    Task<Result> AddAsync(Expense expense, CancellationToken ct = default);

    /// <summary>
    /// Actualiza un <see cref="Expense"/> existente.
    /// </summary>
    /// <param name="expense">Entidad a actualizar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> UpdateAsync(Expense expense, CancellationToken ct = default);

    /// <summary>
    /// Elimina un <see cref="Expense"/> por su ID.
    /// </summary>
    /// <param name="id">Identificador del agregado a eliminar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un <see cref="Expense"/> por su ID, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="id">Identificador del agregado.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los <see cref="Expense"/>. La colección retornada
    /// es de solo lectura.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene una página de <see cref="Expense"/> que cumplen con el
    /// filtro especificado, ordenados por <see cref="Expense.Date"/>
    /// descendente y con <see cref="Expense.CreatedAt"/> descendente
    /// como desempate.
    /// </summary>
    /// <param name="filter">Filtro de paginación y criterios de búsqueda.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="PagedResult{Expense}"/> con los items de la página
    /// solicitada y los metadatos de paginación.
    /// </returns>
    Task<PagedResult<Expense>> GetByFilterAsync(ExpenseFilter filter, CancellationToken ct = default);
}
