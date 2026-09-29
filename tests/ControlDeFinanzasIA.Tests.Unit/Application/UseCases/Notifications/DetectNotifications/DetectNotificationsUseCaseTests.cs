using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Notifications.DetectNotifications;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.Recurring;
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
        var settingsStore = new InMemorySettingsStore();
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
        var settingsStore = new InMemorySettingsStore();
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
        var settingsStore = new InMemorySettingsStore();
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

    private static DetectNotificationsUseCase CreateUseCase(
        INotificationRepository? notificationRepository = null,
        IDebtPaymentRepository? debtPaymentRepository = null,
        IDebtRepository? debtRepository = null,
        IRecurringRuleRepository? recurringRuleRepository = null,
        ISettingsStore? settingsStore = null,
        RecordingLogger<DetectNotificationsUseCase>? logger = null)
        => new(
            notificationRepository ?? new FakeNotificationRepository(),
            debtPaymentRepository ?? new FakeDebtPaymentRepository(),
            debtRepository ?? new FakeDebtRepository(),
            recurringRuleRepository ?? new FakeRecurringRuleRepository(),
            settingsStore ?? new InMemorySettingsStore(),
            new FakeClock { Today = _today, UtcNow = _fixedCreatedAt },
            logger ?? new RecordingLogger<DetectNotificationsUseCase>());

    private static Debt CreateDebt(string description, DateOnly scheduledDate)
        => Debt.Create(
            description,
            Money.Create(1500m, Currency.DOP),
            Currency.DOP,
            totalInstallments: 1,
            installmentAmount: Money.Create(1500m, Currency.DOP),
            frequency: nameof(DebtFrequency.Monthly),
            startDate: scheduledDate);

    private static RecurringRule CreateRule(string name, DateOnly nextGenerationDate)
        => RecurringRule.Create(
            name,
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
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
}
