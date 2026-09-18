using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Debts;

namespace ControlDeFinanzasIA.Application.UseCases.Debts.GetDebtSchedule;

/// <summary>
/// Query que devuelve el calendario completo de cuotas de una deuda,
/// ordenado por <see cref="DebtPayment.InstallmentNumber"/>.
/// </summary>
public sealed class GetDebtScheduleQuery
{
    private readonly IDebtRepository _debtRepository;
    private readonly IDebtPaymentRepository _paymentRepository;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public GetDebtScheduleQuery(IDebtRepository debtRepository, IDebtPaymentRepository paymentRepository)
    {
        ArgumentNullException.ThrowIfNull(debtRepository);
        ArgumentNullException.ThrowIfNull(paymentRepository);

        _debtRepository = debtRepository;
        _paymentRepository = paymentRepository;
    }

    /// <returns>
    /// <see cref="Result"/> con un <see cref="IReadOnlyList{T}"/> de
    /// <see cref="DebtPayment"/> en <see cref="Result.Value"/> si la
    /// deuda existe; <see cref="Error"/> con c&#243;digo <c>"NotFound"</c>
    /// en caso contrario.
    /// </returns>
    public async Task<Result> ExecuteAsync(Guid debtId, CancellationToken ct = default)
    {
        Debt? debt = await _debtRepository.GetByIdAsync(debtId, ct).ConfigureAwait(false);
        if (debt is null)
        {
            return Result.Failure("NotFound", $"Debt {debtId} was not found.");
        }

        IReadOnlyList<DebtPayment> payments = await _paymentRepository
            .GetByDebtIdAsync(debtId, ct)
            .ConfigureAwait(false);

        return Result.Success(payments);
    }
}
