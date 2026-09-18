using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Debts;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Debts.SkipDebtPayment;

/// <summary>
/// Use case que marca una <see cref="DebtPayment"/> como saltada
/// (R-5), sin afectar la numeraci&#243;n ni las fechas de las cuotas
/// siguientes y sin cerrar la deuda (el cierre autom&#225;tico requiere
/// que la &#250;ltima cuota est&#233; Paid, no Skipped; ver R-6).
/// </summary>
public sealed class SkipDebtPaymentUseCase
{
    private readonly IDebtPaymentRepository _paymentRepository;
    private readonly ILogger<SkipDebtPaymentUseCase> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public SkipDebtPaymentUseCase(IDebtPaymentRepository paymentRepository, ILogger<SkipDebtPaymentUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(paymentRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _paymentRepository = paymentRepository;
        _logger = logger;
    }

    /// <returns>
    /// <see cref="Result"/> exitoso si la cuota se marc&#243; como saltada;
    /// <see cref="Error"/> con c&#243;digo <c>"NotFound"</c> si la cuota no
    /// existe, o <c>"DomainInvariant"</c> si ya no est&#225; Pending.
    /// </returns>
    public async Task<Result> ExecuteAsync(Guid debtPaymentId, CancellationToken ct = default)
    {
        DebtPayment? payment = await _paymentRepository.GetByIdAsync(debtPaymentId, ct).ConfigureAwait(false);
        if (payment is null)
        {
            return Result.Failure("NotFound", $"Debt payment {debtPaymentId} was not found.");
        }

        try
        {
            payment.Skip();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while skipping debt payment {DebtPaymentId}.", debtPaymentId);
            return Result.Failure("DomainInvariant", ex.Message);
        }

        return await _paymentRepository.UpdateAsync(payment, ct).ConfigureAwait(false);
    }
}
