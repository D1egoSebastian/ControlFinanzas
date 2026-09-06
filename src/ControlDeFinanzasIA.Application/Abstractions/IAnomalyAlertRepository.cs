using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Alerts;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Repositorio para gestionar agregados <see cref="AnomalyAlert"/>.
/// </summary>
public interface IAnomalyAlertRepository
{
    /// <summary>
    /// Añade un nuevo <see cref="AnomalyAlert"/> al store.
    /// </summary>
    /// <param name="alert">Entidad a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si falló (p. ej. la entidad ya existe).
    /// </returns>
    Task<Result> AddAsync(AnomalyAlert alert, CancellationToken ct = default);

    /// <summary>
    /// Actualiza un <see cref="AnomalyAlert"/> existente.
    /// </summary>
    /// <param name="alert">Entidad a actualizar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> UpdateAsync(AnomalyAlert alert, CancellationToken ct = default);

    /// <summary>
    /// Elimina un <see cref="AnomalyAlert"/> por su ID.
    /// </summary>
    /// <param name="id">Identificador del agregado a eliminar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un <see cref="AnomalyAlert"/> por su ID, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="id">Identificador del agregado.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<AnomalyAlert?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los <see cref="AnomalyAlert"/>. La colección retornada
    /// es de solo lectura.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<AnomalyAlert>> GetAllAsync(CancellationToken ct = default);
}
