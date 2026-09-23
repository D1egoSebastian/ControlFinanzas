using ControlDeFinanzasIA.Domain.Alerts;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Alerts;

/// <summary>
/// Tests para las invariantes de <see cref="AnomalyAlert.Create"/> (R-3,
/// D-3), <see cref="AnomalyAlert.Refresh"/> y <see cref="AnomalyAlert.Acknowledge"/>.
/// </summary>
public sealed class AnomalyAlertTests
{
    private static readonly DateTime _detectedAt = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

    private static AnomalyAlert CreateValidAlert(
        Guid? transactionId = null,
        Guid? categoryId = null,
        DateOnly? date = null,
        AnomalySeverity severity = AnomalySeverity.Warning,
        string message = "Gasto en Supermercado 5.43x el promedio habitual.",
        DateTime? detectedAtUtc = null) =>
        AnomalyAlert.Create(
            transactionId ?? Guid.NewGuid(),
            categoryId ?? Guid.NewGuid(),
            date ?? new DateOnly(2026, 9, 14),
            severity,
            message,
            detectedAtUtc ?? _detectedAt);

    // --- Create invariants ---

    [Fact]
    public void Create_ValidAlert_ReturnsUnacknowledgedAlertWithProvidedValues()
    {
        var transactionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 14);

        var alert = AnomalyAlert.Create(
            transactionId, categoryId, date, AnomalySeverity.Critical, "Mensaje de prueba", _detectedAt);

        Assert.NotEqual(Guid.Empty, alert.Id);
        Assert.Equal(transactionId, alert.TransactionId);
        Assert.Equal(categoryId, alert.CategoryId);
        Assert.Equal(date, alert.Date);
        Assert.Equal(AnomalySeverity.Critical, alert.Severity);
        Assert.Equal("Mensaje de prueba", alert.Message);
        Assert.Equal(_detectedAt, alert.DetectedAt);
        Assert.Null(alert.AcknowledgedAt);
        Assert.False(alert.IsAcknowledged);
    }

    [Fact]
    public void Create_EmptyTransactionId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AnomalyAlert.Create(
            Guid.Empty, Guid.NewGuid(), new DateOnly(2026, 9, 14), AnomalySeverity.Info, "msg", _detectedAt));
    }

    [Fact]
    public void Create_EmptyCategoryId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AnomalyAlert.Create(
            Guid.NewGuid(), Guid.Empty, new DateOnly(2026, 9, 14), AnomalySeverity.Info, "msg", _detectedAt));
    }

    [Fact]
    public void Create_UndefinedSeverity_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AnomalyAlert.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 14), (AnomalySeverity)99, "msg", _detectedAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhitespaceMessage_ThrowsArgumentException(string? message)
    {
        Assert.Throws<ArgumentException>(() => AnomalyAlert.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 14), AnomalySeverity.Info, message!, _detectedAt));
    }

    [Fact]
    public void Create_MessageTooLong_ThrowsArgumentException()
    {
        string tooLong = new('a', AnomalyAlert.MaxMessageLength + 1);

        Assert.Throws<ArgumentException>(() => AnomalyAlert.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 14), AnomalySeverity.Info, tooLong, _detectedAt));
    }

    [Fact]
    public void Create_MessageAtMaxLength_DoesNotThrow()
    {
        string maxLength = new('a', AnomalyAlert.MaxMessageLength);

        var alert = AnomalyAlert.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 14), AnomalySeverity.Info, maxLength, _detectedAt);

        Assert.Equal(maxLength, alert.Message);
    }

    [Fact]
    public void Create_DetectedAtDefault_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AnomalyAlert.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 14), AnomalySeverity.Info, "msg", default));
    }

    // --- Refresh ---

    [Fact]
    public void Refresh_Unacknowledged_UpdatesFieldsKeepingStableId()
    {
        AnomalyAlert alert = CreateValidAlert(severity: AnomalySeverity.Info, message: "original");
        Guid originalId = alert.Id;
        var newDate = new DateOnly(2026, 9, 20);

        alert.Refresh(newDate, AnomalySeverity.Critical, "updated");

        Assert.Equal(originalId, alert.Id);
        Assert.Equal(newDate, alert.Date);
        Assert.Equal(AnomalySeverity.Critical, alert.Severity);
        Assert.Equal("updated", alert.Message);
    }

    [Fact]
    public void Refresh_Acknowledged_ThrowsInvalidOperationException()
    {
        AnomalyAlert alert = CreateValidAlert();
        alert.Acknowledge(_detectedAt);

        Assert.Throws<InvalidOperationException>(
            () => alert.Refresh(new DateOnly(2026, 9, 20), AnomalySeverity.Critical, "updated"));
    }

    // --- Acknowledge ---

    [Fact]
    public void Acknowledge_FirstCall_SetsAcknowledgedAtAndIsAcknowledged()
    {
        AnomalyAlert alert = CreateValidAlert();
        var acknowledgedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc);

        alert.Acknowledge(acknowledgedAt);

        Assert.Equal(acknowledgedAt, alert.AcknowledgedAt);
        Assert.True(alert.IsAcknowledged);
    }

    [Fact]
    public void Acknowledge_SecondCall_KeepsFirstTimestamp()
    {
        AnomalyAlert alert = CreateValidAlert();
        var firstAcknowledgedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc);
        var secondAcknowledgedAt = new DateTime(2026, 9, 25, 9, 0, 0, DateTimeKind.Utc);

        alert.Acknowledge(firstAcknowledgedAt);
        alert.Acknowledge(secondAcknowledgedAt);

        Assert.Equal(firstAcknowledgedAt, alert.AcknowledgedAt);
    }
}
