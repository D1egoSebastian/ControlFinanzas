using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Plan de reconciliación (D-8) producido por <c>DetectAnomaliesUseCase</c>
/// para un mes dado: qué alertas se agregan, se refrescan o se eliminan
/// tras comparar la salida actual de <see cref="AnomalyAlert"/> contra
/// <c>AnomalyDetector.Detect</c>.
/// </summary>
/// <param name="ToAdd">Alertas nuevas a insertar.</param>
/// <param name="ToUpdate">Alertas no reconocidas existentes a refrescar (<see cref="AnomalyAlert.Refresh"/> ya aplicado por el caller).</param>
/// <param name="ToRemove">Ids de alertas no reconocidas que ya no califican o cuyo gasto fue eliminado.</param>
public sealed record AnomalyReconciliationPlan(
    IReadOnlyList<AnomalyAlert> ToAdd,
    IReadOnlyList<AnomalyAlert> ToUpdate,
    IReadOnlyList<Guid> ToRemove)
{
    /// <summary><see langword="true"/> si el plan no tiene ningún cambio que aplicar.</summary>
    public bool IsEmpty => ToAdd.Count == 0 && ToUpdate.Count == 0 && ToRemove.Count == 0;
}

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

    /// <summary>
    /// Obtiene todas las alertas (incluyendo reconocidas) cuya
    /// <see cref="AnomalyAlert.Date"/> cae dentro de <paramref name="month"/>.
    /// </summary>
    /// <param name="month">Rango del mes a consultar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<AnomalyAlert>> GetByMonthAsync(DateRange month, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las alertas (incluyendo reconocidas) cuyo
    /// <see cref="AnomalyAlert.TransactionId"/> está en <paramref name="ids"/>.
    /// Usada para capturar una alerta cuyo gasto fue re-fechado fuera del
    /// mes que originalmente la contenía (D-8).
    /// </summary>
    /// <param name="ids">Ids de transacción a buscar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<AnomalyAlert>> GetByTransactionIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Aplica las tres listas de <paramref name="plan"/> en una única
    /// <c>SaveChangesAsync</c> (reconciliación atómica, D-8). Si el guardado
    /// falla (p. ej. una violación del índice único de
    /// <see cref="AnomalyAlert.TransactionId"/>), desvincula únicamente las
    /// entidades que esta llamada adjuntó (nunca <c>ChangeTracker.Clear()</c>)
    /// y retorna <c>Result.Failure</c> sin dejar el <c>DbContext</c> compartido
    /// envenenado para llamadas posteriores.
    /// </summary>
    /// <param name="plan">Plan de reconciliación a aplicar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<Result> ApplyReconciliationAsync(AnomalyReconciliationPlan plan, CancellationToken ct = default);
}
