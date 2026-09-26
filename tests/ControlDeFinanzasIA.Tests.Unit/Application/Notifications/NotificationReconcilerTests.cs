using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Notifications;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Notifications;

/// <summary>
/// Tests para <see cref="NotificationReconciler.Plan"/> (F-017, R-1, R-5,
/// R-7, R-9, D-4): purga, agregar/refrescar/no-op sobre candidatos, y
/// remover filas sin descartar cuya fuente ya no califica.
/// </summary>
public sealed class NotificationReconcilerTests
{
    private static readonly DateOnly _today = new(2026, 9, 26);
    private static readonly DateTime _createdAt = new(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlySet<NotificationKind> _debtKindOnly =
        new HashSet<NotificationKind> { NotificationKind.DebtInstallmentDue };

    private static readonly IReadOnlySet<NotificationKind> _budgetKindOnly =
        new HashSet<NotificationKind> { NotificationKind.BudgetThreshold };

    private static readonly IReadOnlySet<NotificationKind> _recurringKindOnly =
        new HashSet<NotificationKind> { NotificationKind.RecurringChargeDue };

    private static readonly IReadOnlySet<string> _noProtectedKeys = new HashSet<string>();

    // --- S-4: idempotency across two runs ---

    [Fact]
    public void Plan_TwoPassesSameFacts_SecondPassIsNoOpAndKeepsOriginalCreatedAt()
    {
        Notification candidateRun1 = CreateDebtCandidate(amount: 1500m, _createdAt);

        NotificationReconciliationPlan plan1 = NotificationReconciler.Plan(
            existing: [],
            candidates: [candidateRun1],
            evaluatedKinds: _debtKindOnly,
            protectedKeys: _noProtectedKeys,
            today: _today,
            digestDue: false);

        Assert.Single(plan1.ToAdd);
        Notification persisted = plan1.ToAdd[0];
        Guid originalId = persisted.Id;
        DateTime originalCreatedAt = persisted.CreatedAt;

        var candidateRun2 = Notification.ForDebtInstallment(
            persisted.SourceId!.Value,
            persisted.ReferenceDate,
            persisted.SubjectLabel,
            Money.Create(1500m, Currency.DOP),
            _createdAt.AddHours(6));

        NotificationReconciliationPlan plan2 = NotificationReconciler.Plan(
            existing: [persisted],
            candidates: [candidateRun2],
            evaluatedKinds: _debtKindOnly,
            protectedKeys: _noProtectedKeys,
            today: _today,
            digestDue: false);

        Assert.True(plan2.IsEmpty);
        Assert.Equal(originalId, persisted.Id);
        Assert.Equal(originalCreatedAt, persisted.CreatedAt);
    }

    // --- S-5: dismissed notification is never recreated ---

    [Fact]
    public void Plan_DismissedRowStillQualifies_NeverRecreatedOrModified()
    {
        Notification dismissed = CreateDebtCandidate(amount: 1500m, _createdAt);
        dismissed.Dismiss(_createdAt.AddHours(1));

        var stillQualifyingCandidate = Notification.ForDebtInstallment(
            dismissed.SourceId!.Value,
            dismissed.ReferenceDate,
            dismissed.SubjectLabel,
            Money.Create(1500m, Currency.DOP),
            _createdAt.AddDays(1));

        NotificationReconciliationPlan plan = NotificationReconciler.Plan(
            existing: [dismissed],
            candidates: [stillQualifyingCandidate],
            evaluatedKinds: _debtKindOnly,
            protectedKeys: _noProtectedKeys,
            today: _today,
            digestDue: false);

        Assert.True(plan.IsEmpty);
    }

    // --- S-6: dismissed Near, then Exceeded crossing creates a new notification ---

    [Fact]
    public void Plan_DismissedNearPlusLaterExceededCandidate_AddsExceededUnaffected()
    {
        var budgetId = Guid.NewGuid();
        var monthStart = new DateOnly(2026, 9, 1);

        var dismissedNear = Notification.ForBudget(
            budgetId, monthStart, "Comida", 85m, NotificationTier.Near, _createdAt);
        dismissedNear.Dismiss(_createdAt.AddHours(1));

        var exceededCandidate = Notification.ForBudget(
            budgetId, monthStart, "Comida", 105m, NotificationTier.Exceeded, _createdAt.AddDays(2));

        NotificationReconciliationPlan plan = NotificationReconciler.Plan(
            existing: [dismissedNear],
            candidates: [exceededCandidate],
            evaluatedKinds: _budgetKindOnly,
            protectedKeys: _noProtectedKeys,
            today: _today,
            digestDue: false);

        Assert.Single(plan.ToAdd);
        Assert.Equal(exceededCandidate.DedupKey, plan.ToAdd[0].DedupKey);
        Assert.Empty(plan.ToRemove);
        Assert.Empty(plan.ToUpdate);
    }

    // --- R-5: Exceeded candidate removes undismissed Near for the same budget/month ---

    [Fact]
    public void Plan_ExceededCandidate_RemovesUndismissedNearForSameBudgetAndMonth()
    {
        var budgetId = Guid.NewGuid();
        var monthStart = new DateOnly(2026, 9, 1);

        var undismissedNear = Notification.ForBudget(
            budgetId, monthStart, "Comida", 85m, NotificationTier.Near, _createdAt);

        var exceededCandidate = Notification.ForBudget(
            budgetId, monthStart, "Comida", 105m, NotificationTier.Exceeded, _createdAt.AddDays(1));

        NotificationReconciliationPlan plan = NotificationReconciler.Plan(
            existing: [undismissedNear],
            candidates: [exceededCandidate],
            evaluatedKinds: _budgetKindOnly,
            protectedKeys: _noProtectedKeys,
            today: _today,
            digestDue: false);

        Assert.Single(plan.ToAdd);
        Assert.Single(plan.ToRemove);
        Assert.Equal(undismissedNear.Id, plan.ToRemove[0]);
    }

    // --- S-7: toggled-off kind removes existing undismissed rows and blocks new ones ---

    [Fact]
    public void Plan_DisabledKindEvaluatedWithZeroCandidates_RemovesBothReadAndUnreadUndismissedRows()
    {
        Notification readRow = CreateRecurringCandidate(ruleId: Guid.NewGuid(), _createdAt);
        readRow.MarkRead(_createdAt.AddHours(1));
        Notification unreadRow = CreateRecurringCandidate(ruleId: Guid.NewGuid(), _createdAt);

        NotificationReconciliationPlan plan = NotificationReconciler.Plan(
            existing: [readRow, unreadRow],
            candidates: [],
            evaluatedKinds: _recurringKindOnly,
            protectedKeys: _noProtectedKeys,
            today: _today,
            digestDue: false);

        Assert.Equal(2, plan.ToRemove.Count);
        Assert.Contains(readRow.Id, plan.ToRemove);
        Assert.Contains(unreadRow.Id, plan.ToRemove);
        Assert.Empty(plan.ToAdd);
    }

    [Fact]
    public void Plan_ReEnabledKind_RecreatesRowsForSourcesThatStillQualifyAndWereNotDismissed()
    {
        var ruleId = Guid.NewGuid();
        Notification candidate = CreateRecurringCandidate(ruleId, _createdAt);

        NotificationReconciliationPlan plan = NotificationReconciler.Plan(
            existing: [],
            candidates: [candidate],
            evaluatedKinds: _recurringKindOnly,
            protectedKeys: _noProtectedKeys,
            today: _today,
            digestDue: false);

        Assert.Single(plan.ToAdd);
        Assert.Equal(candidate.DedupKey, plan.ToAdd[0].DedupKey);
    }

    // --- protectedKeys entry is kept even with no candidate ---

    [Fact]
    public void Plan_ProtectedKeyWithNoCandidate_IsKeptNotRemoved()
    {
        var budgetId = Guid.NewGuid();
        var monthStart = new DateOnly(2026, 9, 1);
        var indeterminateRow = Notification.ForBudget(
            budgetId, monthStart, "Comida", 85m, NotificationTier.Near, _createdAt);
        var protectedKeys = new HashSet<string> { indeterminateRow.DedupKey };

        NotificationReconciliationPlan plan = NotificationReconciler.Plan(
            existing: [indeterminateRow],
            candidates: [],
            evaluatedKinds: _budgetKindOnly,
            protectedKeys: protectedKeys,
            today: _today,
            digestDue: false);

        Assert.True(plan.IsEmpty);
    }

    // --- S-10: purge removes rows older than 60 days, dismissed included; keeps exactly 60 days ---

    [Fact]
    public void Plan_PurgesRowsOlderThan60Days_DismissedIncluded_KeepsRowAtExactly60Days()
    {
        DateOnly sixtyOneDaysAgo = _today.AddDays(-61);
        DateOnly sixtyDaysAgo = _today.AddDays(-60);

        var staleDismissed = Notification.ForDebtInstallment(
            Guid.NewGuid(), sixtyOneDaysAgo, "Cuota vieja", Money.Create(100m, Currency.DOP), _createdAt);
        staleDismissed.Dismiss(_createdAt.AddHours(1));

        var staleUndismissed = Notification.ForDebtInstallment(
            Guid.NewGuid(), sixtyOneDaysAgo, "Otra cuota vieja", Money.Create(200m, Currency.DOP), _createdAt);

        var exactlySixtyDays = Notification.ForDebtInstallment(
            Guid.NewGuid(), sixtyDaysAgo, "Cuota limite", Money.Create(300m, Currency.DOP), _createdAt);

        // Still qualifies this run (same facts), isolating the assertion to the purge rule only —
        // otherwise the general removal rule would also drop it for "no longer being a candidate".
        var stillQualifyingCandidate = Notification.ForDebtInstallment(
            exactlySixtyDays.SourceId!.Value,
            exactlySixtyDays.ReferenceDate,
            exactlySixtyDays.SubjectLabel,
            Money.Create(300m, Currency.DOP),
            _createdAt.AddDays(1));

        NotificationReconciliationPlan plan = NotificationReconciler.Plan(
            existing: [staleDismissed, staleUndismissed, exactlySixtyDays],
            candidates: [stillQualifyingCandidate],
            evaluatedKinds: _debtKindOnly,
            protectedKeys: _noProtectedKeys,
            today: _today,
            digestDue: false);

        Assert.Equal(2, plan.ToRemove.Count);
        Assert.Contains(staleDismissed.Id, plan.ToRemove);
        Assert.Contains(staleUndismissed.Id, plan.ToRemove);
        Assert.DoesNotContain(exactlySixtyDays.Id, plan.ToRemove);
    }

    // --- an undismissed row with changed facts goes to ToUpdate, preserving Id/CreatedAt/ReadAt ---

    [Fact]
    public void Plan_UndismissedRowWithChangedFacts_GoesToToUpdatePreservingIdCreatedAtAndReadAt()
    {
        Notification existingRow = CreateDebtCandidate(amount: 1500m, _createdAt);
        DateTime readAt = _createdAt.AddHours(2);
        existingRow.MarkRead(readAt);
        Guid originalId = existingRow.Id;
        DateTime originalCreatedAt = existingRow.CreatedAt;

        var changedCandidate = Notification.ForDebtInstallment(
            existingRow.SourceId!.Value,
            existingRow.ReferenceDate,
            existingRow.SubjectLabel,
            Money.Create(9999m, Currency.DOP),
            _createdAt.AddDays(1));

        NotificationReconciliationPlan plan = NotificationReconciler.Plan(
            existing: [existingRow],
            candidates: [changedCandidate],
            evaluatedKinds: _debtKindOnly,
            protectedKeys: _noProtectedKeys,
            today: _today,
            digestDue: false);

        Assert.Single(plan.ToUpdate);
        Assert.Empty(plan.ToAdd);
        Assert.Empty(plan.ToRemove);
        Assert.Equal(originalId, existingRow.Id);
        Assert.Equal(originalCreatedAt, existingRow.CreatedAt);
        Assert.Equal(readAt, existingRow.ReadAt);
        Assert.Equal(9999m, existingRow.Amount);
    }

    // --- edge case: empty current facts + existing rows -> all removed except dismissed ---

    [Fact]
    public void Plan_EmptyCandidates_RemovesAllExistingExceptDismissed()
    {
        Notification dismissedRow = CreateDebtCandidate(amount: 100m, _createdAt);
        dismissedRow.Dismiss(_createdAt.AddHours(1));

        Notification undismissedRow1 = CreateDebtCandidate(amount: 200m, _createdAt);
        Notification undismissedRow2 = CreateDebtCandidate(amount: 300m, _createdAt);

        NotificationReconciliationPlan plan = NotificationReconciler.Plan(
            existing: [dismissedRow, undismissedRow1, undismissedRow2],
            candidates: [],
            evaluatedKinds: _debtKindOnly,
            protectedKeys: _noProtectedKeys,
            today: _today,
            digestDue: false);

        Assert.Equal(2, plan.ToRemove.Count);
        Assert.Contains(undismissedRow1.Id, plan.ToRemove);
        Assert.Contains(undismissedRow2.Id, plan.ToRemove);
        Assert.DoesNotContain(dismissedRow.Id, plan.ToRemove);
    }

    // --- edge case: empty existing rows + current facts -> all added ---

    [Fact]
    public void Plan_EmptyExisting_AddsAllCandidates()
    {
        Notification candidate1 = CreateDebtCandidate(amount: 100m, _createdAt);
        Notification candidate2 = CreateDebtCandidate(amount: 200m, _createdAt);

        NotificationReconciliationPlan plan = NotificationReconciler.Plan(
            existing: [],
            candidates: [candidate1, candidate2],
            evaluatedKinds: _debtKindOnly,
            protectedKeys: _noProtectedKeys,
            today: _today,
            digestDue: false);

        Assert.Equal(2, plan.ToAdd.Count);
        Assert.Contains(plan.ToAdd, n => n.DedupKey == candidate1.DedupKey);
        Assert.Contains(plan.ToAdd, n => n.DedupKey == candidate2.DedupKey);
    }

    // --- edge case: identical facts -> no-op/no update ---

    [Fact]
    public void Plan_IdenticalFacts_IsNoOpAndDoesNotMutateExistingRow()
    {
        Notification existingRow = CreateDebtCandidate(amount: 1500m, _createdAt);
        Guid originalId = existingRow.Id;
        DateTime originalCreatedAt = existingRow.CreatedAt;

        var identicalCandidate = Notification.ForDebtInstallment(
            existingRow.SourceId!.Value,
            existingRow.ReferenceDate,
            existingRow.SubjectLabel,
            Money.Create(1500m, Currency.DOP),
            _createdAt.AddDays(3));

        NotificationReconciliationPlan plan = NotificationReconciler.Plan(
            existing: [existingRow],
            candidates: [identicalCandidate],
            evaluatedKinds: _debtKindOnly,
            protectedKeys: _noProtectedKeys,
            today: _today,
            digestDue: false);

        Assert.True(plan.IsEmpty);
        Assert.Equal(originalId, existingRow.Id);
        Assert.Equal(originalCreatedAt, existingRow.CreatedAt);
    }

    private static Notification CreateDebtCandidate(decimal amount, DateTime createdAtUtc) =>
        Notification.ForDebtInstallment(
            Guid.NewGuid(),
            new DateOnly(2026, 9, 29),
            "Cuota",
            Money.Create(amount, Currency.DOP),
            createdAtUtc);

    private static Notification CreateRecurringCandidate(Guid ruleId, DateTime createdAtUtc) =>
        Notification.ForRecurringCharge(
            ruleId,
            new DateOnly(2026, 9, 27),
            "Netflix",
            Money.Create(500m, Currency.USD),
            createdAtUtc);
}
