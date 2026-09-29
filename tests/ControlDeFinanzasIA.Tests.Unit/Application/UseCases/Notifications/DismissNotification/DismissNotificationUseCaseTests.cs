using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Notifications.DismissNotification;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Notifications.DismissNotification;

/// <summary>
/// Unit tests for <see cref="DismissNotificationUseCase"/> (F-017, slice S4,
/// R-8, D-6): dismisses one row permanently, is idempotent, and treats a row
/// that vanished between the list load and the click as a successful no-op.
/// </summary>
public sealed class DismissNotificationUseCaseTests
{
    private static readonly DateTime _created = new(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _later = new(2026, 9, 26, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_ExistingRow_IsDismissedWithClockTimeAndSaved()
    {
        var repository = new FakeNotificationRepository();
        Notification row = Debt();
        repository.Seed(row);

        Result result = await CreateUseCase(repository).ExecuteAsync(row.Id);

        Assert.True(result.IsSuccess);
        Assert.True(row.IsDismissed);
        Assert.Equal(_later, row.DismissedAt);
        Assert.Equal(1, repository.UpdateRangeCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_MissingRow_ReturnsSuccessWithoutSaving()
    {
        var repository = new FakeNotificationRepository();

        Result result = await CreateUseCase(repository).ExecuteAsync(Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, repository.UpdateRangeCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyDismissedRow_KeepsFirstTimestampAndSucceeds()
    {
        var repository = new FakeNotificationRepository();
        Notification row = Debt();
        row.Dismiss(_created);
        repository.Seed(row);

        Result result = await CreateUseCase(repository).ExecuteAsync(row.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(_created, row.DismissedAt);
        Assert.Equal(0, repository.UpdateRangeCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_SaveFails_PropagatesTheFailure()
    {
        var repository = new FakeNotificationRepository
        {
            UpdateRangeResult = Result.Failure("Conflict", "Save failed."),
        };
        Notification row = Debt();
        repository.Seed(row);

        Result result = await CreateUseCase(repository).ExecuteAsync(row.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Conflict", result.Error?.Code);
    }

    private static DismissNotificationUseCase CreateUseCase(FakeNotificationRepository repository)
        => new(repository, new FakeClock { Today = new DateOnly(2026, 9, 26), UtcNow = _later });

    private static Notification Debt()
        => Notification.ForDebtInstallment(
            Guid.NewGuid(), new DateOnly(2026, 9, 27), "Préstamo", Money.Create(100m, Currency.DOP), _created);
}
