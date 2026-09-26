using ControlDeFinanzasIA.Domain.Notifications;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Notifications;

/// <summary>
/// Tests para la forma exacta del <c>DedupKey</c> de cada tipo de aviso
/// (F-017, R-1, D-2).
/// </summary>
public sealed class NotificationKeysTests
{
    [Fact]
    public void ForDebtInstallment_ReturnsDebtPrefixedKeyWithNFormatGuid()
    {
        var paymentId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        string key = NotificationKeys.ForDebtInstallment(paymentId);

        Assert.Equal("debt:11111111222233334444555555555555", key);
    }

    [Fact]
    public void ForRecurringCharge_ReturnsRecurringPrefixedKeyWithInvariantDate()
    {
        var ruleId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var chargeDate = new DateOnly(2026, 9, 5);

        string key = NotificationKeys.ForRecurringCharge(ruleId, chargeDate);

        Assert.Equal("recurring:11111111222233334444555555555555:2026-09-05", key);
    }

    [Theory]
    [InlineData(NotificationTier.Near, "Near")]
    [InlineData(NotificationTier.Exceeded, "Exceeded")]
    public void ForBudget_ReturnsBudgetPrefixedKeyWithInvariantMonthAndTier(
        NotificationTier tier, string expectedTierLiteral)
    {
        var budgetId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var monthStart = new DateOnly(2026, 9, 1);

        string key = NotificationKeys.ForBudget(budgetId, monthStart, tier);

        Assert.Equal(
            $"budget:11111111222233334444555555555555:2026-09:{expectedTierLiteral}", key);
    }

    [Fact]
    public void ForAnomaly_ReturnsAnomalyPrefixedKeyWithNFormatGuid()
    {
        var alertId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        string key = NotificationKeys.ForAnomaly(alertId);

        Assert.Equal("anomaly:11111111222233334444555555555555", key);
    }

    [Theory]
    [InlineData(DigestFrequency.Daily, "Daily")]
    [InlineData(DigestFrequency.Weekly, "Weekly")]
    public void ForDigest_ReturnsDigestPrefixedKeyWithFrequencyAndInvariantDate(
        DigestFrequency frequency, string expectedFrequencyLiteral)
    {
        var periodStart = new DateOnly(2026, 9, 21);

        string key = NotificationKeys.ForDigest(frequency, periodStart);

        Assert.Equal($"digest:{expectedFrequencyLiteral}:2026-09-21", key);
    }

    [Fact]
    public void ForDebtInstallment_CalledTwiceWithSameInput_IsDeterministic()
    {
        var paymentId = Guid.NewGuid();

        string first = NotificationKeys.ForDebtInstallment(paymentId);
        string second = NotificationKeys.ForDebtInstallment(paymentId);

        Assert.Equal(first, second);
    }
}
