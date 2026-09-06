using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Repositorio para gestionar agregados <see cref="Tag"/>.
/// </summary>
public interface ITagRepository
{
    /// <summary>
    /// Añade un nuevo <see cref="Tag"/> al store.
    /// </summary>
    /// <param name="tag">Entidad a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si falló (p. ej. la entidad ya existe).
    /// </returns>
    Task<Result> AddAsync(Tag tag, CancellationToken ct = default);

    /// <summary>
    /// Actualiza un <see cref="Tag"/> existente.
    /// </summary>
    /// <param name="tag">Entidad a actualizar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> UpdateAsync(Tag tag, CancellationToken ct = default);

    /// <summary>
    /// Elimina un <see cref="Tag"/> por su ID.
    /// </summary>
    /// <param name="id">Identificador del agregado a eliminar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un <see cref="Tag"/> por su ID, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="id">Identificador del agregado.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los <see cref="Tag"/>. La colección retornada
    /// es de solo lectura.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct = default);
}
