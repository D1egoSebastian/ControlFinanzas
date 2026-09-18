using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Debts;

/// <summary>
/// Servicio de dominio puro que calcula el calendario completo de
/// <see cref="DebtPayment"/> de una <see cref="Debt"/> no guardada
/// (R-3). Stateless: misma entrada, misma salida. Sin I/O, sin
/// dependencia de <c>IClock</c> (el calendario se calcula respecto a
/// <see cref="Debt.StartDate"/>, no respecto a "hoy").
/// </summary>
/// <remarks>
/// <para>
/// A diferencia de <c>Domain.Recurring.RecurringOccurrenceCalculator</c>
/// (que calcula ocurrencias dentro de un rango [from, to] y puede ser
/// indefinido), aqui el numero de cuotas es finito y conocido desde el
/// inicio: <see cref="Debt.TotalInstallments"/>. El generador siempre
/// devuelve exactamente esa cantidad de <see cref="DebtPayment"/>.
/// </para>
/// <para>
/// Estrategia de borde de mes para <see cref="DebtFrequency.Monthly"/>:
/// misma que <c>RecurringOccurrenceCalculator</c> — si la cuota cae en
/// dia 31 y el mes destino tiene menos dias, se usa el ultimo dia del
/// mes destino.
/// </para>
/// <para>
/// Para <see cref="DebtFrequency.Biweekly"/> ("quincenal"), cada cuota
/// esta espaciada exactamente 15 dias de la anterior, partiendo de
/// <see cref="Debt.StartDate"/> (S-6).
/// </para>
/// </remarks>
public sealed class DebtScheduleGenerator
{
    /// <summary>
    /// Genera el calendario completo de cuotas de <paramref name="debt"/>.
    /// </summary>
    /// <param name="debt">
    /// Deuda ya validada (via <see cref="Debt.Create"/>), tipicamente
    /// aun no persistida.
    /// </param>
    /// <returns>
    /// Una lista de exactamente <see cref="Debt.TotalInstallments"/>
    /// <see cref="DebtPayment"/>, en estado Pending, con
    /// <see cref="DebtPayment.InstallmentNumber"/> de 1 a N y
    /// <see cref="DebtPayment.ScheduledDate"/> calculadas segun la
    /// frecuencia de la deuda.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="debt"/> es null.</exception>
    public IReadOnlyList<DebtPayment> Generate(Debt debt)
    {
        ArgumentNullException.ThrowIfNull(debt);

        var payments = new List<DebtPayment>(debt.TotalInstallments);
        for (int installmentNumber = 1; installmentNumber <= debt.TotalInstallments; installmentNumber++)
        {
            DateOnly scheduledDate = CalculateScheduledDate(debt, installmentNumber);

            // Each DebtPayment must own a distinct Money instance: EF Core's
            // owned-type change tracking does not support the same owned
            // instance being shared across multiple owner entities (it
            // silently loses the value for all but one owner, resulting in
            // a NOT NULL constraint failure on insert).
            var installmentAmount = Money.Create(debt.InstallmentAmount.Amount, debt.InstallmentAmount.Currency);
            payments.Add(new DebtPayment(
                Guid.NewGuid(),
                debt.Id,
                installmentNumber,
                scheduledDate,
                installmentAmount,
                DebtPaymentStatus.Pending));
        }

        return payments;
    }

    private static DateOnly CalculateScheduledDate(Debt debt, int installmentNumber)
    {
        int offset = installmentNumber - 1;
        return debt.Frequency switch
        {
            nameof(DebtFrequency.Biweekly) => debt.StartDate.AddDays(15 * offset),
            _ => AddMonthsClamped(debt.StartDate, offset),
        };
    }

    /// <summary>
    /// Suma meses a una fecha, clamping al ultimo dia del mes destino
    /// si la fecha original cae en el ultimo dia de su propio mes
    /// (evita que un dia de pago "31" se "deslice" permanentemente al
    /// 28/29/30). Misma logica que
    /// <c>RecurringOccurrenceCalculator.AddMonthsClamped</c>.
    /// </summary>
    private static DateOnly AddMonthsClamped(DateOnly date, int months)
    {
        int year = date.Year;
        int month = date.Month + months;
        while (month > 12)
        {
            month -= 12;
            year += 1;
        }

        while (month < 1)
        {
            month += 12;
            year -= 1;
        }

        int targetDay = date.Day == DateTime.DaysInMonth(date.Year, date.Month)
            ? DateTime.DaysInMonth(year, month)
            : Math.Min(date.Day, DateTime.DaysInMonth(year, month));

        return new DateOnly(year, month, targetDay);
    }
}
