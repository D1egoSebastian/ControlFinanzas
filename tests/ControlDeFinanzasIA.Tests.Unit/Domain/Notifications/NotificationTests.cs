using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Notifications;

/// <summary>
/// Tests para las invariantes de las factories por tipo de
/// <see cref="Notification"/> (R-1, R-11, D-1), <see cref="Notification.MarkRead"/>,
/// <see cref="Notification.Dismiss"/>, <see cref="Notification.RefreshFacts"/> y
/// <see cref="Notification.HasSameFacts"/>.
/// </summary>
public sealed class NotificationTests
{
    private static readonly DateTime _createdAt = new(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc);

    // --- ForDebtInstallment ---

    [Fact]
    public void ForDebtInstallment_ValidInputs_SetsOnlyDebtFacts()
    {
        var paymentId = Guid.NewGuid();
        var dueDate = new DateOnly(2026, 9, 29);
        var amount = Money.Create(1500m, Currency.DOP);

        var notification = Notification.ForDebtInstallment(
            paymentId, dueDate, "  Cuota   Auto  ", amount, _createdAt);

        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.Equal(NotificationKind.DebtInstallmentDue, notification.Kind);
        Assert.Equal(NotificationKeys.ForDebtInstallment(paymentId), notification.DedupKey);
        Assert.Equal(paymentId, notification.SourceId);
        Assert.Equal(dueDate, notification.ReferenceDate);
        Assert.Equal("Cuota Auto", notification.SubjectLabel);
        Assert.Equal(1500m, notification.Amount);
        Assert.Equal(Currency.DOP, notification.Currency);
        Assert.Equal(_createdAt, notification.CreatedAt);
        Assert.Null(notification.ReadAt);
        Assert.Null(notification.DismissedAt);
        Assert.False(notification.IsRead);
        Assert.False(notification.IsDismissed);

        // Rejects facts belonging to other kinds: they stay unset.
        Assert.Null(notification.Percent);
        Assert.Null(notification.Tier);
        Assert.Null(notification.DigestFrequency);
        Assert.Null(notification.DigestPaymentCount);
        Assert.Null(notification.DigestBudgetCount);
        Assert.Null(notification.DigestAnomalyCount);
        Assert.Empty(notification.DigestTotals);
    }

