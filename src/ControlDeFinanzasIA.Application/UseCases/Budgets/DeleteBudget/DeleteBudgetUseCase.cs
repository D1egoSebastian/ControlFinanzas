using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Budgets.DeleteBudget;

/// <summary>
/// Use case que elimina un presupuesto por su ID (R-1). Wrapper delgado
/// sobre <see cref="IBudgetRepository.RemoveAsync"/>: la eliminación es
/// dura (hard delete), no existe concepto de archivado/soft-delete para
/// presupuestos.
/// </summary>
public sealed class DeleteBudgetUseCase
{
    private readonly IBudgetRepository _repository;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public DeleteBudgetUseCase(IBudgetRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>
    /// Elimina el presupuesto identificado por <paramref name="budgetId"/>.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> con <c>IsSuccess = true</c> si la operación
    /// tuvo éxito, o con <see cref="Error"/> código <c>"NotFound"</c> si
    /// el ID no existe.
    /// </returns>
    public Task<Result> ExecuteAsync(Guid budgetId, CancellationToken ct = default)
        => _repository.RemoveAsync(budgetId, ct);
}
