using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Repositorio para gestionar agregados <see cref="Category"/>.
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// Añade un nuevo <see cref="Category"/> al store.
    /// </summary>
    /// <param name="category">Entidad a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si falló (p. ej. la entidad ya existe).
    /// </returns>
    Task<Result> AddAsync(Category category, CancellationToken ct = default);

    /// <summary>
    /// Actualiza un <see cref="Category"/> existente.
    /// </summary>
    /// <param name="category">Entidad a actualizar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> UpdateAsync(Category category, CancellationToken ct = default);

    /// <summary>
    /// Elimina un <see cref="Category"/> por su ID.
    /// </summary>
    /// <param name="id">Identificador del agregado a eliminar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un <see cref="Category"/> por su ID, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="id">Identificador del agregado.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los <see cref="Category"/>. La colección retornada
    /// es de solo lectura.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default);
}