    [Fact]
    public void ForDebtInstallment_EmptyPaymentId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Notification.ForDebtInstallment(
            Guid.Empty, new DateOnly(2026, 9, 29), "label", Money.Create(10m, Currency.DOP), _createdAt));
    }

    [Fact]
    public void ForDebtInstallment_NullAmount_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Notification.ForDebtInstallment(
            Guid.NewGuid(), new DateOnly(2026, 9, 29), "label", null!, _createdAt));
    }

    [Fact]
    public void ForDebtInstallment_DefaultCreatedAt_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Notification.ForDebtInstallment(
            Guid.NewGuid(), new DateOnly(2026, 9, 29), "label", Money.Create(10m, Currency.DOP), default));
    }

    // --- ForRecurringCharge ---

    [Fact]
    public void ForRecurringCharge_ValidInputs_SetsOnlyRecurringFacts()
    {
        var ruleId = Guid.NewGuid();
        var chargeDate = new DateOnly(2026, 9, 27);
        var amount = Money.Create(500m, Currency.USD);

        var notification = Notification.ForRecurringCharge(
            ruleId, chargeDate, "Netflix", amount, _createdAt);

        Assert.Equal(NotificationKind.RecurringChargeDue, notification.Kind);
        Assert.Equal(NotificationKeys.ForRecurringCharge(ruleId, chargeDate), notification.DedupKey);
        Assert.Equal(ruleId, notification.SourceId);
        Assert.Equal(chargeDate, notification.ReferenceDate);
        Assert.Equal("Netflix", notification.SubjectLabel);
        Assert.Equal(500m, notification.Amount);
        Assert.Equal(Currency.USD, notification.Currency);
        Assert.Null(notification.Tier);
        Assert.Null(notification.DigestFrequency);
        Assert.Empty(notification.DigestTotals);
    }

    [Fact]
    public void ForRecurringCharge_EmptyRuleId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Notification.ForRecurringCharge(
            Guid.Empty, new DateOnly(2026, 9, 27), "label", Money.Create(10m, Currency.DOP), _createdAt));
    }

    [Fact]
    public void ForRecurringCharge_NullAmount_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Notification.ForRecurringCharge(
            Guid.NewGuid(), new DateOnly(2026, 9, 27), "label", null!, _createdAt));
    }

    // --- ForBudget ---

    [Fact]
    public void ForBudget_ValidInputs_SetsOnlyBudgetFacts()
    {
        var budgetId = Guid.NewGuid();
        var monthStart = new DateOnly(2026, 9, 1);

        var notification = Notification.ForBudget(
            budgetId, monthStart, "Comida", 85.5m, NotificationTier.Near, _createdAt);

        Assert.Equal(NotificationKind.BudgetThreshold, notification.Kind);
        Assert.Equal(NotificationKeys.ForBudget(budgetId, monthStart, NotificationTier.Near), notification.DedupKey);
        Assert.Equal(budgetId, notification.SourceId);
        Assert.Equal(monthStart, notification.ReferenceDate);
        Assert.Equal(85.5m, notification.Percent);
        Assert.Equal(NotificationTier.Near, notification.Tier);
        Assert.Null(notification.Amount);
        Assert.Null(notification.Currency);
        Assert.Null(notification.DigestFrequency);
        Assert.Empty(notification.DigestTotals);
    }

    [Fact]
    public void ForBudget_EmptyBudgetId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Notification.ForBudget(
            Guid.Empty, new DateOnly(2026, 9, 1), "label", 90m, NotificationTier.Near, _createdAt));
    }

    [Fact]
    public void ForBudget_UndefinedTier_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Notification.ForBudget(
            Guid.NewGuid(), new DateOnly(2026, 9, 1), "label", 90m, (NotificationTier)99, _createdAt));
    }

    // --- ForAnomaly ---

    [Fact]
    public void ForAnomaly_ValidInputs_SetsOnlyAnomalyFacts()
    {
        var alertId = Guid.NewGuid();
        var alertDate = new DateOnly(2026, 9, 20);

        var notification = Notification.ForAnomaly(alertId, alertDate, "Supermercado", _createdAt);

        Assert.Equal(NotificationKind.Anomaly, notification.Kind);
        Assert.Equal(NotificationKeys.ForAnomaly(alertId), notification.DedupKey);
        Assert.Equal(alertId, notification.SourceId);
        Assert.Equal(alertDate, notification.ReferenceDate);
        Assert.Null(notification.Amount);
        Assert.Null(notification.Percent);
        Assert.Null(notification.Tier);
        Assert.Null(notification.DigestFrequency);
        Assert.Empty(notification.DigestTotals);
    }

    [Fact]
    public void ForAnomaly_EmptyAlertId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Notification.ForAnomaly(
            Guid.Empty, new DateOnly(2026, 9, 20), "label", _createdAt));
    }

    // --- ForDigest ---

    [Fact]
    public void ForDigest_ValidInputs_SetsOnlyDigestFacts()
    {
        var periodStart = new DateOnly(2026, 9, 21);
        var totals = new List<NotificationCurrencyTotal>
        {
            new(Currency.DOP, 3200m),
            new(Currency.USD, 40m)
        };

        var notification = Notification.ForDigest(
            DigestFrequency.Weekly, periodStart, payments: 3, totals, budgets: 2, anomalies: 1, _createdAt);

        Assert.Equal(NotificationKind.Digest, notification.Kind);
        Assert.Equal(NotificationKeys.ForDigest(DigestFrequency.Weekly, periodStart), notification.DedupKey);
        Assert.Null(notification.SourceId);
        Assert.Equal(periodStart, notification.ReferenceDate);
        Assert.Equal(DigestFrequency.Weekly, notification.DigestFrequency);
        Assert.Equal(3, notification.DigestPaymentCount);
        Assert.Equal(2, notification.DigestBudgetCount);
        Assert.Equal(1, notification.DigestAnomalyCount);
        Assert.Equal(totals, notification.DigestTotals);
        Assert.Null(notification.Amount);
        Assert.Null(notification.Percent);
        Assert.Null(notification.Tier);
    }

    [Fact]
    public void ForDigest_OffFrequency_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Notification.ForDigest(
            DigestFrequency.Off, new DateOnly(2026, 9, 21), 0, [], 0, 0, _createdAt));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    public void ForDigest_NegativeCount_ThrowsArgumentOutOfRangeException(int payments, int budgets, int anomalies)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Notification.ForDigest(
            DigestFrequency.Weekly, new DateOnly(2026, 9, 21), payments, [], budgets, anomalies, _createdAt));
    }

    [Fact]
    public void ForDigest_NullTotals_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Notification.ForDigest(
            DigestFrequency.Weekly, new DateOnly(2026, 9, 21), 0, null!, 0, 0, _createdAt));
    }

    // --- SanitizeLabel (via any factory) ---

    [Theory]
    [InlineData("Netflix", "Netflix")]
    [InlineData("  Netflix  ", "Netflix")]
    [InlineData("Netflix\tMensual\n", "Netflix Mensual")]
    [InlineData("A   B", "A B")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SanitizeLabel_NormalizesControlCharsWhitespaceAndTrims(string? input, string expected)
    {
        var notification = Notification.ForRecurringCharge(
            Guid.NewGuid(), new DateOnly(2026, 9, 27), input!, Money.Create(10m, Currency.DOP), _createdAt);

        Assert.Equal(expected, notification.SubjectLabel);
    }

    [Fact]
    public void SanitizeLabel_LongerThan120Chars_CutsAt120()
    {
        string longLabel = new('a', 200);

        var notification = Notification.ForRecurringCharge(
            Guid.NewGuid(), new DateOnly(2026, 9, 27), longLabel, Money.Create(10m, Currency.DOP), _createdAt);

        Assert.Equal(120, notification.SubjectLabel.Length);
        Assert.Equal(new string('a', 120), notification.SubjectLabel);
    }

    // --- MarkRead ---

    [Fact]
    public void MarkRead_FirstCall_SetsReadAt()
    {
        Notification notification = CreateDebtNotification();
        var readAt = new DateTime(2026, 9, 27, 8, 0, 0, DateTimeKind.Utc);

        notification.MarkRead(readAt);

        Assert.Equal(readAt, notification.ReadAt);
        Assert.True(notification.IsRead);
    }

    [Fact]
    public void MarkRead_SecondCall_KeepsFirstTimestamp()
    {
        Notification notification = CreateDebtNotification();
        var first = new DateTime(2026, 9, 27, 8, 0, 0, DateTimeKind.Utc);
        var second = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);

        notification.MarkRead(first);
        notification.MarkRead(second);

        Assert.Equal(first, notification.ReadAt);
    }

    // --- Dismiss ---

    [Fact]
    public void Dismiss_FirstCall_SetsDismissedAt()
    {
        Notification notification = CreateDebtNotification();
        var dismissedAt = new DateTime(2026, 9, 27, 9, 0, 0, DateTimeKind.Utc);

        notification.Dismiss(dismissedAt);

        Assert.Equal(dismissedAt, notification.DismissedAt);
        Assert.True(notification.IsDismissed);
    }

    [Fact]
    public void Dismiss_SecondCall_KeepsFirstTimestamp()
    {
        Notification notification = CreateDebtNotification();
        var first = new DateTime(2026, 9, 27, 9, 0, 0, DateTimeKind.Utc);
        var second = new DateTime(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc);

        notification.Dismiss(first);
        notification.Dismiss(second);

        Assert.Equal(first, notification.DismissedAt);
    }

    // --- RefreshFacts ---

    [Fact]
    public void RefreshFacts_Undismissed_UpdatesFactsButNeverTouchesIdCreatedAtOrReadAt()
    {
        Notification notification = CreateDebtNotification();
        Guid originalId = notification.Id;
        DateTime originalCreatedAt = notification.CreatedAt;
        var readAt = new DateTime(2026, 9, 27, 8, 0, 0, DateTimeKind.Utc);
        notification.MarkRead(readAt);

        var refreshedCandidate = Notification.ForDebtInstallment(
            notification.SourceId!.Value,
            notification.ReferenceDate,
            "Cuota actualizada",
            Money.Create(9999m, Currency.DOP),
            new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc));

        notification.RefreshFacts(refreshedCandidate);

        Assert.Equal(originalId, notification.Id);
        Assert.Equal(originalCreatedAt, notification.CreatedAt);
        Assert.Equal(readAt, notification.ReadAt);
        Assert.Equal(9999m, notification.Amount);
        Assert.Equal("Cuota actualizada", notification.SubjectLabel);
    }

    [Fact]
    public void RefreshFacts_Dismissed_ThrowsInvalidOperationException()
    {
        Notification notification = CreateDebtNotification();
        notification.Dismiss(new DateTime(2026, 9, 27, 9, 0, 0, DateTimeKind.Utc));

        var candidate = Notification.ForDebtInstallment(
            notification.SourceId!.Value,
            notification.ReferenceDate,
            "otro",
            Money.Create(1m, Currency.DOP),
            _createdAt);

        Assert.Throws<InvalidOperationException>(() => notification.RefreshFacts(candidate));
    }

    // --- HasSameFacts ---

    [Fact]
    public void HasSameFacts_SameFactsDifferentIdAndCreatedAt_ReturnsTrue()
    {
        var paymentId = Guid.NewGuid();
        var dueDate = new DateOnly(2026, 9, 29);
        var amount = Money.Create(1500m, Currency.DOP);

        var first = Notification.ForDebtInstallment(paymentId, dueDate, "Cuota", amount, _createdAt);
        var second = Notification.ForDebtInstallment(
            paymentId, dueDate, "Cuota", amount, _createdAt.AddDays(1));

        Assert.True(first.HasSameFacts(second));
    }

    [Fact]
    public void HasSameFacts_DifferentAmount_ReturnsFalse()
    {
        var paymentId = Guid.NewGuid();
        var dueDate = new DateOnly(2026, 9, 29);

        var first = Notification.ForDebtInstallment(
            paymentId, dueDate, "Cuota", Money.Create(1500m, Currency.DOP), _createdAt);
        var second = Notification.ForDebtInstallment(
            paymentId, dueDate, "Cuota", Money.Create(1600m, Currency.DOP), _createdAt);

        Assert.False(first.HasSameFacts(second));
    }

    private static Notification CreateDebtNotification() =>
        Notification.ForDebtInstallment(
            Guid.NewGuid(), new DateOnly(2026, 9, 29), "Cuota", Money.Create(1500m, Currency.DOP), _createdAt);
}
