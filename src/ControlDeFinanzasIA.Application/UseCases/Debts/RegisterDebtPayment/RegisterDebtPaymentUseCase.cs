using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Debts;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Debts.RegisterDebtPayment;

/// <summary>
/// Resultado exitoso de <see cref="RegisterDebtPaymentUseCase"/>: el
/// saldo pendiente recalculado (R-7) y si la deuda se cerr&#243;
/// autom&#225;ticamente como consecuencia de este pago (R-6).
/// </summary>
public sealed record RegisterDebtPaymentResult(
    Guid DebtId,
    Guid DebtPaymentId,
    decimal PendingBalance,
    bool DebtClosed);

/// <summary>
/// Use case que registra el pago total de una <see cref="DebtPayment"/>
/// (R-4), recalcula el saldo pendiente de la deuda (R-7) y la cierra
/// autom&#225;ticamente si era la &#250;ltima cuota pendiente (R-6, S-5).
/// </summary>
public sealed class RegisterDebtPaymentUseCase
{
    private readonly IDebtRepository _debtRepository;
    private readonly IDebtPaymentRepository _paymentRepository;
    private readonly IClock _clock;
    private readonly ILogger<RegisterDebtPaymentUseCase> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public RegisterDebtPaymentUseCase(
        IDebtRepository debtRepository,
        IDebtPaymentRepository paymentRepository,
        IClock clock,
        ILogger<RegisterDebtPaymentUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(debtRepository);
        ArgumentNullException.ThrowIfNull(paymentRepository);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _debtRepository = debtRepository;
        _paymentRepository = paymentRepository;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Registra el pago de la cuota <paramref name="debtPaymentId"/>
    /// usando la fecha actual (<see cref="IClock.Today"/>) como fecha
    /// de pago.
    /// </summary>
    public Task<Result> ExecuteAsync(Guid debtPaymentId, CancellationToken ct = default) =>
        ExecuteAsync(debtPaymentId, _clock.Today, ct);

    /// <summary>
    /// Registra el pago de la cuota <paramref name="debtPaymentId"/> con
    /// una fecha de pago expl&#237;cita.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> con un <see cref="RegisterDebtPaymentResult"/>
    /// si la operaci&#243;n tiene &#233;xito; <see cref="Error"/> con c&#243;digo
    /// <c>"NotFound"</c> si la cuota o la deuda no existen, o
    /// <c>"DomainInvariant"</c> si la cuota ya no est&#225; Pending.
    /// </returns>
    public async Task<Result> ExecuteAsync(Guid debtPaymentId, DateOnly paidDate, CancellationToken ct = default)
    {
        DebtPayment? payment = await _paymentRepository.GetByIdAsync(debtPaymentId, ct).ConfigureAwait(false);
        if (payment is null)
        {
            return Result.Failure("NotFound", $"Debt payment {debtPaymentId} was not found.");
        }

        Debt? debt = await _debtRepository.GetByIdAsync(payment.DebtId, ct).ConfigureAwait(false);
        if (debt is null)
        {
            return Result.Failure("NotFound", $"Debt {payment.DebtId} was not found.");
        }

        IReadOnlyList<DebtPayment> allPayments = await _paymentRepository
            .GetByDebtIdAsync(payment.DebtId, ct)
            .ConfigureAwait(false);

        // Use the tracked instance from the full schedule so the auto-close
        // evaluation below sees the transition applied by this call.
        DebtPayment target = allPayments.First(p => p.Id == payment.Id);

        try
        {
            target.MarkAsPaid(paidDate);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while registering debt payment {DebtPaymentId}.", debtPaymentId);
            return Result.Failure("DomainInvariant", ex.Message);
        }

        Result updatePaymentResult = await _paymentRepository.UpdateAsync(target, ct).ConfigureAwait(false);
        if (!updatePaymentResult.IsSuccess)
        {
            return updatePaymentResult;
        }

        bool closed = debt.TryAutoClose(allPayments);
        if (closed)
        {
            Result updateDebtResult = await _debtRepository.UpdateAsync(debt, ct).ConfigureAwait(false);
            if (!updateDebtResult.IsSuccess)
            {
                return updateDebtResult;
            }
        }

        decimal pendingBalance = debt.CalculatePendingBalance(allPayments).Amount;
        return Result.Success(new RegisterDebtPaymentResult(debt.Id, target.Id, pendingBalance, closed));
    }
}
