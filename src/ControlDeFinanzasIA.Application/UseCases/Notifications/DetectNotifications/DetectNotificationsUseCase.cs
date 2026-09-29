using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Notifications;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Notifications.DetectNotifications;

/// <summary>
/// Use case que detecta y reconcilia avisos (F-017, slices S3-S4 y S7, D-5):
/// cuotas de deuda próximas o vencidas
/// (<see cref="NotificationKind.DebtInstallmentDue"/>), cargos recurrentes
/// próximos a generarse (<see cref="NotificationKind.RecurringChargeDue"/>),
/// presupuestos en Near/Exceeded del mes actual
/// (<see cref="NotificationKind.BudgetThreshold"/>), anomalías sin
/// reconocer (<see cref="NotificationKind.Anomaly"/>) y, una vez por período,
/// el resumen periódico (<see cref="NotificationKind.Digest"/>, D-12).
/// </summary>
/// <remarks>
/// Sin dependencia de <c>DetectAnomaliesUseCase</c> ni de ningún cliente
/// LLM (R-6). Cada lectura habilitada se espera secuencialmente — nunca
/// vía <c>Task.WhenAll</c> (R-15) — porque el caller (el centro de
/// notificaciones) ejecuta este use case dentro de su propio
/// <c>IServiceScopeFactory.CreateScope()</c> y ese <c>AppDbContext</c> no
/// admite operaciones concurrentes. Esta clase no atrapa excepciones: el
/// centro las atrapa, registra y descarta (R-15).
/// </remarks>
public sealed class DetectNotificationsUseCase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IDebtPaymentRepository _debtPaymentRepository;
    private readonly IDebtRepository _debtRepository;
    private readonly IRecurringRuleRepository _recurringRuleRepository;
    private readonly GetBudgetVsActualQuery _budgetVsActualQuery;
    private readonly IAnomalyAlertRepository _anomalyAlertRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISettingsStore _settingsStore;
    private readonly IClock _clock;
    private readonly ILogger<DetectNotificationsUseCase> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public DetectNotificationsUseCase(
        INotificationRepository notificationRepository,
        IDebtPaymentRepository debtPaymentRepository,
        IDebtRepository debtRepository,
        IRecurringRuleRepository recurringRuleRepository,
        GetBudgetVsActualQuery budgetVsActualQuery,
        IAnomalyAlertRepository anomalyAlertRepository,
        ICategoryRepository categoryRepository,
        ISettingsStore settingsStore,
        IClock clock,
        ILogger<DetectNotificationsUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(notificationRepository);
        ArgumentNullException.ThrowIfNull(debtPaymentRepository);
        ArgumentNullException.ThrowIfNull(debtRepository);
        ArgumentNullException.ThrowIfNull(recurringRuleRepository);
        ArgumentNullException.ThrowIfNull(budgetVsActualQuery);
        ArgumentNullException.ThrowIfNull(anomalyAlertRepository);
        ArgumentNullException.ThrowIfNull(categoryRepository);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _notificationRepository = notificationRepository;
        _debtPaymentRepository = debtPaymentRepository;
        _debtRepository = debtRepository;
        _recurringRuleRepository = recurringRuleRepository;
        _budgetVsActualQuery = budgetVsActualQuery;
        _anomalyAlertRepository = anomalyAlertRepository;
        _categoryRepository = categoryRepository;
        _settingsStore = settingsStore;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta una corrida de detección y reconciliación.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con un <see cref="DetectNotificationsSummary"/>
    /// en <see cref="Result.Value"/> si la reconciliación tuvo éxito (o no
    /// tenía cambios que aplicar), o <c>Failure("NotificationReconcileConflict", …)</c>
    /// si <see cref="INotificationRepository.ApplyReconciliationAsync"/> falla.
    /// </returns>
    public async Task<Result> ExecuteAsync(CancellationToken ct = default)
    {
        DateOnly today = _clock.Today;
        DateTime now = _clock.UtcNow;

        NotificationSettings settings = await NotificationSettingsReader
            .ReadAsync(_settingsStore, ct)
            .ConfigureAwait(false);
        IReadOnlyList<Notification> existing = await _notificationRepository
            .GetAllAsync(ct)
            .ConfigureAwait(false);

        // Resumen periódico (D-5 paso 3, D-12): vence cuando el cursor está atrás del inicio
        // del período actual. Una frecuencia Off nunca vence y no toca el cursor.
        DigestFrequency digestFrequency = settings.Digest;
        DateOnly periodStart = default;
        bool digestDue = false;

        if (digestFrequency != DigestFrequency.Off)
        {
            periodStart = DigestPeriod.Start(digestFrequency, today);
            string? rawCursor = await _settingsStore
                .GetStringAsync(SettingKeys.NotificationsDigestLastPeriodStart, defaultValue: null, ct)
                .ConfigureAwait(false);
            DateOnly? cursor = NotificationSettingsReader.ParseCursor(rawCursor);
            digestDue = cursor is null || cursor.Value < periodStart;
        }

        var candidates = new List<Notification>();

        // Cuando el resumen vence, las cuatro fuentes se leen completas aunque su toggle
        // esté apagado: el resumen tiene su propio interruptor (Reconciliation #8).
        IReadOnlyList<DebtPayment> digestPayments = [];
        IReadOnlyList<RecurringRule> digestRules = [];
        IReadOnlyList<BudgetVsActualLine> digestBudgetLines = [];
        IReadOnlyList<AnomalyAlert> digestAlerts = [];

        if (settings.DebtDue || digestDue)
        {
            int horizonDays = NotificationRules.DueHorizonDays;
            if (digestDue)
            {
                int digestHorizonDays = DigestPeriod.WindowEnd(digestFrequency, today).DayNumber - today.DayNumber;
                horizonDays = Math.Max(horizonDays, digestHorizonDays);
            }

            var window = DateRange.Create(
                today.AddDays(-NotificationRules.OverdueLookbackDays),
                today.AddDays(horizonDays));

            IReadOnlyList<DebtPayment> payments = await _debtPaymentRepository
                .GetPendingByDateRangeAsync(window, ct)
                .ConfigureAwait(false);
            digestPayments = payments;

            if (settings.DebtDue)
            {
                IReadOnlyList<Debt> debts = await _debtRepository.GetAllAsync(ct).ConfigureAwait(false);
                var debtLabels = debts.ToDictionary(d => d.Id, d => d.Description);

                candidates.AddRange(NotificationRules.DebtInstallments(payments, debtLabels, today, now));
            }
        }

        if (settings.RecurringDue || digestDue)
        {
            IReadOnlyList<RecurringRule> rules = await _recurringRuleRepository
                .GetAllAsync(ct)
                .ConfigureAwait(false);
            digestRules = rules;

            if (settings.RecurringDue)
            {
                candidates.AddRange(NotificationRules.RecurringCharges(rules, today, now));
            }
        }

        // Deuda, recurrentes y anomalías se evalúan siempre: un toggle apagado
        // equivale a "evaluado con cero candidatos" y sus filas no descartadas
        // se eliminan (S-7). Presupuestos se suma abajo salvo lectura fallida.
        var evaluatedKinds = new HashSet<NotificationKind>
        {
            NotificationKind.DebtInstallmentDue,
            NotificationKind.RecurringChargeDue,
            NotificationKind.Anomaly,
        };
        var protectedKeys = new HashSet<string>(StringComparer.Ordinal);

        bool budgetReadFailed = false;

        if (settings.Budget || digestDue)
        {
            DateOnly monthStart = MonthPeriod.Anchor(today);
            Result budgetResult = await _budgetVsActualQuery
                .ExecuteAsync(today, ct)
                .ConfigureAwait(false);

            if (budgetResult.IsSuccess && budgetResult.Value is BudgetVsActualResult budgetVsActual)
            {
                digestBudgetLines = budgetVsActual.Lines;

                if (settings.Budget)
                {
                    BudgetNotificationOutcome outcome = NotificationRules.Budgets(budgetVsActual.Lines, monthStart, now);
                    candidates.AddRange(outcome.Candidates);

                    // "No se puede evaluar" (tasa faltante) no es "ya no califica":
                    // se protegen ambas claves de nivel del presupuesto indeterminado.
                    foreach (Guid unknownBudgetId in outcome.UnknownBudgetIds)
                    {
                        protectedKeys.Add(NotificationKeys.ForBudget(unknownBudgetId, monthStart, NotificationTier.Near));
                        protectedKeys.Add(NotificationKeys.ForBudget(unknownBudgetId, monthStart, NotificationTier.Exceeded));
                    }
                }
            }
            else
            {
                // Sin lectura fiable no se puede afirmar que un aviso dejó de
                // calificar: se omite el kind y se conservan sus filas.
                budgetReadFailed = true;
                _logger.LogWarning(
                    "Budget notification source could not be read: {ErrorCode} {ErrorMessage}",
                    budgetResult.Error?.Code,
                    budgetResult.Error?.Message);
            }
        }

        if (!(settings.Budget && budgetReadFailed))
        {
            evaluatedKinds.Add(NotificationKind.BudgetThreshold);
        }

        if (settings.Anomaly || digestDue)
        {
            IReadOnlyList<AnomalyAlert> alerts = await _anomalyAlertRepository
                .GetAllAsync(ct)
                .ConfigureAwait(false);
            digestAlerts = alerts;

            if (settings.Anomaly)
            {
                IReadOnlyList<Category> categories = await _categoryRepository
                    .GetAllAsync(ct)
                    .ConfigureAwait(false);
                var categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);

                candidates.AddRange(NotificationRules.Anomalies(alerts, categoryNames, today, now));
            }
        }

        // Un resumen se compone solo con lecturas completas: si presupuestos no se pudo leer,
        // el resumen se omite esta corrida (sin cursor) y se reintenta en la siguiente.
        bool digestComputable = digestDue && !budgetReadFailed;

        if (digestComputable)
        {
            Notification? digest = DigestComposer.Compose(
                digestFrequency, today, now, digestPayments, digestRules, digestBudgetLines, digestAlerts);

            if (digest is not null)
            {
                candidates.Add(digest);
            }
        }

        // Digest se evalúa cuando vence (cero candidatos si todos los conteos son cero) o cuando
        // la frecuencia es Off (sus filas no descartadas se eliminan).
        if (digestComputable || digestFrequency == DigestFrequency.Off)
        {
            evaluatedKinds.Add(NotificationKind.Digest);
        }

        NotificationReconciliationPlan plan = NotificationReconciler.Plan(
            existing, candidates, evaluatedKinds, protectedKeys, today, digestDue: digestComputable);

        if (!plan.IsEmpty)
        {
            Result applyResult = await _notificationRepository
                .ApplyReconciliationAsync(plan, ct)
                .ConfigureAwait(false);

            if (!applyResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Notification reconciliation failed: {ErrorCode} {ErrorMessage}",
                    applyResult.Error?.Code,
                    applyResult.Error?.Message);

                return Result.Failure(
                    "NotificationReconcileConflict",
                    applyResult.Error?.Message ?? "Notification reconciliation failed.");
            }
        }

        if (digestComputable)
        {
            // El cursor solo avanza tras una reconciliación exitosa y aun con todos los conteos en
            // cero (R-13). Si la escritura falla, la próxima corrida replanea y el DedupKey evita
            // duplicar la fila.
            Result cursorResult = await _settingsStore
                .SetAsync(
                    SettingKeys.NotificationsDigestLastPeriodStart,
                    periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ct)
                .ConfigureAwait(false);

            if (!cursorResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Digest cursor could not be persisted: {ErrorCode} {ErrorMessage}",
                    cursorResult.Error?.Code,
                    cursorResult.Error?.Message);
            }
        }

        return Result.Success(new DetectNotificationsSummary(plan.ToAdd.Count, plan.ToUpdate.Count, plan.ToRemove.Count));
    }
}
