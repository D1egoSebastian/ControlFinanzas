using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.AcknowledgeAnomaly;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Dashboard;

/// <summary>
/// Unit tests for <see cref="AcknowledgeAnomalyUseCase"/> (F-014, slice S4,
/// D-7): the per-row dismiss action behind <c>GetDashboardInsightsQuery
/// .DismissAnomalyAsync</c>.
/// </summary>
public sealed class AcknowledgeAnomalyUseCaseTests
{
    private static readonly DateTime _fixedDetectedAt = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_UnknownId_ReturnsNotFoundFailure()
    {
        FakeAnomalyAlertRepository repo = new();
        FakeClock clock = new() { UtcNow = new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc) };
        AcknowledgeAnomalyUseCase useCase = new(repo, clock);

        Result result = await useCase.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyAcknowledged_IsIdempotentAndKeepsFirstTimestamp()
    {
        var originalAcknowledgedAt = new DateTime(2026, 3, 9, 7, 0, 0, DateTimeKind.Utc);
        var alert = AnomalyAlert.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 3, 5), AnomalySeverity.Warning, "Msg", _fixedDetectedAt);
        alert.Acknowledge(originalAcknowledgedAt);

        FakeAnomalyAlertRepository repo = new();
        repo.Seed(alert);
        FakeClock clock = new() { UtcNow = new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc) };
        AcknowledgeAnomalyUseCase useCase = new(repo, clock);

        Result result = await useCase.ExecuteAsync(alert.Id);

        Assert.True(result.IsSuccess);
        AnomalyAlert persisted = Assert.Single(repo.Alerts);
        Assert.Equal(originalAcknowledgedAt, persisted.AcknowledgedAt);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_AcknowledgesAndPersists()
    {
        var alert = AnomalyAlert.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 3, 5), AnomalySeverity.Warning, "Msg", _fixedDetectedAt);

        FakeAnomalyAlertRepository repo = new();
        repo.Seed(alert);
        var acknowledgeAt = new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc);
        FakeClock clock = new() { UtcNow = acknowledgeAt };
        AcknowledgeAnomalyUseCase useCase = new(repo, clock);

        Result result = await useCase.ExecuteAsync(alert.Id);

        Assert.True(result.IsSuccess);
        AnomalyAlert persisted = Assert.Single(repo.Alerts);
        Assert.True(persisted.IsAcknowledged);
        Assert.Equal(acknowledgeAt, persisted.AcknowledgedAt);
    }
}
