using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Budgets;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Repositorio para gestionar agregados <see cref="Budget"/>.
/// </summary>
public interface IBudgetRepository
{
    /// <summary>
    /// Añade un nuevo <see cref="Budget"/> al store.
    /// </summary>
    /// <param name="budget">Entidad a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si falló (p. ej. la entidad ya existe).
    /// </returns>
    Task<Result> AddAsync(Budget budget, CancellationToken ct = default);

    /// <summary>
    /// Actualiza un <see cref="Budget"/> existente.
    /// </summary>
    /// <param name="budget">Entidad a actualizar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> UpdateAsync(Budget budget, CancellationToken ct = default);

    /// <summary>
    /// Elimina un <see cref="Budget"/> por su ID.
    /// </summary>
    /// <param name="id">Identificador del agregado a eliminar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un <see cref="Budget"/> por su ID, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="id">Identificador del agregado.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<Budget?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los <see cref="Budget"/>. La colección retornada
    /// es de solo lectura.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<Budget>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los <see cref="Budget"/> de un mes calendario,
    /// ordenados deterministicamente por <c>CategoryId</c>.
    /// </summary>
    /// <param name="year">Año del periodo.</param>
    /// <param name="month">Mes del periodo (1-12).</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<Budget>> GetByMonthAsync(int year, int month, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el único <see cref="Budget"/> de una categoría en un mes
    /// calendario, o <c>null</c> si no existe. Respalda el upsert de
    /// <c>CreateOrUpdateBudgetUseCase</c>.
    /// </summary>
    /// <param name="categoryId">Identificador de la categoría.</param>
    /// <param name="year">Año del periodo.</param>
    /// <param name="month">Mes del periodo (1-12).</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<Budget?> GetByCategoryAndMonthAsync(Guid categoryId, int year, int month, CancellationToken ct = default);
}
