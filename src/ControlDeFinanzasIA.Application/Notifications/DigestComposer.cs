using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.Recurring;

namespace ControlDeFinanzasIA.Application.Notifications;

/// <summary>
/// Compone el aviso de resumen periódico (<see cref="NotificationKind.Digest"/>,
/// F-017, R-13, D-12) a partir de los resultados completos de las fuentes.
/// Puro: no hace I/O ni tiene DI. Los conteos son independientes de los
/// toggles por tipo y de los avisos descartados — el resumen tiene su propio
/// interruptor (Spec Reconciliation #8).
/// </summary>
public static class DigestComposer
{
    /// <summary>
    /// Arma el resumen del período actual, o <see langword="null"/> si todos
    /// los conteos son cero (en ese caso el caller igual avanza el cursor).
    /// </summary>
    /// <param name="frequency">Frecuencia configurada (<see cref="DigestFrequency.Daily"/> o <see cref="DigestFrequency.Weekly"/>).</param>
    /// <param name="today">Fecha de referencia de la corrida.</param>
    /// <param name="now">Momento UTC de creación del candidato.</param>
    /// <param name="payments">Cuotas de deuda pendientes leídas de la fuente.</param>
    /// <param name="rules">Reglas recurrentes leídas de la fuente.</param>
    /// <param name="budgetLines">Líneas de presupuesto del mes actual.</param>
    /// <param name="alerts">Alertas de anomalía leídas de la fuente.</param>
    /// <returns>El candidato <see cref="NotificationKind.Digest"/>, o <see langword="null"/> si no hay nada que resumir.</returns>
    /// <remarks>
    /// <para>
    /// <b>Pagos:</b> cuotas pendientes más cargos recurrentes activos y no
    /// terminados cuya fecha cae en <c>[today, <see cref="DigestPeriod.WindowEnd"/>]</c>
    /// (solo hacia adelante: lo vencido queda fuera). <b>Totales:</b> por
    /// moneda, ordenados por <see cref="Currency"/>; nunca se
    /// suman monedas distintas sin conversión. <b>Presupuestos:</b> líneas en
    /// Near o Exceeded. <b>Anomalías:</b> alertas sin reconocer cuya fecha cae
    /// en el mes actual.
    /// </para>
    /// </remarks>
    public static Notification? Compose(
        DigestFrequency frequency,
        DateOnly today,
        DateTime now,
        IReadOnlyList<DebtPayment> payments,
        IReadOnlyList<RecurringRule> rules,
        IReadOnlyList<BudgetVsActualLine> budgetLines,
        IReadOnlyList<AnomalyAlert> alerts)
    {
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(budgetLines);
        ArgumentNullException.ThrowIfNull(alerts);

        DateOnly periodStart = DigestPeriod.Start(frequency, today);
        DateOnly windowEnd = DigestPeriod.WindowEnd(frequency, today);

        var totalsByCurrency = new SortedDictionary<Currency, decimal>();
        int paymentCount = 0;

        foreach (DebtPayment payment in payments)
        {
            if (payment.Status != DebtPaymentStatus.Pending
                || payment.ScheduledDate < today
                || payment.ScheduledDate > windowEnd)
            {
                continue;
            }

            paymentCount++;
            AddToTotal(totalsByCurrency, payment.Amount.Currency, payment.Amount.Amount);
        }

        foreach (RecurringRule rule in rules)
        {
            if (!rule.Active
                || rule.NextGenerationDate < today
                || rule.NextGenerationDate > windowEnd
                || (rule.EndDate is { } endDate && endDate < rule.NextGenerationDate))
            {
                continue;
            }

            paymentCount++;
            AddToTotal(totalsByCurrency, rule.Amount.Currency, rule.Amount.Amount);
        }

        int budgetCount = budgetLines.Count(line => line.Status is BudgetStatus.Near or BudgetStatus.Exceeded);
        int anomalyCount = alerts.Count(alert =>
            !alert.IsAcknowledged && alert.Date.Year == today.Year && alert.Date.Month == today.Month);

        if (paymentCount == 0 && budgetCount == 0 && anomalyCount == 0)
        {
            return null;
        }

        var totals = totalsByCurrency
            .Select(entry => new NotificationCurrencyTotal(entry.Key, entry.Value))
            .ToList();

        return Notification.ForDigest(frequency, periodStart, paymentCount, totals, budgetCount, anomalyCount, now);
    }

    private static void AddToTotal(SortedDictionary<Currency, decimal> totals, Currency currency, decimal amount)
        => totals[currency] = totals.TryGetValue(currency, out decimal current) ? current + amount : amount;
}
