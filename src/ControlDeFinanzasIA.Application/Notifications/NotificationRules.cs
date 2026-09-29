using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.Recurring;

namespace ControlDeFinanzasIA.Application.Notifications;

/// <summary>
/// Evaluadores puros que convierten filas de origen en candidatos
/// <see cref="Notification"/> (F-017, D-3). No hacen I/O ni tienen DI: los
/// datos ya fueron leídos por el caller (<c>DetectNotificationsUseCase</c>).
/// </summary>
public static class NotificationRules
{
    /// <summary>Tamaño de la ventana hacia adelante, en días (D8).</summary>
    public const int DueHorizonDays = 3;

    /// <summary>Días de mora hacia atrás que aún se notifican, solo cuotas de deuda (D8).</summary>
    public const int OverdueLookbackDays = 7;

    /// <summary>Días de retención antes de purgar una fila (R-9), espejo de <see cref="NotificationReconciler.RetentionDays"/>.</summary>
    public const int RetentionDays = 60;

    /// <summary>Máximo de filas mostradas en el flyout (R-10).</summary>
    public const int FlyoutCap = 20;

    /// <summary>
    /// Evalúa cuotas de deuda (R-4): califica una <see cref="DebtPayment"/>
    /// <see cref="DebtPaymentStatus.Pending"/> cuya <see cref="DebtPayment.ScheduledDate"/>
    /// cae en <c>[today - <see cref="OverdueLookbackDays"/>, today + <see cref="DueHorizonDays"/>]</c>,
    /// inclusivo en ambos extremos. El caller ya filtra por este mismo rango
    /// vía <c>IDebtPaymentRepository.GetPendingByDateRangeAsync</c>; este
    /// método re-verifica la ventana y el estado a la defensiva.
    /// </summary>
    /// <param name="payments">Cuotas candidatas a evaluar.</param>
    /// <param name="debtLabels">
    /// Mapa <c>DebtId</c> → <c>Debt.Description</c> (etiqueta del sujeto). Una
    /// cuota cuya deuda no aparece en el mapa produce una etiqueta vacía.
    /// </param>
    /// <param name="today">Fecha de referencia de la corrida.</param>
    /// <param name="now">Momento UTC de creación para los candidatos nuevos.</param>
    /// <returns>Los candidatos <see cref="NotificationKind.DebtInstallmentDue"/> calificados.</returns>
    public static IReadOnlyList<Notification> DebtInstallments(
        IReadOnlyList<DebtPayment> payments,
        IReadOnlyDictionary<Guid, string> debtLabels,
        DateOnly today,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(debtLabels);

        DateOnly windowStart = today.AddDays(-OverdueLookbackDays);
        DateOnly windowEnd = today.AddDays(DueHorizonDays);

        var candidates = new List<Notification>();

        foreach (DebtPayment payment in payments)
        {
            if (payment.Status != DebtPaymentStatus.Pending)
            {
                continue;
            }

            if (payment.ScheduledDate < windowStart || payment.ScheduledDate > windowEnd)
            {
                continue;
            }

            string label = debtLabels.TryGetValue(payment.DebtId, out string? debtLabel) ? debtLabel : string.Empty;

            candidates.Add(Notification.ForDebtInstallment(
                payment.Id, payment.ScheduledDate, label, payment.Amount, now));
        }

        return candidates;
    }

    /// <summary>
    /// Evalúa cargos recurrentes (R-4, Reconciliation #5): califica una
    /// <see cref="RecurringRule"/> activa cuya <see cref="RecurringRule.NextGenerationDate"/>
    /// cae en <c>[today, today + <see cref="DueHorizonDays"/>]</c>, inclusivo
    /// en ambos extremos, sin mirar hacia atrás. Una regla cuyo
    /// <see cref="RecurringRule.EndDate"/> es anterior a su
    /// <see cref="RecurringRule.NextGenerationDate"/> nunca volverá a
    /// generar y queda excluida aunque siga <see cref="RecurringRule.Active"/>.
    /// </summary>
    /// <param name="rules">Reglas candidatas a evaluar.</param>
    /// <param name="today">Fecha de referencia de la corrida.</param>
    /// <param name="now">Momento UTC de creación para los candidatos nuevos.</param>
    /// <returns>Los candidatos <see cref="NotificationKind.RecurringChargeDue"/> calificados.</returns>
    public static IReadOnlyList<Notification> RecurringCharges(
        IReadOnlyList<RecurringRule> rules,
        DateOnly today,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(rules);

        DateOnly windowEnd = today.AddDays(DueHorizonDays);

        var candidates = new List<Notification>();

        foreach (RecurringRule rule in rules)
        {
            if (!rule.Active)
            {
                continue;
            }

            if (rule.NextGenerationDate < today || rule.NextGenerationDate > windowEnd)
            {
                continue;
            }

            if (rule.EndDate is { } endDate && endDate < rule.NextGenerationDate)
            {
                continue;
            }

            candidates.Add(Notification.ForRecurringCharge(
                rule.Id, rule.NextGenerationDate, rule.Name, rule.Amount, now));
        }

        return candidates;
    }
}
