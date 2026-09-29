using ControlDeFinanzasIA.Application.Notifications;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Notifications;

/// <summary>
/// Tests for <see cref="NotificationRules.DebtInstallments"/> and
/// <see cref="NotificationRules.RecurringCharges"/> (F-017, slice S3, R-4,
/// D-3): the 3-day forward horizon plus the 7-day overdue look-back for
/// debt installments (S-8), and the forward-only 3-day horizon plus the
/// deactivated/ended exclusions for recurring charges (Reconciliation #5).
/// </summary>
public sealed class NotificationRulesTests
{
    private static readonly DateOnly _today = new(2026, 9, 26);
    private static readonly DateOnly _monthStart = new(2026, 9, 1);
    private static readonly DateTime _now = new(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc);

    // --- DebtInstallments: S-8 window boundary ---

    [Theory]
    [InlineData(3, true)] // day +3 forward: in
    [InlineData(4, false)] // day +4 forward: out
    [InlineData(-7, true)] // 7 days overdue: in
    [InlineData(-8, false)] // 8 days overdue: out
    public void DebtInstallments_DateOffsetFromToday_RespectsWindow(int offsetDays, bool expectedIncluded)
    {
        DebtPayment payment = CreatePayment(_today.AddDays(offsetDays));
        var debtLabels = new Dictionary<Guid, string> { [payment.DebtId] = "Préstamo carro" };

        IReadOnlyList<Notification> candidates = NotificationRules.DebtInstallments(
            [payment], debtLabels, _today, _now);

        Assert.Equal(expectedIncluded, candidates.Count == 1);
    }

    [Fact]
    public void DebtInstallments_QualifyingPayment_BuildsDebtInstallmentDueCandidate()
    {
        DebtPayment payment = CreatePayment(_today.AddDays(2));
        var debtLabels = new Dictionary<Guid, string> { [payment.DebtId] = "Préstamo carro" };

        IReadOnlyList<Notification> candidates = NotificationRules.DebtInstallments(
            [payment], debtLabels, _today, _now);

        Notification candidate = Assert.Single(candidates);
        Assert.Equal(NotificationKind.DebtInstallmentDue, candidate.Kind);
        Assert.Equal(payment.Id, candidate.SourceId);
        Assert.Equal(payment.ScheduledDate, candidate.ReferenceDate);
        Assert.Equal("Préstamo carro", candidate.SubjectLabel);
        Assert.Equal(payment.Amount.Amount, candidate.Amount);
        Assert.Equal(payment.Amount.Currency, candidate.Currency);
    }

    [Fact]
    public void DebtInstallments_PaidPayment_IsExcludedEvenInsideWindow()
    {
        DebtPayment payment = CreatePayment(_today.AddDays(1));
        payment.MarkAsPaid(_today);
        var debtLabels = new Dictionary<Guid, string> { [payment.DebtId] = "Préstamo carro" };

        IReadOnlyList<Notification> candidates = NotificationRules.DebtInstallments(
            [payment], debtLabels, _today, _now);

        Assert.Empty(candidates);
    }

    [Fact]
    public void DebtInstallments_SkippedPayment_IsExcludedEvenInsideWindow()
    {
        DebtPayment payment = CreatePayment(_today.AddDays(1));
        payment.Skip();
        var debtLabels = new Dictionary<Guid, string> { [payment.DebtId] = "Préstamo carro" };

        IReadOnlyList<Notification> candidates = NotificationRules.DebtInstallments(
            [payment], debtLabels, _today, _now);

        Assert.Empty(candidates);
    }

    [Fact]
    public void DebtInstallments_UnknownDebtLabel_UsesEmptyLabel()
    {
        DebtPayment payment = CreatePayment(_today);

        IReadOnlyList<Notification> candidates = NotificationRules.DebtInstallments(
            [payment], new Dictionary<Guid, string>(), _today, _now);

        Notification candidate = Assert.Single(candidates);
        Assert.Equal(string.Empty, candidate.SubjectLabel);
    }

    // --- RecurringCharges: forward-only window ---

