using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Recurring;

/// <summary>
/// Servicio de dominio puro que calcula las fechas de ocurrencia
/// de una regla recurrente. Stateless: misma entrada, misma salida.
/// </summary>
/// <remarks>
/// <para>
/// Estrategia de borde de mes: el dia de la fecha de inicio es el ancla. Si la regla tiene dia 31 y el mes
/// destino tiene 28/29/30 dias, se usa el ultimo dia del mes
/// destino. Esto es consistente con la convencion de varios
/// sistemas de facturacion (Netflix cobra el 28 de feb si la suscripcion
/// empezo el 31 de enero). Cada ocurrencia se calcula desde el ancla, sin encadenar, asi que un cobro del 30
    /// vuelve al 30 despues de febrero en lugar de deslizarse al fin de mes.
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

        int monthInterval = RecurringSchedule.MonthsOf(rule.Frequency);
        int anchorDay = rule.StartDate.Day;
        var occurrences = new List<DateOnly>();

        // Every occurrence is computed from the anchor (the day of the start date), never chained from the previous
        // one, so a day 30 charge clamped to February 28 returns to the 30th in March instead of drifting.
        int index = FirstIndexOnOrAfter(rule.StartDate, anchorDay, effectiveFrom, monthInterval);
        DateOnly current = Nth(rule.StartDate, anchorDay, index, monthInterval);
        while (current <= effectiveTo)
        {
            occurrences.Add(current);
            index++;
            current = Nth(rule.StartDate, anchorDay, index, monthInterval);
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

        return RecurringSchedule.AddMonthsAnchored(rule.StartDate.Day, lastGeneratedDate.Value, RecurringSchedule.MonthsOf(rule.Frequency));
    }

    private static DateOnly Nth(DateOnly startDate, int anchorDay, int index, int monthInterval) =>
        RecurringSchedule.AddMonthsAnchored(anchorDay, startDate, index * monthInterval);

    /// <summary>
    /// Devuelve el indice de la primera ocurrencia de la regla que sea
    /// &gt;= <paramref name="from"/> (0 es la fecha de inicio).
    /// </summary>
    private static int FirstIndexOnOrAfter(DateOnly startDate, int anchorDay, DateOnly from, int monthInterval)
    {
        if (startDate >= from)
        {
            return 0;
        }

        int monthsBetween = ((from.Year - startDate.Year) * 12) + (from.Month - startDate.Month);
        int index = Math.Max(0, monthsBetween / monthInterval);
        while (Nth(startDate, anchorDay, index, monthInterval) < from)
        {
            index++;
        }

        while (index > 0 && Nth(startDate, anchorDay, index - 1, monthInterval) >= from)
        {
            index--;
        }

        return index;
    }
}
