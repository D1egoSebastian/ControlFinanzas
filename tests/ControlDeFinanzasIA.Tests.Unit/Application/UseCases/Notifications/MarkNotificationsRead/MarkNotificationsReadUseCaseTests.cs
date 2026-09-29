using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Notifications.MarkNotificationsRead;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Notifications.MarkNotificationsRead;

/// <summary>
/// Unit tests for <see cref="MarkNotificationsReadUseCase"/> (F-017, slice
/// S4, R-8, D-6): marks only the requested rows read in one save, keeps the
/// first read timestamp, and silently ignores ids that no longer exist.
/// </summary>
public sealed class MarkNotificationsReadUseCaseTests
{
    private static readonly DateTime _created = new(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _later = new(2026, 9, 26, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_ListedUnreadRows_AreMarkedReadWithClockTime()
    {
        var repository = new FakeNotificationRepository();
        Notification a = Debt();
        Notification b = Debt();
        Notification untouched = Debt();
        repository.SeedRange([a, b, untouched]);

        Result result = await CreateUseCase(repository).ExecuteAsync([a.Id, b.Id]);

        Assert.True(result.IsSuccess);
        Assert.Equal(_later, a.ReadAt);
        Assert.Equal(_later, b.ReadAt);
        Assert.False(untouched.IsRead);
        Assert.Equal(1, repository.UpdateRangeCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyReadRow_KeepsFirstTimestampAndDoesNotSave()
    {
        var repository = new FakeNotificationRepository();
        Notification alreadyRead = Debt();
        alreadyRead.MarkRead(_created);
        repository.Seed(alreadyRead);

        Result result = await CreateUseCase(repository).ExecuteAsync([alreadyRead.Id]);

        Assert.True(result.IsSuccess);
        Assert.Equal(_created, alreadyRead.ReadAt);
        Assert.Equal(0, repository.UpdateRangeCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownOrPurgedIds_AreSilentlyIgnored()
    {
        var repository = new FakeNotificationRepository();
        Notification present = Debt();
        repository.Seed(present);

        Result result = await CreateUseCase(repository).ExecuteAsync([Guid.NewGuid(), present.Id]);

        Assert.True(result.IsSuccess);
        Assert.True(present.IsRead);
    }

    [Fact]
    public async Task ExecuteAsync_OnlyUnknownIds_SucceedsWithoutSaving()
    {
        var repository = new FakeNotificationRepository();

        Result result = await CreateUseCase(repository).ExecuteAsync([Guid.NewGuid()]);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, repository.UpdateRangeCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyIds_SucceedsWithoutTouchingTheRepository()
    {
        var repository = new FakeNotificationRepository();

        Result result = await CreateUseCase(repository).ExecuteAsync([]);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, repository.UpdateRangeCallCount);
        Assert.Equal(0, repository.GetByIdsCallCount);
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

        Result result = await CreateUseCase(repository).ExecuteAsync([row.Id]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Conflict", result.Error?.Code);
    }

    private static MarkNotificationsReadUseCase CreateUseCase(FakeNotificationRepository repository)
        => new(repository, new FakeClock { Today = new DateOnly(2026, 9, 26), UtcNow = _later });

    private static Notification Debt()
        => Notification.ForDebtInstallment(
            Guid.NewGuid(), new DateOnly(2026, 9, 27), "Préstamo", Money.Create(100m, Currency.DOP), _created);
}