    [Theory]
    [InlineData(0, true)] // today: in
    [InlineData(-1, false)] // yesterday: out (no overdue look-back)
    [InlineData(3, true)] // day +3: in
    [InlineData(4, false)] // day +4: out
    public void RecurringCharges_DateOffsetFromToday_RespectsForwardOnlyWindow(int offsetDays, bool expectedIncluded)
    {
        RecurringRule rule = CreateRule("Netflix", _today.AddDays(offsetDays));

        IReadOnlyList<Notification> candidates = NotificationRules.RecurringCharges([rule], _today, _now);

        Assert.Equal(expectedIncluded, candidates.Count == 1);
    }

    [Fact]
    public void RecurringCharges_QualifyingRule_BuildsRecurringChargeDueCandidate()
    {
        RecurringRule rule = CreateRule("Netflix", _today.AddDays(1));

        IReadOnlyList<Notification> candidates = NotificationRules.RecurringCharges([rule], _today, _now);

        Notification candidate = Assert.Single(candidates);
        Assert.Equal(NotificationKind.RecurringChargeDue, candidate.Kind);
        Assert.Equal(rule.Id, candidate.SourceId);
        Assert.Equal(rule.NextGenerationDate, candidate.ReferenceDate);
        Assert.Equal("Netflix", candidate.SubjectLabel);
        Assert.Equal(rule.Amount.Amount, candidate.Amount);
        Assert.Equal(rule.Amount.Currency, candidate.Currency);
    }

    [Fact]
    public void RecurringCharges_InactiveRule_IsExcluded()
    {
        RecurringRule rule = CreateRule("Gimnasio", _today.AddDays(1));
        rule.Deactivate();

        IReadOnlyList<Notification> candidates = NotificationRules.RecurringCharges([rule], _today, _now);

        Assert.Empty(candidates);
    }

    [Fact]
    public void RecurringCharges_EndDateBeforeNextGenerationDate_IsExcluded()
    {
        RecurringRule rule = CreateEndedRule("Prueba gratis", _today);

        IReadOnlyList<Notification> candidates = NotificationRules.RecurringCharges([rule], _today, _now);

        Assert.Empty(candidates);
    }

    // --- Budgets: highest tier only, indeterminate lines protected ---

    [Theory]
    [InlineData(79.99, null)] // just below Near: no candidate
    [InlineData(80.00, NotificationTier.Near)] // Near lower bound
    [InlineData(99.99, NotificationTier.Near)] // just below Exceeded: still Near only
    [InlineData(100.00, NotificationTier.Exceeded)] // Exceeded lower bound: Exceeded only, never both
    [InlineData(250.00, NotificationTier.Exceeded)]
    public void Budgets_PercentConsumed_ProducesHighestTierOnly(double percent, NotificationTier? expectedTier)
    {
        BudgetVsActualLine line = CreateLine("Comida", (decimal)percent);

        BudgetNotificationOutcome outcome = NotificationRules.Budgets([line], _monthStart, _now);

        if (expectedTier is null)
        {
            Assert.Empty(outcome.Candidates);
            return;
        }

        Notification candidate = Assert.Single(outcome.Candidates);
        Assert.Equal(NotificationKind.BudgetThreshold, candidate.Kind);
        Assert.Equal(expectedTier, candidate.Tier);
        Assert.Equal(line.BudgetId, candidate.SourceId);
        Assert.Equal(_monthStart, candidate.ReferenceDate);
        Assert.Equal("Comida", candidate.SubjectLabel);
        Assert.Equal((decimal)percent, candidate.Percent);
        Assert.Empty(outcome.UnknownBudgetIds);
    }

    [Fact]
    public void Budgets_IndeterminateLine_ProducesNoCandidateAndReportsBudgetId()
    {
        var indeterminate = new BudgetVsActualLine(
            Guid.NewGuid(), Guid.NewGuid(), "Viajes", BudgetDop: null, ActualDop: 500m, PercentConsumed: null, Status: null);
        BudgetVsActualLine known = CreateLine("Comida", 90m);

        BudgetNotificationOutcome outcome = NotificationRules.Budgets([indeterminate, known], _monthStart, _now);

        Notification candidate = Assert.Single(outcome.Candidates);
        Assert.Equal(known.BudgetId, candidate.SourceId);
        Guid unknownId = Assert.Single(outcome.UnknownBudgetIds);
        Assert.Equal(indeterminate.BudgetId, unknownId);
    }

