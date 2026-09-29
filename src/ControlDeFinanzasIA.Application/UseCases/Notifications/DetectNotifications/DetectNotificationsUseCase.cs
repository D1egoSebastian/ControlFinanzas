using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Notifications;
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
/// Use case que detecta y reconcilia avisos (F-017, slices S3-S4, D-5):
/// cuotas de deuda próximas o vencidas
/// (<see cref="NotificationKind.DebtInstallmentDue"/>), cargos recurrentes
/// próximos a generarse (<see cref="NotificationKind.RecurringChargeDue"/>),
/// presupuestos en Near/Exceeded del mes actual
/// (<see cref="NotificationKind.BudgetThreshold"/>) y anomalías sin
/// reconocer (<see cref="NotificationKind.Anomaly"/>). El tipo
/// <c>Digest</c> se cablea en el slice S7.
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

        var candidates = new List<Notification>();

        if (settings.DebtDue)
        {
            var window = DateRange.Create(
                today.AddDays(-NotificationRules.OverdueLookbackDays),
                today.AddDays(NotificationRules.DueHorizonDays));

            IReadOnlyList<DebtPayment> payments = await _debtPaymentRepository
                .GetPendingByDateRangeAsync(window, ct)
                .ConfigureAwait(false);
            IReadOnlyList<Debt> debts = await _debtRepository.GetAllAsync(ct).ConfigureAwait(false);
            var debtLabels = debts.ToDictionary(d => d.Id, d => d.Description);

            candidates.AddRange(NotificationRules.DebtInstallments(payments, debtLabels, today, now));
        }

        if (settings.RecurringDue)
        {
            IReadOnlyList<RecurringRule> rules = await _recurringRuleRepository
                .GetAllAsync(ct)
                .ConfigureAwait(false);

            candidates.AddRange(NotificationRules.RecurringCharges(rules, today, now));
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

        bool budgetsEvaluable = true;

        if (settings.Budget)
        {
            DateOnly monthStart = MonthPeriod.Anchor(today);
            Result budgetResult = await _budgetVsActualQuery
                .ExecuteAsync(today, ct)
                .ConfigureAwait(false);

            if (budgetResult.IsSuccess && budgetResult.Value is BudgetVsActualResult budgetVsActual)
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
            else
            {
                // Sin lectura fiable no se puede afirmar que un aviso dejó de
                // calificar: se omite el kind y se conservan sus filas.
                budgetsEvaluable = false;
                _logger.LogWarning(
                    "Budget notification source could not be read: {ErrorCode} {ErrorMessage}",
                    budgetResult.Error?.Code,
                    budgetResult.Error?.Message);
            }
        }

        if (budgetsEvaluable)
        {
            evaluatedKinds.Add(NotificationKind.BudgetThreshold);
        }

        if (settings.Anomaly)
        {
            IReadOnlyList<AnomalyAlert> alerts = await _anomalyAlertRepository
                .GetAllAsync(ct)
                .ConfigureAwait(false);
            IReadOnlyList<Category> categories = await _categoryRepository
                .GetAllAsync(ct)
                .ConfigureAwait(false);
            var categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);

            candidates.AddRange(NotificationRules.Anomalies(alerts, categoryNames, today, now));
        }

        NotificationReconciliationPlan plan = NotificationReconciler.Plan(
            existing, candidates, evaluatedKinds, protectedKeys, today, digestDue: false);

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

        return Result.Success(new DetectNotificationsSummary(plan.ToAdd.Count, plan.ToUpdate.Count, plan.ToRemove.Count));
    }
}
