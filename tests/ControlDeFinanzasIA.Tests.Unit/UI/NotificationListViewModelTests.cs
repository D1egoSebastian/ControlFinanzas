using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for <see cref="NotificationListViewModel"/> (F-017, R-8, R-10, R-14, S-11, D-10, slice S5a).
/// </summary>
public sealed class NotificationListViewModelTests
{
    private static readonly DateOnly _today = new(2026, 9, 28);

    private static NotificationDto Dto(Guid? id = null, bool isRead = false) =>
        new(
            id ?? Guid.NewGuid(),
            NotificationKind.Anomaly,
            Guid.NewGuid(),
            _today,
            "Supermercado",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Array.Empty<NotificationCurrencyTotal>(),
            isRead);

    private static (NotificationListViewModel Vm, FakeNotificationCenter Center) CreateSut()
    {
        var center = new FakeNotificationCenter();
        var vm = new NotificationListViewModel(
            center,
            new FakeClock { Today = _today },
            NullLogger<NotificationListViewModel>.Instance);
        return (vm, center);
    }

    private static NotificationListDto ListOf(int unread, int read = 0)
    {
        var items = new List<NotificationDto>();
        items.AddRange(Enumerable.Range(0, unread).Select(_ => Dto()));
        items.AddRange(Enumerable.Range(0, read).Select(_ => Dto(isRead: true)));
        return new NotificationListDto(items, unread);
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "1")]
    [InlineData(9, "9")]
    [InlineData(10, "9+")]
    [InlineData(12, "9+")]
    public async Task BadgeText_FollowsUnreadCount(int unread, string expected)
    {
        (NotificationListViewModel vm, FakeNotificationCenter center) = CreateSut();
        center.ListToReturn = ListOf(unread);

        await vm.LoadAsync();

        Assert.Equal(expected, vm.BadgeText);
        Assert.Equal(unread > 0, vm.HasUnread);
    }

    [Fact]
    public async Task ChangedEvent_ReloadsTheList()
    {
        (NotificationListViewModel vm, FakeNotificationCenter center) = CreateSut();
        center.ListToReturn = ListOf(2, 1);

        center.RaiseChanged();
        await Task.Yield();

        Assert.Equal(3, vm.Items.Count);
        Assert.Equal(2, vm.UnreadCount);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task Load_FormatsRowsThroughTheFormatter_AndKeepsUnreadFlag()
    {
        (NotificationListViewModel vm, FakeNotificationCenter center) = CreateSut();
        NotificationDto unread = Dto();
        NotificationDto read = Dto(isRead: true);
        center.ListToReturn = new NotificationListDto([unread, read], 1);

        await vm.LoadAsync();

        Assert.Equal(unread.Id, vm.Items[0].Id);
        Assert.True(vm.Items[0].IsUnread);
        Assert.False(vm.Items[1].IsUnread);
        Assert.False(string.IsNullOrWhiteSpace(vm.Items[0].Title));
        Assert.Contains("Supermercado", vm.Items[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Load_FailedRead_KeepsPreviousRows()
    {
        (NotificationListViewModel vm, FakeNotificationCenter center) = CreateSut();
        center.ListToReturn = ListOf(2);
        await vm.LoadAsync();

        center.ListToReturn = null;
        await vm.LoadAsync();

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal(2, vm.UnreadCount);
    }

    [Fact]
    public async Task Load_EmptyList_SetsIsEmpty()
    {
        (NotificationListViewModel vm, FakeNotificationCenter center) = CreateSut();
        center.ListToReturn = ListOf(1);
        await vm.LoadAsync();
        Assert.False(vm.IsEmpty);

        center.ListToReturn = ListOf(0);
        await vm.LoadAsync();

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public async Task Constructor_DoesNotLoad()
    {
        (_, FakeNotificationCenter center) = CreateSut();

        await Task.Yield();

        Assert.Equal(0, center.GetCalls);
    }

    [Fact]
    public async Task Open_MarksOnlyTheListedUnreadIds_AndDoesNotRefresh()
    {
        (NotificationListViewModel vm, FakeNotificationCenter center) = CreateSut();
        NotificationDto unreadA = Dto();
        NotificationDto unreadB = Dto();
        NotificationDto read = Dto(isRead: true);
        center.ListToReturn = new NotificationListDto([unreadA, unreadB, read], 2);
        await vm.LoadAsync();

        vm.IsOpen = true;

        IReadOnlyCollection<Guid> ids = Assert.Single(center.MarkReadCalls);
        Assert.Equal(new[] { unreadA.Id, unreadB.Id }, ids.ToArray());
        Assert.Equal(0, center.RefreshCalls);
        Assert.Equal(0, center.TryRefreshCalls);
    }

    [Fact]
    public async Task Open_WithNothingUnread_DoesNotCallMarkRead()
    {
        (NotificationListViewModel vm, FakeNotificationCenter center) = CreateSut();
        center.ListToReturn = ListOf(0, 2);
        await vm.LoadAsync();

        vm.IsOpen = true;

        Assert.Empty(center.MarkReadCalls);
    }

    [Fact]
    public async Task Close_DoesNotMarkRead()
    {
        (NotificationListViewModel vm, FakeNotificationCenter center) = CreateSut();
        center.ListToReturn = ListOf(1);
        await vm.LoadAsync();
        vm.IsOpen = true;
        center.MarkReadCalls.Clear();

        vm.IsOpen = false;

        Assert.Empty(center.MarkReadCalls);
    }

    [Fact]
    public async Task Refresh_SetsIsRefreshing_AndSecondInvocationInFlightMakesNoSecondCall()
    {
        (NotificationListViewModel vm, FakeNotificationCenter center) = CreateSut();
        center.TryRefreshGate = new TaskCompletionSource<bool>();

        Task first = vm.RefreshCommand.ExecuteAsync(null);
        Task second = vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.IsRefreshing);
        Assert.False(vm.RefreshCommand.CanExecute(null));
        Assert.Equal(1, center.TryRefreshCalls);

        center.TryRefreshGate.SetResult(true);
        await Task.WhenAll(first, second);

        Assert.False(vm.IsRefreshing);
        Assert.True(vm.RefreshCommand.CanExecute(null));
        Assert.Equal(1, center.TryRefreshCalls);
    }

    [Fact]
    public async Task Dismiss_CallsCenter_AndListReloadsViaChanged()
    {
        (NotificationListViewModel vm, FakeNotificationCenter center) = CreateSut();
        NotificationDto first = Dto();
        NotificationDto second = Dto();
        center.ListToReturn = new NotificationListDto([first, second], 2);
        await vm.LoadAsync();
        NotificationItemViewModel target = vm.Items[0];

        // The center reports the remaining rows when Changed fires after the dismiss.
        center.ListToReturn = new NotificationListDto([second], 1);
        await vm.DismissCommand.ExecuteAsync(target);
        await Task.Yield();

        Assert.Equal(new[] { first.Id }, center.DismissCalls);
        Assert.Single(vm.Items);
        Assert.Equal(second.Id, vm.Items[0].Id);
        Assert.Equal(1, vm.UnreadCount);
    }

    [Fact]
    public async Task Dismiss_NullItem_IsIgnored()
    {
        (NotificationListViewModel vm, FakeNotificationCenter center) = CreateSut();

        await vm.DismissCommand.ExecuteAsync(null);

        Assert.Empty(center.DismissCalls);
    }

    [Fact]
    public async Task Dismiss_TwoDifferentRowsConcurrently_BothReachTheCenter()
    {
        (NotificationListViewModel vm, FakeNotificationCenter center) = CreateSut();
        center.RaiseChangedOnDismiss = false;
        var a = new NotificationItemViewModel(Guid.NewGuid(), true, "A", "a");
        var b = new NotificationItemViewModel(Guid.NewGuid(), true, "B", "b");

        await Task.WhenAll(vm.DismissCommand.ExecuteAsync(a), vm.DismissCommand.ExecuteAsync(b));

        Assert.Equal(2, center.DismissCalls.Count);
        Assert.Contains(a.Id, center.DismissCalls);
        Assert.Contains(b.Id, center.DismissCalls);
    }

    [Fact]
    public async Task BellAutomationName_IncludesUnreadCount()
    {
        (NotificationListViewModel vm, FakeNotificationCenter center) = CreateSut();
        center.ListToReturn = ListOf(3);

        await vm.LoadAsync();

        Assert.Contains("3", vm.BellAutomationName, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        var center = new FakeNotificationCenter();
        var clock = new FakeClock();
        NullLogger<NotificationListViewModel> logger = NullLogger<NotificationListViewModel>.Instance;

        Assert.Throws<ArgumentNullException>(() => new NotificationListViewModel(null!, clock, logger));
        Assert.Throws<ArgumentNullException>(() => new NotificationListViewModel(center, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new NotificationListViewModel(center, clock, null!));
    }
}
