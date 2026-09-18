using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Debts;

/// <summary>
/// Representa una cuota individual dentro del plan de pagos de una
/// <see cref="Debt"/>. Se crea exclusivamente a traves de
/// <see cref="DebtScheduleGenerator"/> al momento de crear la deuda
/// (R-3): no existe un factory publico de creacion suelta, ya que una
/// <see cref="DebtPayment"/> huerfana (sin <see cref="Debt"/> valida)
/// no tiene sentido de negocio.
/// </summary>
/// <remarks>
/// Setters privados y constructor sin parametros reservado para EF
/// Core, mismo patron que <see cref="Debt"/> y
/// <c>Domain.Recurring.RecurringRule</c>. Las transiciones de estado
/// (<see cref="MarkAsPaid"/>, <see cref="Skip"/>) son metodos con
/// nombre que re-validan la invariante "solo se puede transicionar
/// desde Pending".
/// </remarks>
public sealed class DebtPayment
{
    /// <summary>Identificador unico de la cuota.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador de la <see cref="Debt"/> due&#241;a de esta cuota.</summary>
    public Guid DebtId { get; private set; }

    /// <summary>
    /// Numero de cuota dentro del plan (1-based). Nunca cambia, ni
    /// siquiera si cuotas anteriores o posteriores son saltadas (R-5).
    /// </summary>
    public int InstallmentNumber { get; private set; }

    /// <summary>Fecha en que la cuota debe pagarse segun el plan original.</summary>
    public DateOnly ScheduledDate { get; private set; }

    /// <summary>Fecha en que la cuota fue efectivamente pagada. Null mientras no este Paid.</summary>
    public DateOnly? PaidDate { get; private set; }

    /// <summary>Monto de la cuota (pago total; pago parcial fuera de alcance v1).</summary>
    public Money Amount { get; private set; } = null!;

    /// <summary>Estado actual de la cuota.</summary>
    public DebtPaymentStatus Status { get; private set; }

    /// <summary>
    /// Constructor sin parametros reservado para EF Core. No debe
    /// usarse desde codigo de aplicacion.
    /// </summary>
    private DebtPayment()
    {
    }

    /// <summary>
    /// Constructor con parametros para uso interno de
    /// <see cref="DebtScheduleGenerator"/>. El caller ya valido
    /// las invariantes (monto positivo, numero de cuota positivo).
    /// </summary>
    internal DebtPayment(
        Guid id,
        Guid debtId,
        int installmentNumber,
        DateOnly scheduledDate,
        Money amount,
        DebtPaymentStatus status,
        DateOnly? paidDate = null)
    {
        Id = id;
        DebtId = debtId;
        InstallmentNumber = installmentNumber;
        ScheduledDate = scheduledDate;
        Amount = amount;
        Status = status;
        PaidDate = paidDate;
    }

    /// <summary>
    /// Marca la cuota como pagada en su totalidad (R-4). Solo es
    /// valido cuando la cuota esta en estado <see cref="DebtPaymentStatus.Pending"/>.
    /// </summary>
    /// <param name="paidDate">Fecha en que se registra el pago.</param>
    /// <exception cref="InvalidOperationException">
    /// Se lanza si la cuota no esta en estado Pending (ya fue pagada o saltada).
    /// </exception>
    public void MarkAsPaid(DateOnly paidDate)
    {
        if (Status != DebtPaymentStatus.Pending)
        {
            throw new InvalidOperationException(
                $"DebtPayment {Id} (installment {InstallmentNumber}) cannot be marked as paid because its status is {Status}, not Pending.");
        }

        Status = DebtPaymentStatus.Paid;
        PaidDate = paidDate;
    }

    /// <summary>
    /// Marca la cuota como saltada (R-5), sin afectar la numeracion ni
    /// las fechas de las demas cuotas. Solo es valido cuando la cuota
    /// esta en estado <see cref="DebtPaymentStatus.Pending"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se lanza si la cuota no esta en estado Pending (ya fue pagada o saltada).
    /// </exception>
    public void Skip()
    {
        if (Status != DebtPaymentStatus.Pending)
        {
            throw new InvalidOperationException(
                $"DebtPayment {Id} (installment {InstallmentNumber}) cannot be skipped because its status is {Status}, not Pending.");
        }

        Status = DebtPaymentStatus.Skipped;
    }
}
