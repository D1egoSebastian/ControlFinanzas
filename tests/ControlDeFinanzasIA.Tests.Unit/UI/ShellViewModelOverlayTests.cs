using System.ComponentModel;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>F-021 S4 (R-10, DD-4): the optional error overlay of the REAL <see cref="ShellViewModel"/>.</summary>
public sealed class ShellViewModelOverlayTests
{
    private static NotificationListViewModel Notifications() =>
        new(
            new FakeNotificationCenter(),
            new FakeClock { Today = new DateOnly(2026, 9, 20) },
            NullLogger<NotificationListViewModel>.Instance);

    private static ShellViewModel Shell(ErrorOverlayViewModel? overlay, NotificationListViewModel? notifications = null) =>
        new(
            new FakeNavigationService(),
            new FakeStringLocalizer<Strings>(),
            notifications ?? Notifications(),
            overlay);

    [Fact]
    public void WithoutAnOverlay_TheShellIsNeverModal()
    {
        ShellViewModel shell = Shell(null);

        Assert.Null(shell.ErrorOverlay);
        Assert.False(shell.IsModalOpen);
    }

    [Fact]
    public void IsModalOpen_FollowsTheOverlay_AndRaisesPropertyChangedBothWays()
    {
        var overlay = new ErrorOverlayViewModel(new FakeClipboardService(), "1.0.0");
        ShellViewModel shell = Shell(overlay);
        var changed = new List<string?>();
        ((INotifyPropertyChanged)shell).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        overlay.Present(Guid.NewGuid(), DateTime.UtcNow);
        Assert.True(shell.IsModalOpen);
        Assert.Same(overlay, shell.ErrorOverlay);

        overlay.Close();
        Assert.False(shell.IsModalOpen);
        Assert.Equal(2, changed.Count(n => n == nameof(ShellViewModel.IsModalOpen)));
    }

    [Fact]
    public void OpeningTheOverlay_ClosesTheNotificationsPopup_ButClosingItDoesNotReopenAnything()
    {
        NotificationListViewModel notifications = Notifications();
        var overlay = new ErrorOverlayViewModel(new FakeClipboardService(), "1.0.0");
        _ = Shell(overlay, notifications);
        notifications.IsOpen = true;

        overlay.Present(Guid.NewGuid(), DateTime.UtcNow);
        Assert.False(notifications.IsOpen);

        overlay.Close();
        Assert.False(notifications.IsOpen);
    }
}
