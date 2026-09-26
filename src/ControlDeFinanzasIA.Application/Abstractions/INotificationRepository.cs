using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Notifications;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Plan de reconciliación (D-4) producido por <c>NotificationReconciler.Plan</c>
/// para una corrida de detección: qué avisos se agregan, se refrescan
/// (<see cref="Notification.RefreshFacts"/> ya aplicado por el caller) o se
/// eliminan (purgados o cuya fuente ya no califica), tras comparar las filas
/// existentes contra los candidatos de la corrida actual.
/// </summary>
/// <param name="ToAdd">Avisos nuevos a insertar.</param>
/// <param name="ToUpdate">Avisos no descartados existentes a refrescar.</param>
/// <param name="ToRemove">Ids de avisos a eliminar (purga o fuente que ya no califica).</param>
public sealed record NotificationReconciliationPlan(
    IReadOnlyList<Notification> ToAdd,
    IReadOnlyList<Notification> ToUpdate,
    IReadOnlyList<Guid> ToRemove)
{
    /// <summary><see langword="true"/> si el plan no tiene ningún cambio que aplicar.</summary>
    public bool IsEmpty => ToAdd.Count == 0 && ToUpdate.Count == 0 && ToRemove.Count == 0;
}

/// <summary>
/// Repositorio para gestionar agregados <see cref="Notification"/> (F-017, R-1).
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    /// Obtiene todos los <see cref="Notification"/> (incluyendo los
    /// descartados). La colección retornada es de solo lectura.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene los avisos no descartados (<see cref="Notification.IsDismissed"/>
    /// es <see langword="false"/>), ordenados por <see cref="Notification.ReferenceDate"/>
    /// y luego por <see cref="Notification.Id"/>.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<Notification>> GetUndismissedAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene un <see cref="Notification"/> por su ID, o <see langword="null"/> si no existe.
    /// </summary>
    /// <param name="id">Identificador del agregado.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los avisos cuyo <see cref="Notification.Id"/> está en <paramref name="ids"/>.
    /// </summary>
    /// <param name="ids">Ids a buscar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<Notification>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Actualiza un lote de avisos existentes (p. ej. tras <see cref="Notification.MarkRead"/>
    /// o <see cref="Notification.Dismiss"/>) en una única <c>SaveChangesAsync</c>. Si el
    /// guardado falla, desvincula únicamente las entidades que esta llamada adjuntó
    /// (nunca <c>ChangeTracker.Clear()</c>).
    /// </summary>
    /// <param name="notifications">Avisos a actualizar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<Result> UpdateRangeAsync(IReadOnlyList<Notification> notifications, CancellationToken ct = default);

    /// <summary>
    /// Aplica las tres listas de <paramref name="plan"/> en una única
    /// <c>SaveChangesAsync</c> (reconciliación atómica, D-1). Si el guardado
    /// falla (p. ej. una violación del índice único de
    /// <see cref="Notification.DedupKey"/>), desvincula únicamente las
    /// entidades que esta llamada adjuntó (nunca <c>ChangeTracker.Clear()</c>)
    /// y retorna <c>Result.Failure</c> sin dejar el <c>DbContext</c> compartido
    /// envenenado para llamadas posteriores.
    /// </summary>
    /// <param name="plan">Plan de reconciliación a aplicar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<Result> ApplyReconciliationAsync(NotificationReconciliationPlan plan, CancellationToken ct = default);
}