    [Fact]
    public void Budgets_OkLine_IsNeitherCandidateNorUnknown()
    {
        BudgetVsActualLine line = CreateLine("Comida", 10m);

        BudgetNotificationOutcome outcome = NotificationRules.Budgets([line], _monthStart, _now);

        Assert.Empty(outcome.Candidates);
        Assert.Empty(outcome.UnknownBudgetIds);
    }

    // --- Anomalies: unacknowledged, 60-day window ---

    [Theory]
    [InlineData(0, true)] // today: in
    [InlineData(-60, true)] // exactly 60 days old: in
    [InlineData(-61, false)] // 61 days old: out (purge would resurrect it)
    public void Anomalies_AlertDateOffsetFromToday_RespectsRetentionWindow(int offsetDays, bool expectedIncluded)
    {
        AnomalyAlert alert = CreateAlert(_today.AddDays(offsetDays));

        IReadOnlyList<Notification> candidates = NotificationRules.Anomalies(
            [alert], new Dictionary<Guid, string>(), _today, _now);

        Assert.Equal(expectedIncluded, candidates.Count == 1);
    }

    [Fact]
    public void Anomalies_UnacknowledgedAlert_BuildsAnomalyCandidateWithCategoryLabel()
    {
        AnomalyAlert alert = CreateAlert(_today.AddDays(-2));
        var categoryNames = new Dictionary<Guid, string> { [alert.CategoryId] = "Restaurantes" };

        IReadOnlyList<Notification> candidates = NotificationRules.Anomalies([alert], categoryNames, _today, _now);

        Notification candidate = Assert.Single(candidates);
        Assert.Equal(NotificationKind.Anomaly, candidate.Kind);
        Assert.Equal(alert.Id, candidate.SourceId);
        Assert.Equal(alert.Date, candidate.ReferenceDate);
        Assert.Equal("Restaurantes", candidate.SubjectLabel);
    }

    [Fact]
    public void Anomalies_AcknowledgedAlert_IsExcluded()
    {
        AnomalyAlert alert = CreateAlert(_today.AddDays(-1));
        alert.Acknowledge(_now);

        IReadOnlyList<Notification> candidates = NotificationRules.Anomalies(
            [alert], new Dictionary<Guid, string>(), _today, _now);

        Assert.Empty(candidates);
    }

    [Fact]
    public void Anomalies_UnknownCategory_UsesEmptyLabel()
    {
        AnomalyAlert alert = CreateAlert(_today);

        IReadOnlyList<Notification> candidates = NotificationRules.Anomalies(
            [alert], new Dictionary<Guid, string>(), _today, _now);

        Assert.Equal(string.Empty, Assert.Single(candidates).SubjectLabel);
    }

    private static BudgetVsActualLine CreateLine(string categoryName, decimal percent)
    {
        BudgetStatus status =
            percent >= BudgetEvaluator.ExceededThresholdPercent ? BudgetStatus.Exceeded :
            percent >= BudgetEvaluator.NearThresholdPercent ? BudgetStatus.Near :
            BudgetStatus.Ok;

        return new BudgetVsActualLine(
            Guid.NewGuid(), Guid.NewGuid(), categoryName, 1000m, percent * 10m, percent, status);
    }

    private static AnomalyAlert CreateAlert(DateOnly date)
        => AnomalyAlert.Create(
            Guid.NewGuid(), Guid.NewGuid(), date, AnomalySeverity.Warning, "Gasto inusual", _now);

    private static DebtPayment CreatePayment(DateOnly scheduledDate)
    {
        var debt = Debt.Create(
            "Préstamo carro",
            Money.Create(1500m, Currency.DOP),
            Currency.DOP,
            totalInstallments: 1,
            installmentAmount: Money.Create(1500m, Currency.DOP),
            frequency: nameof(DebtFrequency.Monthly),
            startDate: scheduledDate);

        return new DebtScheduleGenerator().Generate(debt)[0];
    }

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

    private static RecurringRule CreateEndedRule(string name, DateOnly nextGenerationDate)
    {
        DateOnly startDate = nextGenerationDate.AddMonths(-1);
        RecurringRule rule = RecurringRule.Create(
            name,
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
            "Monthly",
            startDate: startDate,
            endDate: startDate,
            categoryId: Guid.NewGuid(),
            paymentMethodId: Guid.NewGuid());

        rule.AdvanceAfterGeneration(startDate, monthInterval: 1);
        return rule;
    }
}
