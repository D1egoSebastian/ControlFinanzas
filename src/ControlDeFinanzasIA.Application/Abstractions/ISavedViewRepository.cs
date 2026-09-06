using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Repositorio para gestionar agregados <see cref="SavedView"/>.
/// </summary>
public interface ISavedViewRepository
{
    /// <summary>
    /// Añade un nuevo <see cref="SavedView"/> al store.
    /// </summary>
    /// <param name="savedView">Entidad a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si falló (p. ej. la entidad ya existe).
    /// </returns>
    Task<Result> AddAsync(SavedView savedView, CancellationToken ct = default);

    /// <summary>
    /// Actualiza un <see cref="SavedView"/> existente.
    /// </summary>
    /// <param name="savedView">Entidad a actualizar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> UpdateAsync(SavedView savedView, CancellationToken ct = default);

    /// <summary>
    /// Elimina un <see cref="SavedView"/> por su ID.
    /// </summary>
    /// <param name="id">Identificador del agregado a eliminar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un <see cref="SavedView"/> por su ID, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="id">Identificador del agregado.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<SavedView?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los <see cref="SavedView"/>. La colección retornada
    /// es de solo lectura.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<SavedView>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los <see cref="SavedView"/> del tipo de entidad dado,
    /// ordenados por nombre. La colección retornada es de solo lectura.
    /// </summary>
    /// <param name="entityType">Tipo de transacción (gastos o ingresos).</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<SavedView>> GetByEntityTypeAsync(
        TransactionType entityType, CancellationToken ct = default);
}
