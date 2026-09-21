using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Goals.DeleteGoal;

/// <summary>
/// Use case que elimina una meta de ahorro por su ID (R-1, D-8).
/// Wrapper delgado sobre <see cref="IGoalRepository.RemoveAsync"/>: la
/// eliminación es dura (hard delete); sus <c>GoalContribution</c> se
/// eliminan en cascada vía la FK (D-8/D-9), no aquí.
/// </summary>
public sealed class DeleteGoalUseCase
{
    private readonly IGoalRepository _repository;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public DeleteGoalUseCase(IGoalRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>
    /// Elimina la meta identificada por <paramref name="goalId"/>.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> con <c>IsSuccess = true</c> si la operación
    /// tuvo éxito, o con <see cref="Error"/> código <c>"NotFound"</c> si
    /// el ID no existe.
    /// </returns>
    public Task<Result> ExecuteAsync(Guid goalId, CancellationToken ct = default)
        => _repository.RemoveAsync(goalId, ct);
}
