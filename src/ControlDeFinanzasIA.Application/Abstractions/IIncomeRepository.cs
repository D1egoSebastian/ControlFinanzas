using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Transactions;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Repositorio para gestionar agregados <see cref="Income"/>.
/// </summary>
public interface IIncomeRepository
{
    /// <summary>
    /// Añade un nuevo <see cref="Income"/> al store.
    /// </summary>
    /// <param name="income">Entidad a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si falló (p. ej. la entidad ya existe).
    /// </returns>
    Task<Result> AddAsync(Income income, CancellationToken ct = default);

    /// <summary>
    /// Actualiza un <see cref="Income"/> existente.
    /// </summary>
    /// <param name="income">Entidad a actualizar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> UpdateAsync(Income income, CancellationToken ct = default);

    /// <summary>
    /// Elimina un <see cref="Income"/> por su ID.
    /// </summary>
    /// <param name="id">Identificador del agregado a eliminar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un <see cref="Income"/> por su ID, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="id">Identificador del agregado.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los <see cref="Income"/>. La colección retornada
    /// es de solo lectura.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<Income>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene una página de <see cref="Income"/> que cumplen con el
    /// filtro especificado, ordenados por <see cref="Income.Date"/>
    /// descendente y con <see cref="Income.CreatedAt"/> descendente
    /// como desempate.
    /// </summary>
    /// <param name="filter">Filtro de paginación y criterios de búsqueda.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="PagedResult{Income}"/> con los items de la página
    /// solicitada y los metadatos de paginación.
    /// </returns>
    Task<PagedResult<Income>> GetByFilterAsync(IncomeFilter filter, CancellationToken ct = default);
}
