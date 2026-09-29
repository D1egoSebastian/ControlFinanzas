using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Notifications;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Notifications.GetNotifications;

/// <summary>
/// Unit tests for <see cref="GetNotificationsQuery"/> (F-017, slice S4, R-10,
/// D-6): undismissed rows only, unread first then reference date ascending
/// then kind then id, capped at <see cref="NotificationRules.FlyoutCap"/>,
/// with an unread count that ignores the cap.
/// </summary>
public sealed class GetNotificationsQueryTests
{
    private static readonly DateOnly _today = new(2026, 9, 26);
    private static readonly DateTime _now = new(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_NoRows_ReturnsEmptyListAndZeroUnread()
    {
        GetNotificationsQuery query = new(new FakeNotificationRepository());

        Result result = await query.ExecuteAsync();

        Assert.True(result.IsSuccess);
        NotificationListDto list = Assert.IsType<NotificationListDto>(result.Value);
        Assert.Empty(list.Items);
        Assert.Equal(0, list.UnreadCount);
    }

    [Fact]
    public async Task ExecuteAsync_DismissedRows_AreExcludedFromItemsAndUnreadCount()
    {
        var repository = new FakeNotificationRepository();
        Notification dismissed = Debt(_today.AddDays(1));
        dismissed.Dismiss(_now);
        Notification live = Debt(_today.AddDays(2));
        repository.SeedRange([dismissed, live]);

        Result result = await new GetNotificationsQuery(repository).ExecuteAsync();

        NotificationListDto list = Assert.IsType<NotificationListDto>(result.Value);
        NotificationDto only = Assert.Single(list.Items);
        Assert.Equal(live.Id, only.Id);
        Assert.Equal(1, list.UnreadCount);
    }

    [Fact]
    public async Task ExecuteAsync_MixedReadState_ListsUnreadFirstThenReferenceDateAscending()
    {
        var repository = new FakeNotificationRepository();
        Notification readEarly = Debt(_today.AddDays(-5));
        readEarly.MarkRead(_now);
        Notification unreadLate = Debt(_today.AddDays(3));
        Notification unreadEarly = Debt(_today.AddDays(-1));
        repository.SeedRange([readEarly, unreadLate, unreadEarly]);

        Result result = await new GetNotificationsQuery(repository).ExecuteAsync();

        NotificationListDto list = Assert.IsType<NotificationListDto>(result.Value);
        Assert.Equal(
            new[] { unreadEarly.Id, unreadLate.Id, readEarly.Id },
            list.Items.Select(i => i.Id).ToArray());
        Assert.Equal(new[] { false, false, true }, list.Items.Select(i => i.IsRead).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_SameReferenceDate_OrdersByKindThenId()
    {
        var repository = new FakeNotificationRepository();
        var anomaly = Notification.ForAnomaly(Guid.NewGuid(), _today, "Comida", _now);
        Notification debtA = Debt(_today);
        Notification debtB = Debt(_today);
        repository.SeedRange([anomaly, debtB, debtA]);

        Result result = await new GetNotificationsQuery(repository).ExecuteAsync();

        NotificationListDto list = Assert.IsType<NotificationListDto>(result.Value);
        Guid[] debtIds = new[] { debtA.Id, debtB.Id }.OrderBy(id => id).ToArray();
        Assert.Equal(
            new[] { debtIds[0], debtIds[1], anomaly.Id },
            list.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_MoreThanCapRows_CapsItemsButUnreadCountCountsAll()
    {
        var repository = new FakeNotificationRepository();
        for (int i = 0; i < NotificationRules.FlyoutCap + 5; i++)
        {
            repository.Seed(Debt(_today.AddDays(i % 3)));
        }

        Result result = await new GetNotificationsQuery(repository).ExecuteAsync();

        NotificationListDto list = Assert.IsType<NotificationListDto>(result.Value);
        Assert.Equal(NotificationRules.FlyoutCap, list.Items.Count);
        Assert.Equal(NotificationRules.FlyoutCap + 5, list.UnreadCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReadRows_DoNotCountTowardUnread()
    {
        var repository = new FakeNotificationRepository();
        Notification read = Debt(_today);
        read.MarkRead(_now);
        repository.SeedRange([read, Debt(_today.AddDays(1))]);

        Result result = await new GetNotificationsQuery(repository).ExecuteAsync();

        NotificationListDto list = Assert.IsType<NotificationListDto>(result.Value);
        Assert.Equal(2, list.Items.Count);
        Assert.Equal(1, list.UnreadCount);
    }

    [Fact]
    public async Task ExecuteAsync_BudgetRow_MapsEveryFactToTheDto()
    {
        var repository = new FakeNotificationRepository();
        var monthStart = new DateOnly(2026, 9, 1);
        var budget = Notification.ForBudget(
            Guid.NewGuid(), monthStart, "Comida", 85.5m, NotificationTier.Near, _now);
        repository.Seed(budget);

        Result result = await new GetNotificationsQuery(repository).ExecuteAsync();

        NotificationListDto list = Assert.IsType<NotificationListDto>(result.Value);
        NotificationDto dto = Assert.Single(list.Items);
        Assert.Equal(budget.Id, dto.Id);
        Assert.Equal(NotificationKind.BudgetThreshold, dto.Kind);
        Assert.Equal(budget.SourceId, dto.SourceId);
        Assert.Equal(monthStart, dto.ReferenceDate);
        Assert.Equal("Comida", dto.SubjectLabel);
        Assert.Equal(85.5m, dto.Percent);
        Assert.Equal(NotificationTier.Near, dto.Tier);
        Assert.Null(dto.Amount);
        Assert.False(dto.IsRead);
    }

    [Fact]
    public async Task ExecuteAsync_DueRow_MapsAmountAndCurrency()
    {
        var repository = new FakeNotificationRepository();
        var due = Notification.ForRecurringCharge(
            Guid.NewGuid(), _today, "Netflix", Money.Create(500m, Currency.USD), _now);
        repository.Seed(due);

        Result result = await new GetNotificationsQuery(repository).ExecuteAsync();

        NotificationDto dto = Assert.Single(Assert.IsType<NotificationListDto>(result.Value).Items);
        Assert.Equal(500m, dto.Amount);
        Assert.Equal(Currency.USD, dto.Currency);
    }

    private static Notification Debt(DateOnly dueDate)
        => Notification.ForDebtInstallment(
            Guid.NewGuid(), dueDate, "Préstamo", Money.Create(100m, Currency.DOP), _now);
}
