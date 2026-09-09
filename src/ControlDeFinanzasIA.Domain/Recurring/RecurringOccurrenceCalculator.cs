using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Recurring;

/// <summary>
/// Servicio de dominio puro que calcula las fechas de ocurrencia
/// de una regla recurrente. Stateless: misma entrada, misma salida.
/// </summary>
/// <remarks>
/// <para>
/// Estrategia de borde de mes: si la regla tiene dia 31 y el mes
/// destino tiene 28/29/30 dias, se usa el ultimo dia del mes
/// destino. Esto es consistente con la convencion de varios
/// sistemas de facturacion (Netflix cobra el 28 de feb si la suscripcion
/// empezo el 31 de enero).
/// </para>
/// <para>
/// La regla tiene un <see cref="RecurringRule.StartDate"/> y un
/// <see cref="RecurringRule.EndDate"/> (opcional). El calculator
/// respeta estos limites.
/// </para>
/// </remarks>
public sealed class RecurringOccurrenceCalculator
{
    /// <summary>
    /// Calcula las fechas de ocurrencia de la regla que caen dentro
    /// del rango [<paramref name="from"/>, <paramref name="to"/>]
    /// (inclusivo en ambos extremos).
    /// </summary>
    /// <param name="rule">Regla recurrente.</param>
    /// <param name="from">Fecha inicial del rango (inclusiva).</param>
    /// <param name="to">Fecha final del rango (inclusiva).</param>
    /// <returns>
    /// Lista de fechas en que la regla debe dispararse, en orden
    /// ascendente. Puede estar vacia si la regla esta fuera del
    /// rango o inactiva (<see cref="RecurringRule.Active"/> == false).
    /// </returns>
    public IReadOnlyList<DateOnly> CalculateOccurrences(
        RecurringRule rule,
        DateOnly from,
        DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (to < from)
        {
            return Array.Empty<DateOnly>();
        }

        if (!rule.Active)
        {
            return Array.Empty<DateOnly>();
        }

        // Effective range: intersection of [StartDate, EndDate??max] with [from, to]
        DateOnly effectiveFrom = from > rule.StartDate ? from : rule.StartDate;
        DateOnly effectiveTo = rule.EndDate is { } end && end < to ? end : to;
        if (effectiveTo < effectiveFrom)
        {
            return Array.Empty<DateOnly>();
        }

        int monthInterval = FrequencyToMonths(rule.Frequency);
        var occurrences = new List<DateOnly>();

        // First occurrence is the first aligned date >= effectiveFrom
        // based on the rule's StartDate.
        DateOnly current = AlignedFirstOccurrenceOnOrAfter(rule.StartDate, effectiveFrom, monthInterval);

        while (current <= effectiveTo)
        {
            occurrences.Add(current);
            current = AddMonthsClamped(current, monthInterval);
        }

        return occurrences;
    }

    /// <summary>
    /// Determina la proxima fecha de generacion a partir de una
    /// ocurrencia generada (o saltada) recientemente. Usado por
    /// <c>GenerateDueRecurringUseCase</c> despues de generar.
    /// </summary>
    /// <param name="rule">Regla recurrente.</param>
    /// <param name="lastGeneratedDate">
    /// Fecha de la ultima ocurrencia generada. Null si nunca se ha generado.
    /// </param>
    /// <returns>Proxima fecha en que la regla debe dispararse.</returns>
    public DateOnly NextGenerationDate(RecurringRule rule, DateOnly? lastGeneratedDate)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (lastGeneratedDate is null)
        {
            return rule.StartDate;
        }

        int monthInterval = FrequencyToMonths(rule.Frequency);
        return AddMonthsClamped(lastGeneratedDate.Value, monthInterval);
    }

    private static int FrequencyToMonths(string frequency) => frequency switch
    {
        nameof(RecurringFrequency.Monthly) => 1,
        nameof(RecurringFrequency.Bimonthly) => 2,
        nameof(RecurringFrequency.Yearly) => 12,
        _ => 1, // default
    };

    /// <summary>
    /// Devuelve la primera fecha de ocurrencia de la regla que sea
    /// &gt;= <paramref name="from"/>. Si la regla empezo en X y X es
    /// anterior a <paramref name="from"/>, se avanza en pasos de
    /// <paramref name="monthInterval"/> meses hasta alcanzar o
    /// superar <paramref name="from"/>.
    /// </summary>
    private static DateOnly AlignedFirstOccurrenceOnOrAfter(
        DateOnly startDate,
        DateOnly from,
        int monthInterval)
    {
        if (startDate >= from)
        {
            return startDate;
        }

        int monthsBetween =
            (from.Year - startDate.Year) * 12 + (from.Month - startDate.Month);
        int steps = (int)Math.Ceiling(monthsBetween / (double)monthInterval);
        if (steps <= 0)
        {
            steps = 1;
        }

        return AddMonthsClamped(startDate, steps * monthInterval);
    }

    /// <summary>
    /// Suma meses a una fecha. Si la fecha original cae en el ultimo
    /// dia de su mes, se preserva la "intencion" de fin de mes
    /// (e.g. 31 de enero + 1 mes = 28 de febrero, luego 28 de febrero
    /// + 1 mes = 31 de marzo, no 28). Esto evita que un dia de pago
    /// "31" se "deslice" permanentemente al 28.
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
