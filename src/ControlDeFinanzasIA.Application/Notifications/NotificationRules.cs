using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.Budgets;
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

    /// <summary>
    /// Evalúa presupuestos del mes actual (R-5, Reconciliation #6): una línea
    /// con <see cref="BudgetStatus.Exceeded"/> produce un candidato
    /// <see cref="NotificationTier.Exceeded"/> y una con
    /// <see cref="BudgetStatus.Near"/> uno <see cref="NotificationTier.Near"/>
    /// — nunca ambos para el mismo presupuesto (solo el nivel más alto). Una
    /// línea indeterminada (<see cref="BudgetVsActualLine.IsIndeterminate"/>,
    /// p. ej. tasa de cambio faltante) no produce candidato y su
    /// <see cref="BudgetVsActualLine.BudgetId"/> se reporta en
    /// <see cref="BudgetNotificationOutcome.UnknownBudgetIds"/>: "no se puede
    /// evaluar" no es "ya no califica".
    /// </summary>
    /// <param name="lines">Líneas de <c>GetBudgetVsActualQuery</c> del mes actual.</param>
    /// <param name="monthStart">Primer día del mes evaluado (fecha de referencia del aviso).</param>
    /// <param name="now">Momento UTC de creación para los candidatos nuevos.</param>
    /// <returns>Los candidatos calificados y los ids de presupuestos no evaluables.</returns>
    public static BudgetNotificationOutcome Budgets(
        IReadOnlyList<BudgetVsActualLine> lines,
        DateOnly monthStart,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var candidates = new List<Notification>();
        var unknownBudgetIds = new HashSet<Guid>();

        foreach (BudgetVsActualLine line in lines)
        {
            if (line.Status is not { } status || line.PercentConsumed is not { } percent)
            {
                unknownBudgetIds.Add(line.BudgetId);
                continue;
            }

            NotificationTier? tier = status switch
            {
                BudgetStatus.Exceeded => NotificationTier.Exceeded,
                BudgetStatus.Near => NotificationTier.Near,
                _ => null,
            };

            if (tier is null)
            {
                continue;
            }

            candidates.Add(Notification.ForBudget(
                line.BudgetId, monthStart, line.CategoryName, percent, tier.Value, now));
        }

        return new BudgetNotificationOutcome(candidates, unknownBudgetIds);
    }

    /// <summary>
    /// Evalúa alertas de anomalía (R-6, Reconciliation #4): califica una
    /// <see cref="AnomalyAlert"/> no reconocida cuya <see cref="AnomalyAlert.Date"/>
    /// es a lo sumo <see cref="RetentionDays"/> días anterior a
    /// <paramref name="today"/>. Sin ese límite, una alerta más vieja sería
    /// purgada y recreada en cada corrida, rompiendo la garantía de que una
    /// purga nunca resucita un aviso.
    /// </summary>
    /// <param name="alerts">Alertas de anomalía existentes.</param>
    /// <param name="categoryNames">
    /// Mapa <c>CategoryId</c> → nombre (etiqueta del sujeto). Una categoría
    /// ausente produce una etiqueta vacía.
    /// </param>
    /// <param name="today">Fecha de referencia de la corrida.</param>
    /// <param name="now">Momento UTC de creación para los candidatos nuevos.</param>
    /// <returns>Los candidatos <see cref="NotificationKind.Anomaly"/> calificados.</returns>
    public static IReadOnlyList<Notification> Anomalies(
        IReadOnlyList<AnomalyAlert> alerts,
        IReadOnlyDictionary<Guid, string> categoryNames,
        DateOnly today,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(categoryNames);

        DateOnly earliest = today.AddDays(-RetentionDays);

        var candidates = new List<Notification>();

        foreach (AnomalyAlert alert in alerts)
        {
            if (alert.IsAcknowledged || alert.Date < earliest)
            {
                continue;
            }

            string label = categoryNames.TryGetValue(alert.CategoryId, out string? categoryName)
                ? categoryName
                : string.Empty;

            candidates.Add(Notification.ForAnomaly(alert.Id, alert.Date, label, now));
        }

        return candidates;
    }
}

/// <summary>
/// Resultado de <see cref="NotificationRules.Budgets"/>: los candidatos
/// <see cref="NotificationKind.BudgetThreshold"/> del mes y los ids de los
/// presupuestos que no pudieron evaluarse (línea indeterminada). El caller
/// convierte estos últimos en claves protegidas para el reconciliador, de
/// modo que un presupuesto no evaluable conserve sus avisos existentes.
/// </summary>
/// <param name="Candidates">Candidatos de presupuesto (solo el nivel más alto por presupuesto).</param>
/// <param name="UnknownBudgetIds">Ids de presupuestos con línea indeterminada.</param>
public sealed record BudgetNotificationOutcome(
    IReadOnlyList<Notification> Candidates,
    IReadOnlySet<Guid> UnknownBudgetIds);
