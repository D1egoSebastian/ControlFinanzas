using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Goals;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Repositorio para gestionar agregados <see cref="GoalContribution"/>.
/// </summary>
public interface IGoalContributionRepository
{
    /// <summary>
    /// Añade un nuevo <see cref="GoalContribution"/> al store.
    /// </summary>
    /// <param name="contribution">Entidad a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si falló (p. ej. la entidad ya existe).
    /// </returns>
    Task<Result> AddAsync(GoalContribution contribution, CancellationToken ct = default);

    /// <summary>
    /// Actualiza un <see cref="GoalContribution"/> existente.
    /// </summary>
    /// <param name="contribution">Entidad a actualizar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> UpdateAsync(GoalContribution contribution, CancellationToken ct = default);

    /// <summary>
    /// Elimina un <see cref="GoalContribution"/> por su ID.
    /// </summary>
    /// <param name="id">Identificador del agregado a eliminar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un <see cref="GoalContribution"/> por su ID, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="id">Identificador del agregado.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<GoalContribution?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los <see cref="GoalContribution"/>. La colección retornada
    /// es de solo lectura.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<GoalContribution>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los aportes de una meta especifica, ordenados por
    /// <see cref="GoalContribution.Date"/>. Analogo directo de
    /// <c>IDebtPaymentRepository.GetByDebtIdAsync</c>. Obligatorio, no
    /// opcional: el progreso de una meta (R-5) se computa sumando esta
    /// coleccion, nunca se almacena.
    /// </summary>
    /// <param name="goalId">Identificador de la meta.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<GoalContribution>> GetByGoalIdAsync(Guid goalId, CancellationToken ct = default);
}
