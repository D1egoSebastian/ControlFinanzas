using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Debts;

namespace ControlDeFinanzasIA.Application.UseCases.Debts.GetDebts;

/// <summary>
/// Query que lista las deudas registradas, opcionalmente filtradas
/// por <see cref="DebtStatus"/> (Active/Closed).
/// </summary>
public sealed class GetDebtsQuery
{
    private readonly IDebtRepository _repository;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public GetDebtsQuery(IDebtRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>
    /// Ejecuta la query.
    /// </summary>
    /// <param name="status">
    /// Filtro opcional por estado. <see langword="null"/> devuelve
    /// tanto deudas activas como cerradas.
    /// </param>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con una <see cref="IReadOnlyList{T}"/> de
    /// <see cref="Debt"/> en <see cref="Result.Value"/>.
    /// </returns>
    public async Task<Result> ExecuteAsync(DebtStatus? status = null, CancellationToken ct = default)
    {
        IReadOnlyList<Debt> debts = await _repository.GetAllAsync(ct).ConfigureAwait(false);
        IReadOnlyList<Debt> filtered = status is null
            ? debts
            : debts.Where(d => d.Status == status.Value).ToList();

        return Result.Success(filtered);
    }
}
