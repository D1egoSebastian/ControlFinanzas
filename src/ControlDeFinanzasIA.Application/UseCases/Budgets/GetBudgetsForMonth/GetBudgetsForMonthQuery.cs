using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Budgets;

namespace ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetsForMonth;

/// <summary>
/// Query que lista los presupuestos de un mes calendario. Lee el
/// repositorio directamente (<see cref="GetDebtsQuery"/> shape) — NO es
/// consumida por <c>GetBudgetVsActualQuery</c>, que también lee
/// <see cref="IBudgetRepository"/> de forma independiente (D-5).
/// </summary>
public sealed class GetBudgetsForMonthQuery
{
    private readonly IBudgetRepository _repository;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public GetBudgetsForMonthQuery(IBudgetRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>
    /// Ejecuta la query para el mes indicado.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> con una <see cref="IReadOnlyList{T}"/> de
    /// <see cref="Budget"/> en <see cref="Result.Value"/>.
    /// </returns>
    public async Task<Result> ExecuteAsync(int year, int month, CancellationToken ct = default)
    {
        IReadOnlyList<Budget> budgets = await _repository.GetByMonthAsync(year, month, ct).ConfigureAwait(false);
        return Result.Success(budgets);
    }
}
