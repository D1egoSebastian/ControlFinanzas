using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Notifications.DetectNotifications;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Notifications.DetectNotifications;

/// <summary>
/// Unit tests for <see cref="DetectNotificationsUseCase"/> (F-017, slice
/// S3, D-5) covering the due-item scope: sequential reads (R-15), toggle-
/// gated sources (R-12), a reconcile-save failure, and an end-to-end pass
/// producing <c>ToAdd</c>/<c>ToUpdate</c>/<c>ToRemove</c>.
/// </summary>
public sealed class DetectNotificationsUseCaseTests
{
    private static readonly DateOnly _today = new(2026, 9, 26);
    private static readonly DateTime _fixedCreatedAt = new(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_BothSourcesEnabled_ReadsEachSourceSequentiallyNeverOverlapping()
    {
        var guard = new ConcurrencyGuard();
        var debtPayments = new FakeDebtPaymentRepository();
        var debts = new FakeDebtRepository();
        var rules = new FakeRecurringRuleRepository();

        Debt debt = CreateDebt("Préstamo carro", _today.AddDays(1));
        debts.Seed(debt);
        debtPayments.SeedRange(new DebtScheduleGenerator().Generate(debt));
        rules.Seed(CreateRule("Netflix", _today.AddDays(1)));

        var trackingPayments = new TrackingDebtPaymentRepository(debtPayments, guard);
        var trackingDebts = new TrackingDebtRepository(debts, guard);
        var trackingRules = new TrackingRecurringRuleRepository(rules, guard);

        DetectNotificationsUseCase useCase = CreateUseCase(
            debtPaymentRepository: trackingPayments,
            debtRepository: trackingDebts,
            recurringRuleRepository: trackingRules);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.False(guard.OverlapDetected);
    }

    [Fact]
    public async Task ExecuteAsync_DebtDueDisabled_DoesNotReadDebtPaymentSource()
    {
        InMemorySettingsStore settingsStore = NewSettingsStore();
        settingsStore.Seed(SettingKeys.NotificationsDebtDueEnabled, "false");

        Debt debt = CreateDebt("Préstamo carro", _today.AddDays(1));
        var debtPayments = new FakeDebtPaymentRepository();
        debtPayments.SeedRange(new DebtScheduleGenerator().Generate(debt));
        var trackingPayments = new TrackingDebtPaymentRepository(debtPayments, new ConcurrencyGuard());

        DetectNotificationsUseCase useCase = CreateUseCase(
            settingsStore: settingsStore,
            debtPaymentRepository: trackingPayments);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, trackingPayments.GetPendingByDateRangeCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_RecurringDueDisabled_DoesNotReadRecurringRuleSource()
    {
        InMemorySettingsStore settingsStore = NewSettingsStore();
        settingsStore.Seed(SettingKeys.NotificationsRecurringDueEnabled, "false");

        var rules = new FakeRecurringRuleRepository();
        rules.Seed(CreateRule("Netflix", _today.AddDays(1)));
        var trackingRules = new TrackingRecurringRuleRepository(rules, new ConcurrencyGuard());

        DetectNotificationsUseCase useCase = CreateUseCase(
            settingsStore: settingsStore,
            recurringRuleRepository: trackingRules);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, trackingRules.GetAllCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_DisabledKindWithExistingUndismissedRows_RemovesThem()
    {
        InMemorySettingsStore settingsStore = NewSettingsStore();
        settingsStore.Seed(SettingKeys.NotificationsRecurringDueEnabled, "false");

        var notifications = new FakeNotificationRepository();
        var existingRow = Notification.ForRecurringCharge(
            Guid.NewGuid(), _today.AddDays(1), "Netflix", Money.Create(500m, Currency.DOP), _fixedCreatedAt);
        notifications.Seed(existingRow);

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            settingsStore: settingsStore);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(notifications.Notifications, n => n.Id == existingRow.Id);
    }

    [Fact]
    public async Task ExecuteAsync_ReconcileSaveFails_ReturnsFailureAndLogsWarningWithoutThrowing()
    {
        var notifications = new FakeNotificationRepository
        {
            ApplyReconciliationResult = Result.Failure("Conflict", "Unique index violation."),
        };

        Debt debt = CreateDebt("Préstamo carro", _today.AddDays(1));
        var debts = new FakeDebtRepository();
        debts.Seed(debt);
        var debtPayments = new FakeDebtPaymentRepository();
        debtPayments.SeedRange(new DebtScheduleGenerator().Generate(debt));

        var logger = new RecordingLogger<DetectNotificationsUseCase>();
        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            debtPaymentRepository: debtPayments,
            debtRepository: debts,
            logger: logger);

        Result result = await useCase.ExecuteAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("NotificationReconcileConflict", result.Error?.Code);
        Assert.True(logger.HasWarning);
    }

    [Fact]
    public async Task ExecuteAsync_DebtAndRecurringCandidates_ReconcilesAddUpdateAndRemove()
    {
        var debts = new FakeDebtRepository();
        var debtPayments = new FakeDebtPaymentRepository();
        var rules = new FakeRecurringRuleRepository();
        var notifications = new FakeNotificationRepository();

        Debt debt = CreateDebt("Préstamo carro", _today.AddDays(2));
        debts.Seed(debt);
        DebtPayment payment = new DebtScheduleGenerator().Generate(debt)[0];
        debtPayments.Seed(payment);

        RecurringRule rule = CreateRule("Netflix", _today.AddDays(1));
        rules.Seed(rule);

        // Stale amount for the same debt payment key -> refreshed (ToUpdate).
        var staleDebtRow = Notification.ForDebtInstallment(
            payment.Id, payment.ScheduledDate, "Préstamo carro", Money.Create(1m, Currency.DOP), _fixedCreatedAt);
        notifications.Seed(staleDebtRow);

        // No longer qualifies (its rule id isn't a candidate this run) -> removed (ToRemove).
        var orphanRecurringRow = Notification.ForRecurringCharge(
            Guid.NewGuid(), _today.AddDays(-30), "Servicio viejo", Money.Create(20m, Currency.DOP), _fixedCreatedAt);
        notifications.Seed(orphanRecurringRow);

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            debtPaymentRepository: debtPayments,
            debtRepository: debts,
            recurringRuleRepository: rules);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        DetectNotificationsSummary summary = Assert.IsType<DetectNotificationsSummary>(result.Value);
        Assert.Equal(1, summary.Added);
        Assert.Equal(1, summary.Updated);
        Assert.Equal(1, summary.Removed);

        Assert.Contains(notifications.Notifications, n => n.Kind == NotificationKind.RecurringChargeDue && n.SourceId == rule.Id);
        Assert.DoesNotContain(notifications.Notifications, n => n.Id == orphanRecurringRow.Id);
        Notification refreshedDebtRow = notifications.Notifications.Single(n => n.Id == staleDebtRow.Id);
        Assert.Equal(1500m, refreshedDebtRow.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_NoQualifyingSources_ReturnsEmptySummaryWithoutCallingApplyReconciliation()
    {
        var notifications = new FakeNotificationRepository();

        DetectNotificationsUseCase useCase = CreateUseCase(notificationRepository: notifications);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        DetectNotificationsSummary summary = Assert.IsType<DetectNotificationsSummary>(result.Value);
        Assert.Equal(0, summary.Added);
        Assert.Equal(0, summary.Updated);
        Assert.Equal(0, summary.Removed);
        Assert.Equal(0, notifications.ApplyReconciliationCallCount);
    }

    // --- S4: budget + anomaly ---

    [Fact]
    public async Task ExecuteAsync_AllFourKindsQualify_AddsOneCandidatePerKind()
    {
        // S-1: installment due in 2 days, subscription in 1 day, budget at Exceeded, unacknowledged anomaly.
        var debts = new FakeDebtRepository();
        var debtPayments = new FakeDebtPaymentRepository();
        var rules = new FakeRecurringRuleRepository();
        var budgets = new FakeBudgetRepository();
        var expenses = new FakeExpenseRepository();
        var categories = new FakeCategoryRepository();
        var alerts = new FakeAnomalyAlertRepository();
        var notifications = new FakeNotificationRepository();

        Debt debt = CreateDebt("Préstamo carro", _today.AddDays(2));
        debts.Seed(debt);
        debtPayments.Seed(new DebtScheduleGenerator().Generate(debt)[0]);
        rules.Seed(CreateRule("Netflix", _today.AddDays(1)));

        var categoryId = Guid.NewGuid();
        categories.SetCategories([new Category { Id = categoryId, Name = "Comida" }]);
        budgets.Seed(Budget.Create(_today.Year, _today.Month, categoryId, Money.Create(1000m, Currency.DOP)));
        expenses.CategoryNames[categoryId] = "Comida";
        await expenses.AddAsync(CreateExpense(categoryId, 1200m));

        var alert = AnomalyAlert.Create(
            Guid.NewGuid(), categoryId, _today.AddDays(-1), AnomalySeverity.Warning, "Gasto inusual", _fixedCreatedAt);
        alerts.Seed(alert);

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            debtPaymentRepository: debtPayments,
            debtRepository: debts,
            recurringRuleRepository: rules,
            budgetRepository: budgets,
            expenseRepository: expenses,
            categoryRepository: categories,
            anomalyAlertRepository: alerts);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        DetectNotificationsSummary summary = Assert.IsType<DetectNotificationsSummary>(result.Value);
        Assert.Equal(4, summary.Added);
        Assert.Equal(
            new[]
            {
                NotificationKind.DebtInstallmentDue,
                NotificationKind.RecurringChargeDue,
                NotificationKind.BudgetThreshold,
                NotificationKind.Anomaly,
            }.OrderBy(k => k),
            notifications.Notifications.Select(n => n.Kind).OrderBy(k => k));

        Notification budgetRow = notifications.Notifications.Single(n => n.Kind == NotificationKind.BudgetThreshold);
        Assert.Equal(NotificationTier.Exceeded, budgetRow.Tier);
        Assert.Equal("Comida", budgetRow.SubjectLabel);
        Assert.Equal(new DateOnly(_today.Year, _today.Month, 1), budgetRow.ReferenceDate);

        Notification anomalyRow = notifications.Notifications.Single(n => n.Kind == NotificationKind.Anomaly);
        Assert.Equal(alert.Id, anomalyRow.SourceId);
        Assert.Equal("Comida", anomalyRow.SubjectLabel);
    }

    [Fact]
    public async Task ExecuteAsync_BudgetTogglesOff_DoesNotReadBudgetsAndRemovesExistingBudgetRows()
    {
        InMemorySettingsStore settingsStore = NewSettingsStore();
        settingsStore.Seed(SettingKeys.NotificationsBudgetEnabled, "false");

        var budgets = new FakeBudgetRepository();
        var notifications = new FakeNotificationRepository();
        var existing = Notification.ForBudget(
            Guid.NewGuid(), new DateOnly(_today.Year, _today.Month, 1), "Comida", 85m, NotificationTier.Near, _fixedCreatedAt);
        notifications.Seed(existing);

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications, settingsStore: settingsStore, budgetRepository: budgets);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, budgets.GetByMonthCallCount);
        Assert.DoesNotContain(notifications.Notifications, n => n.Id == existing.Id);
    }

    [Fact]
    public async Task ExecuteAsync_AnomalyToggleOff_DoesNotReadAlertsAndRemovesExistingAnomalyRows()
    {
        InMemorySettingsStore settingsStore = NewSettingsStore();
        settingsStore.Seed(SettingKeys.NotificationsAnomalyEnabled, "false");

        var alerts = new TrackingAnomalyAlertRepository(new FakeAnomalyAlertRepository(), new ConcurrencyGuard());
        var notifications = new FakeNotificationRepository();
        var existing = Notification.ForAnomaly(Guid.NewGuid(), _today, "Comida", _fixedCreatedAt);
        notifications.Seed(existing);

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications, settingsStore: settingsStore, anomalyAlertRepository: alerts);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, alerts.GetAllCallCount);
        Assert.DoesNotContain(notifications.Notifications, n => n.Id == existing.Id);
    }

    [Fact]
    public async Task ExecuteAsync_AlertAcknowledgedElsewhere_RemovesItsUndismissedNotification()
    {
        var alerts = new FakeAnomalyAlertRepository();
        var notifications = new FakeNotificationRepository();

        var alert = AnomalyAlert.Create(
            Guid.NewGuid(), Guid.NewGuid(), _today.AddDays(-1), AnomalySeverity.Info, "Gasto inusual", _fixedCreatedAt);
        alert.Acknowledge(_fixedCreatedAt);
        alerts.Seed(alert);

        var existing = Notification.ForAnomaly(alert.Id, alert.Date, "Comida", _fixedCreatedAt);
        notifications.Seed(existing);

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications, anomalyAlertRepository: alerts);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(notifications.Notifications, n => n.Id == existing.Id);
    }

    [Fact]
    public async Task ExecuteAsync_NearThenExceeded_ReplacesUndismissedNearRowWithExceeded()
    {
        var budgets = new FakeBudgetRepository();
        var expenses = new FakeExpenseRepository();
        var categories = new FakeCategoryRepository();
        var notifications = new FakeNotificationRepository();

        var categoryId = Guid.NewGuid();
        categories.SetCategories([new Category { Id = categoryId, Name = "Comida" }]);
        var budget = Budget.Create(_today.Year, _today.Month, categoryId, Money.Create(1000m, Currency.DOP));
        budgets.Seed(budget);
        expenses.CategoryNames[categoryId] = "Comida";
        await expenses.AddAsync(CreateExpense(categoryId, 1050m));

        var monthStart = new DateOnly(_today.Year, _today.Month, 1);
        var nearRow = Notification.ForBudget(
            budget.Id, monthStart, "Comida", 85m, NotificationTier.Near, _fixedCreatedAt);
        notifications.Seed(nearRow);

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            budgetRepository: budgets,
            expenseRepository: expenses,
            categoryRepository: categories);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Notification only = Assert.Single(notifications.Notifications);
        Assert.Equal(NotificationTier.Exceeded, only.Tier);
        Assert.NotEqual(nearRow.Id, only.Id);
    }

    [Fact]
    public async Task ExecuteAsync_IndeterminateBudget_KeepsItsExistingNotification()
    {
        var budgets = new FakeBudgetRepository();
        var categories = new FakeCategoryRepository();
        var notifications = new FakeNotificationRepository();

        var categoryId = Guid.NewGuid();
        categories.SetCategories([new Category { Id = categoryId, Name = "Viajes" }]);
        var budget = Budget.Create(_today.Year, _today.Month, categoryId, Money.Create(500m, Currency.USD));
        budgets.Seed(budget);

        var monthStart = new DateOnly(_today.Year, _today.Month, 1);
        var nearRow = Notification.ForBudget(
            budget.Id, monthStart, "Viajes", 85m, NotificationTier.Near, _fixedCreatedAt);
        notifications.Seed(nearRow);

        // USD -> DOP fails, so the budget line is indeterminate (cannot evaluate != no longer qualifies).
        var converter = new FakeCurrencyConverter(onConvert: (_, _) => Result.Failure("ExchangeRateUnavailable", "No rate."));

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            budgetRepository: budgets,
            categoryRepository: categories,
            currencyConverter: converter);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains(notifications.Notifications, n => n.Id == nearRow.Id);
    }

    [Fact]
    public async Task ExecuteAsync_FourKindsEnabled_ReadsAllSourcesSequentiallyNeverOverlapping()
    {
        var guard = new ConcurrencyGuard();
        var alerts = new TrackingAnomalyAlertRepository(new FakeAnomalyAlertRepository(), guard);
        var categories = new TrackingCategoryRepository(new FakeCategoryRepository(), guard);
        var budgetRepository = new FakeBudgetRepository();
        var categoryId = Guid.NewGuid();
        budgetRepository.Seed(Budget.Create(_today.Year, _today.Month, categoryId, Money.Create(1000m, Currency.DOP)));

        DetectNotificationsUseCase useCase = CreateUseCase(
            budgetRepository: budgetRepository,
            categoryRepository: categories,
            anomalyAlertRepository: alerts);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.False(guard.OverlapDetected);
        Assert.True(alerts.GetAllCallCount > 0);
        Assert.True(categories.GetAllCallCount > 0);
    }

    // --- S7: digest (R-13, D-5, D-12, S-9) ---

    private static readonly DateOnly _thisMonday = new(2026, 9, 21);

    private static InMemorySettingsStore NewSettingsStore()
    {
        // Digest defaults to Weekly with no cursor (always due); non-digest tests opt out.
        var store = new InMemorySettingsStore();
        store.Seed(SettingKeys.NotificationsDigestFrequency, nameof(DigestFrequency.Off));
        return store;
    }

    private static InMemorySettingsStore DigestSettings(DigestFrequency frequency, string? cursor = null)
    {
        InMemorySettingsStore store = NewSettingsStore();
        store.Seed(SettingKeys.NotificationsDigestFrequency, frequency.ToString());
        if (cursor is not null)
        {
            store.Seed(SettingKeys.NotificationsDigestLastPeriodStart, cursor);
        }

        return store;
    }

    private static Task<string?> ReadCursorAsync(InMemorySettingsStore store)
        => store.GetStringAsync(SettingKeys.NotificationsDigestLastPeriodStart);

    private static void SeedDebtPayment(
        FakeDebtRepository debts, FakeDebtPaymentRepository payments, string description, DateOnly scheduledDate)
    {
        Debt debt = CreateDebt(description, scheduledDate);
        debts.Seed(debt);
        payments.SeedRange(new DebtScheduleGenerator().Generate(debt));
    }

    [Fact]
    public async Task ExecuteAsync_WeeklyWithFiveWeekStaleCursor_CreatesExactlyOneDigestAndJumpsCursorToThisMonday()
    {
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Weekly, cursor: "2026-08-17");
        var debts = new FakeDebtRepository();
        var debtPayments = new FakeDebtPaymentRepository();
        var notifications = new FakeNotificationRepository();
        SeedDebtPayment(debts, debtPayments, "Prestamo carro", _today.AddDays(2));

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            debtPaymentRepository: debtPayments,
            debtRepository: debts,
            settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Notification digest = Assert.Single(notifications.Notifications, n => n.Kind == NotificationKind.Digest);
        Assert.Equal(_thisMonday, digest.ReferenceDate);
        Assert.Equal(NotificationKeys.ForDigest(DigestFrequency.Weekly, _thisMonday), digest.DedupKey);
        Assert.Equal("2026-09-21", await ReadCursorAsync(settings));
    }

    [Fact]
    public async Task ExecuteAsync_DigestDue_SummarizesCountsAndPerCurrencyTotalsOrderedByCurrency()
    {
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Weekly);
        var debts = new FakeDebtRepository();
        var debtPayments = new FakeDebtPaymentRepository();
        var rules = new FakeRecurringRuleRepository();
        var budgets = new FakeBudgetRepository();
        var expenses = new FakeExpenseRepository();
        var categories = new FakeCategoryRepository();
        var alerts = new FakeAnomalyAlertRepository();
        var notifications = new FakeNotificationRepository();

        SeedDebtPayment(debts, debtPayments, "Prestamo carro", _today.AddDays(2));
        rules.Seed(CreateRule("Netflix", _today.AddDays(1)));
        rules.Seed(CreateRule("Spotify USD", _today.AddDays(3), 20m, Currency.USD));

        var categoryId = Guid.NewGuid();
        categories.SetCategories([new Category { Id = categoryId, Name = "Comida" }]);
        budgets.Seed(Budget.Create(_today.Year, _today.Month, categoryId, Money.Create(1000m, Currency.DOP)));
        expenses.CategoryNames[categoryId] = "Comida";
        await expenses.AddAsync(CreateExpense(categoryId, 1200m));

        // One unacknowledged alert this month, one from a previous month (not counted).
        alerts.Seed(AnomalyAlert.Create(
            Guid.NewGuid(), categoryId, _today.AddDays(-1), AnomalySeverity.Warning, "Gasto inusual", _fixedCreatedAt));
        alerts.Seed(AnomalyAlert.Create(
            Guid.NewGuid(), categoryId, _today.AddDays(-40), AnomalySeverity.Warning, "Gasto inusual", _fixedCreatedAt));

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            debtPaymentRepository: debtPayments,
            debtRepository: debts,
            recurringRuleRepository: rules,
            budgetRepository: budgets,
            expenseRepository: expenses,
            categoryRepository: categories,
            anomalyAlertRepository: alerts,
            settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Notification digest = Assert.Single(notifications.Notifications, n => n.Kind == NotificationKind.Digest);
        Assert.Equal(DigestFrequency.Weekly, digest.DigestFrequency);
        Assert.Equal(3, digest.DigestPaymentCount);
        Assert.Equal(1, digest.DigestBudgetCount);
        Assert.Equal(1, digest.DigestAnomalyCount);
        Assert.Equal(
            new[]
            {
                new NotificationCurrencyTotal(Currency.DOP, 2000m),
                new NotificationCurrencyTotal(Currency.USD, 20m),
            },
            digest.DigestTotals);
    }

    [Fact]
    public async Task ExecuteAsync_DigestDue_IgnoresPerKindTogglesAndExistingDismissals()
    {
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Weekly);
        settings.Seed(SettingKeys.NotificationsDebtDueEnabled, "false");
        settings.Seed(SettingKeys.NotificationsRecurringDueEnabled, "false");
        settings.Seed(SettingKeys.NotificationsBudgetEnabled, "false");
        settings.Seed(SettingKeys.NotificationsAnomalyEnabled, "false");

        var debts = new FakeDebtRepository();
        var debtPayments = new FakeDebtPaymentRepository();
        var rules = new FakeRecurringRuleRepository();
        var alerts = new FakeAnomalyAlertRepository();
        var notifications = new FakeNotificationRepository();

        SeedDebtPayment(debts, debtPayments, "Prestamo carro", _today.AddDays(2));
        rules.Seed(CreateRule("Netflix", _today.AddDays(1)));
        var alert = AnomalyAlert.Create(
            Guid.NewGuid(), Guid.NewGuid(), _today.AddDays(-1), AnomalySeverity.Info, "Gasto inusual", _fixedCreatedAt);
        alerts.Seed(alert);

        // The anomaly notice was dismissed earlier; the digest must still count the alert.
        var dismissedAnomaly = Notification.ForAnomaly(alert.Id, alert.Date, "Comida", _fixedCreatedAt);
        dismissedAnomaly.Dismiss(_fixedCreatedAt);
        notifications.Seed(dismissedAnomaly);

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            debtPaymentRepository: debtPayments,
            debtRepository: debts,
            recurringRuleRepository: rules,
            anomalyAlertRepository: alerts,
            settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Notification digest = Assert.Single(notifications.Notifications, n => n.Kind == NotificationKind.Digest);
        Assert.Equal(2, digest.DigestPaymentCount);
        Assert.Equal(1, digest.DigestAnomalyCount);

        // No per-kind notice is created for a silenced kind.
        Assert.DoesNotContain(notifications.Notifications, n => n.Kind == NotificationKind.DebtInstallmentDue);
        Assert.DoesNotContain(notifications.Notifications, n => n.Kind == NotificationKind.RecurringChargeDue);
        Assert.Contains(notifications.Notifications, n => n.Id == dismissedAnomaly.Id && n.IsDismissed);
    }

    [Fact]
    public async Task ExecuteAsync_DigestDueWithDebtToggleOff_StillReadsDebtPaymentSourceOnce()
    {
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Weekly);
        settings.Seed(SettingKeys.NotificationsDebtDueEnabled, "false");
        var trackingPayments = new TrackingDebtPaymentRepository(new FakeDebtPaymentRepository(), new ConcurrencyGuard());

        DetectNotificationsUseCase useCase = CreateUseCase(
            debtPaymentRepository: trackingPayments, settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(1, trackingPayments.GetPendingByDateRangeCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WeeklyDigest_CountsOnlyPaymentsFromTodayThroughSixDaysAhead()
    {
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Weekly);
        var debts = new FakeDebtRepository();
        var debtPayments = new FakeDebtPaymentRepository();
        var notifications = new FakeNotificationRepository();

        SeedDebtPayment(debts, debtPayments, "Vencida", _today.AddDays(-1));
        SeedDebtPayment(debts, debtPayments, "Hoy", _today);
        SeedDebtPayment(debts, debtPayments, "Limite", _today.AddDays(6));
        SeedDebtPayment(debts, debtPayments, "Fuera", _today.AddDays(7));

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            debtPaymentRepository: debtPayments,
            debtRepository: debts,
            settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Notification digest = Assert.Single(notifications.Notifications, n => n.Kind == NotificationKind.Digest);
        Assert.Equal(2, digest.DigestPaymentCount);
    }

    [Fact]
    public async Task ExecuteAsync_DailyDigest_CountsOnlyPaymentsDueToday()
    {
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Daily, cursor: "2026-09-25");
        var debts = new FakeDebtRepository();
        var debtPayments = new FakeDebtPaymentRepository();
        var notifications = new FakeNotificationRepository();

        SeedDebtPayment(debts, debtPayments, "Hoy", _today);
        SeedDebtPayment(debts, debtPayments, "Manana", _today.AddDays(1));

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            debtPaymentRepository: debtPayments,
            debtRepository: debts,
            settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Notification digest = Assert.Single(notifications.Notifications, n => n.Kind == NotificationKind.Digest);
        Assert.Equal(DigestFrequency.Daily, digest.DigestFrequency);
        Assert.Equal(_today, digest.ReferenceDate);
        Assert.Equal(1, digest.DigestPaymentCount);
        Assert.Equal("2026-09-26", await ReadCursorAsync(settings));
    }

    [Fact]
    public async Task ExecuteAsync_DigestDueWithAllZeroCounts_CreatesNoRowButAdvancesCursor()
    {
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Weekly, cursor: "2026-09-14");
        var notifications = new FakeNotificationRepository();

        DetectNotificationsUseCase useCase = CreateUseCase(notificationRepository: notifications, settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(notifications.Notifications, n => n.Kind == NotificationKind.Digest);
        Assert.Equal(0, notifications.ApplyReconciliationCallCount);
        Assert.Equal("2026-09-21", await ReadCursorAsync(settings));
    }

    [Fact]
    public async Task ExecuteAsync_CursorAlreadyAtCurrentPeriod_CreatesNoDigestAndDoesNotReadDisabledSources()
    {
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Weekly, cursor: "2026-09-21");
        settings.Seed(SettingKeys.NotificationsDebtDueEnabled, "false");
        var debts = new FakeDebtRepository();
        var debtPayments = new FakeDebtPaymentRepository();
        SeedDebtPayment(debts, debtPayments, "Prestamo carro", _today.AddDays(2));
        var trackingPayments = new TrackingDebtPaymentRepository(debtPayments, new ConcurrencyGuard());
        var notifications = new FakeNotificationRepository();

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications, debtPaymentRepository: trackingPayments, settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(notifications.Notifications, n => n.Kind == NotificationKind.Digest);
        Assert.Equal(0, trackingPayments.GetPendingByDateRangeCallCount);
        Assert.Equal("2026-09-21", await ReadCursorAsync(settings));
    }

    [Fact]
    public async Task ExecuteAsync_WeeklyToDailyLaterInTheWeek_CreatesADailyDigest()
    {
        // The Weekly digest already ran this Monday; switching to Daily compares the cursor to today.
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Daily, cursor: "2026-09-21");
        var debts = new FakeDebtRepository();
        var debtPayments = new FakeDebtPaymentRepository();
        var notifications = new FakeNotificationRepository();
        SeedDebtPayment(debts, debtPayments, "Hoy", _today);

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            debtPaymentRepository: debtPayments,
            debtRepository: debts,
            settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Notification digest = Assert.Single(notifications.Notifications, n => n.Kind == NotificationKind.Digest);
        Assert.Equal(DigestFrequency.Daily, digest.DigestFrequency);
    }

    [Fact]
    public async Task ExecuteAsync_DailyToWeeklyAfterMonday_WaitsForNextMonday()
    {
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Weekly, cursor: "2026-09-26");
        var debts = new FakeDebtRepository();
        var debtPayments = new FakeDebtPaymentRepository();
        var notifications = new FakeNotificationRepository();
        SeedDebtPayment(debts, debtPayments, "Hoy", _today);

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            debtPaymentRepository: debtPayments,
            debtRepository: debts,
            settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(notifications.Notifications, n => n.Kind == NotificationKind.Digest);
        Assert.Equal("2026-09-26", await ReadCursorAsync(settings));
    }

    [Fact]
    public async Task ExecuteAsync_NewDigest_RemovesOlderUndismissedDigestAndKeepsDismissedOne()
    {
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Weekly, cursor: "2026-09-14");
        var debts = new FakeDebtRepository();
        var debtPayments = new FakeDebtPaymentRepository();
        var notifications = new FakeNotificationRepository();
        SeedDebtPayment(debts, debtPayments, "Prestamo carro", _today.AddDays(2));

        var olderUndismissed = Notification.ForDigest(
            DigestFrequency.Weekly, new DateOnly(2026, 9, 14), 1, [], 0, 0, _fixedCreatedAt);
        var olderDismissed = Notification.ForDigest(
            DigestFrequency.Weekly, new DateOnly(2026, 9, 7), 1, [], 0, 0, _fixedCreatedAt);
        olderDismissed.Dismiss(_fixedCreatedAt);
        notifications.Seed(olderUndismissed);
        notifications.Seed(olderDismissed);

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            debtPaymentRepository: debtPayments,
            debtRepository: debts,
            settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(notifications.Notifications, n => n.Id == olderUndismissed.Id);
        Assert.Contains(notifications.Notifications, n => n.Id == olderDismissed.Id);
        Notification current = Assert.Single(
            notifications.Notifications, n => n.Kind == NotificationKind.Digest && !n.IsDismissed);
        Assert.Equal(_thisMonday, current.ReferenceDate);
    }

    [Fact]
    public async Task ExecuteAsync_DigestOff_RemovesUndismissedDigestAndLeavesCursorUntouched()
    {
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Off);
        var notifications = new FakeNotificationRepository();
        var existing = Notification.ForDigest(
            DigestFrequency.Weekly, _thisMonday, 1, [], 0, 0, _fixedCreatedAt);
        notifications.Seed(existing);

        DetectNotificationsUseCase useCase = CreateUseCase(notificationRepository: notifications, settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(notifications.Notifications, n => n.Id == existing.Id);
        Assert.Null(await ReadCursorAsync(settings));
    }

    [Fact]
    public async Task ExecuteAsync_ReconcileSaveFailsWhileDigestDue_DoesNotAdvanceCursor()
    {
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Weekly, cursor: "2026-09-14");
        var debts = new FakeDebtRepository();
        var debtPayments = new FakeDebtPaymentRepository();
        var notifications = new FakeNotificationRepository
        {
            ApplyReconciliationResult = Result.Failure("Conflict", "Unique index violation."),
        };
        SeedDebtPayment(debts, debtPayments, "Prestamo carro", _today.AddDays(2));

        DetectNotificationsUseCase useCase = CreateUseCase(
            notificationRepository: notifications,
            debtPaymentRepository: debtPayments,
            debtRepository: debts,
            settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("2026-09-14", await ReadCursorAsync(settings));
    }

    [Fact]
    public async Task ExecuteAsync_DigestDueWithAllSourcesTracked_ReadsSequentiallyNeverOverlapping()
    {
        var guard = new ConcurrencyGuard();
        InMemorySettingsStore settings = DigestSettings(DigestFrequency.Weekly);
        settings.Seed(SettingKeys.NotificationsDebtDueEnabled, "false");
        settings.Seed(SettingKeys.NotificationsRecurringDueEnabled, "false");
        settings.Seed(SettingKeys.NotificationsAnomalyEnabled, "false");

        var trackingPayments = new TrackingDebtPaymentRepository(new FakeDebtPaymentRepository(), guard);
        var trackingRules = new TrackingRecurringRuleRepository(new FakeRecurringRuleRepository(), guard);
        var trackingAlerts = new TrackingAnomalyAlertRepository(new FakeAnomalyAlertRepository(), guard);

        DetectNotificationsUseCase useCase = CreateUseCase(
            debtPaymentRepository: trackingPayments,
            recurringRuleRepository: trackingRules,
            anomalyAlertRepository: trackingAlerts,
            settingsStore: settings);

        Result result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.False(guard.OverlapDetected);
        Assert.Equal(1, trackingPayments.GetPendingByDateRangeCallCount);
        Assert.Equal(1, trackingRules.GetAllCallCount);
        Assert.Equal(1, trackingAlerts.GetAllCallCount);
    }

    private static DetectNotificationsUseCase CreateUseCase(
        INotificationRepository? notificationRepository = null,
        IDebtPaymentRepository? debtPaymentRepository = null,
        IDebtRepository? debtRepository = null,
        IRecurringRuleRepository? recurringRuleRepository = null,
        IBudgetRepository? budgetRepository = null,
        IExpenseRepository? expenseRepository = null,
        ICategoryRepository? categoryRepository = null,
        ICurrencyConverter? currencyConverter = null,
        IAnomalyAlertRepository? anomalyAlertRepository = null,
        ISettingsStore? settingsStore = null,
        RecordingLogger<DetectNotificationsUseCase>? logger = null)
    {
        ICategoryRepository categories = categoryRepository ?? new FakeCategoryRepository();
        var budgetVsActual = new GetBudgetVsActualQuery(
            budgetRepository ?? new FakeBudgetRepository(),
            expenseRepository ?? new FakeExpenseRepository(),
            categories,
            currencyConverter ?? new FakeCurrencyConverter());

        return new(
            notificationRepository ?? new FakeNotificationRepository(),
            debtPaymentRepository ?? new FakeDebtPaymentRepository(),
            debtRepository ?? new FakeDebtRepository(),
            recurringRuleRepository ?? new FakeRecurringRuleRepository(),
            budgetVsActual,
            anomalyAlertRepository ?? new FakeAnomalyAlertRepository(),
            categories,
            settingsStore ?? NewSettingsStore(),
            new FakeClock { Today = _today, UtcNow = _fixedCreatedAt },
            logger ?? new RecordingLogger<DetectNotificationsUseCase>());
    }

    private static Expense CreateExpense(Guid categoryId, decimal amount)
        => Expense.Create(
            categoryId,
            new DateOnly(_today.Year, _today.Month, 10),
            Money.Create(amount, Currency.DOP),
            "Test expense",
            null,
            _fixedCreatedAt);

    private static Debt CreateDebt(string description, DateOnly scheduledDate)
        => Debt.Create(
            description,
            Money.Create(1500m, Currency.DOP),
            Currency.DOP,
            totalInstallments: 1,
            installmentAmount: Money.Create(1500m, Currency.DOP),
            frequency: nameof(DebtFrequency.Monthly),
            startDate: scheduledDate);

    private static RecurringRule CreateRule(
        string name, DateOnly nextGenerationDate, decimal amount = 500m, Currency currency = Currency.DOP)
        => RecurringRule.Create(
            name,
            Money.Create(amount, currency),
            currency,
            "Monthly",
            startDate: nextGenerationDate,
            endDate: null,
            categoryId: Guid.NewGuid(),
            paymentMethodId: Guid.NewGuid());

    /// <summary>
    /// Tracks how many reads are in flight at once so a test can assert
    /// <see cref="DetectNotificationsUseCase"/> never awaits two source
    /// reads concurrently (R-15: sequential, never <c>Task.WhenAll</c>).
    /// </summary>
    private sealed class ConcurrencyGuard
    {
        private int _active;

        public bool OverlapDetected { get; private set; }

        public async Task EnterAsync()
        {
            if (Interlocked.Increment(ref _active) > 1)
            {
                OverlapDetected = true;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        public void Exit() => Interlocked.Decrement(ref _active);
    }

    private sealed class TrackingDebtPaymentRepository : IDebtPaymentRepository
    {
        private readonly FakeDebtPaymentRepository _inner;
        private readonly ConcurrencyGuard _guard;

        public TrackingDebtPaymentRepository(FakeDebtPaymentRepository inner, ConcurrencyGuard guard)
        {
            _inner = inner;
            _guard = guard;
        }

        public int GetPendingByDateRangeCallCount { get; private set; }

        public Task<Result> AddAsync(DebtPayment payment, CancellationToken ct = default) => _inner.AddAsync(payment, ct);

        public Task<Result> UpdateAsync(DebtPayment payment, CancellationToken ct = default) => _inner.UpdateAsync(payment, ct);

        public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default) => _inner.RemoveAsync(id, ct);

        public Task<DebtPayment?> GetByIdAsync(Guid id, CancellationToken ct = default) => _inner.GetByIdAsync(id, ct);

        public Task<IReadOnlyList<DebtPayment>> GetAllAsync(CancellationToken ct = default) => _inner.GetAllAsync(ct);

        public Task<IReadOnlyList<DebtPayment>> GetByDebtIdAsync(Guid debtId, CancellationToken ct = default)
            => _inner.GetByDebtIdAsync(debtId, ct);

        public async Task<IReadOnlyList<DebtPayment>> GetPendingByDateRangeAsync(DateRange range, CancellationToken ct = default)
        {
            GetPendingByDateRangeCallCount++;
            await _guard.EnterAsync().ConfigureAwait(false);
            try
            {
                return await _inner.GetPendingByDateRangeAsync(range, ct).ConfigureAwait(false);
            }
            finally
            {
                _guard.Exit();
            }
        }
    }

    private sealed class TrackingDebtRepository : IDebtRepository
    {
        private readonly FakeDebtRepository _inner;
        private readonly ConcurrencyGuard _guard;

        public TrackingDebtRepository(FakeDebtRepository inner, ConcurrencyGuard guard)
        {
            _inner = inner;
            _guard = guard;
        }

        public int GetAllCallCount { get; private set; }

        public Task<Result> AddAsync(Debt debt, CancellationToken ct = default) => _inner.AddAsync(debt, ct);

        public Task<Result> AddWithScheduleAsync(Debt debt, IReadOnlyList<DebtPayment> payments, CancellationToken ct = default)
            => _inner.AddWithScheduleAsync(debt, payments, ct);

        public Task<Result> UpdateAsync(Debt debt, CancellationToken ct = default) => _inner.UpdateAsync(debt, ct);

        public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default) => _inner.RemoveAsync(id, ct);

        public Task<Debt?> GetByIdAsync(Guid id, CancellationToken ct = default) => _inner.GetByIdAsync(id, ct);

        public async Task<IReadOnlyList<Debt>> GetAllAsync(CancellationToken ct = default)
        {
            GetAllCallCount++;
            await _guard.EnterAsync().ConfigureAwait(false);
            try
            {
                return await _inner.GetAllAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _guard.Exit();
            }
        }
    }

    private sealed class TrackingRecurringRuleRepository : IRecurringRuleRepository
    {
        private readonly FakeRecurringRuleRepository _inner;
        private readonly ConcurrencyGuard _guard;

        public TrackingRecurringRuleRepository(FakeRecurringRuleRepository inner, ConcurrencyGuard guard)
        {
            _inner = inner;
            _guard = guard;
        }

        public int GetAllCallCount { get; private set; }

        public Task<Result> AddAsync(RecurringRule rule, CancellationToken ct = default) => _inner.AddAsync(rule, ct);

        public Task<Result> UpdateAsync(RecurringRule rule, CancellationToken ct = default) => _inner.UpdateAsync(rule, ct);

        public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default) => _inner.RemoveAsync(id, ct);

        public Task<RecurringRule?> GetByIdAsync(Guid id, CancellationToken ct = default) => _inner.GetByIdAsync(id, ct);

        public async Task<IReadOnlyList<RecurringRule>> GetAllAsync(CancellationToken ct = default)
        {
            GetAllCallCount++;
            await _guard.EnterAsync().ConfigureAwait(false);
            try
            {
                return await _inner.GetAllAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _guard.Exit();
            }
        }
    }

    private sealed class TrackingAnomalyAlertRepository : IAnomalyAlertRepository
    {
        private readonly FakeAnomalyAlertRepository _inner;
        private readonly ConcurrencyGuard _guard;

        public TrackingAnomalyAlertRepository(FakeAnomalyAlertRepository inner, ConcurrencyGuard guard)
        {
            _inner = inner;
            _guard = guard;
        }

        public int GetAllCallCount { get; private set; }

        public Task<Result> AddAsync(AnomalyAlert alert, CancellationToken ct = default) => _inner.AddAsync(alert, ct);

        public Task<Result> UpdateAsync(AnomalyAlert alert, CancellationToken ct = default) => _inner.UpdateAsync(alert, ct);

        public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default) => _inner.RemoveAsync(id, ct);

        public Task<AnomalyAlert?> GetByIdAsync(Guid id, CancellationToken ct = default) => _inner.GetByIdAsync(id, ct);

        public Task<IReadOnlyList<AnomalyAlert>> GetByMonthAsync(DateRange month, CancellationToken ct = default)
            => _inner.GetByMonthAsync(month, ct);

        public Task<IReadOnlyList<AnomalyAlert>> GetByTransactionIdsAsync(
            IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
            => _inner.GetByTransactionIdsAsync(ids, ct);

        public Task<Result> ApplyReconciliationAsync(AnomalyReconciliationPlan plan, CancellationToken ct = default)
            => _inner.ApplyReconciliationAsync(plan, ct);

        public async Task<IReadOnlyList<AnomalyAlert>> GetAllAsync(CancellationToken ct = default)
        {
            GetAllCallCount++;
            await _guard.EnterAsync().ConfigureAwait(false);
            try
            {
                return await _inner.GetAllAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _guard.Exit();
            }
        }
    }

    private sealed class TrackingCategoryRepository : ICategoryRepository
    {
        private readonly FakeCategoryRepository _inner;
        private readonly ConcurrencyGuard _guard;

        public TrackingCategoryRepository(FakeCategoryRepository inner, ConcurrencyGuard guard)
        {
            _inner = inner;
            _guard = guard;
        }

        public int GetAllCallCount { get; private set; }

        public Task<Result> AddAsync(Category category, CancellationToken ct = default) => _inner.AddAsync(category, ct);

        public Task<Result> UpdateAsync(Category category, CancellationToken ct = default) => _inner.UpdateAsync(category, ct);

        public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default) => _inner.RemoveAsync(id, ct);

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default) => _inner.GetByIdAsync(id, ct);

        public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default)
        {
            GetAllCallCount++;
            await _guard.EnterAsync().ConfigureAwait(false);
            try
            {
                return await _inner.GetAllAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _guard.Exit();
            }
        }
    }
}
