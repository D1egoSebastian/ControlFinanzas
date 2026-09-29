using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;
using ControlDeFinanzasIA.UI.Formatting;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.Services;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Route-agnostic notification list shown from the shell (F-017, R-3, D-10).
/// </summary>
/// <remarks>
/// Singleton. It has no constructor load: the startup refresh raises
/// <see cref="INotificationCenter.Changed"/>, and that event performs the first (and every
/// later) load. Opening the list marks only the rows currently listed as read and never
/// triggers detection (R-8, R-14). Clicking a row does nothing (R-16).
/// </remarks>
public sealed partial class NotificationListViewModel : ViewModelBase
{
    private const int _badgeCap = 9;

    private readonly INotificationCenter _center;
    private readonly IClock _clock;
    private readonly ILogger<NotificationListViewModel> _logger;
    private readonly HashSet<Guid> _dismissing = new();
    private int _loadVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationListViewModel"/> class.
    /// </summary>
    /// <param name="center">Notification center used for every read and write.</param>
    /// <param name="clock">Clock supplying the current day for relative dates.</param>
    /// <param name="logger">Logger for swallowed failures.</param>
    public NotificationListViewModel(
        INotificationCenter center,
        IClock clock,
        ILogger<NotificationListViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(center);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _center = center;
        _clock = clock;
        _logger = logger;

        _center.Changed += (_, _) => _ = LoadAsync();
    }

    /// <summary>Gets the listed notifications, unread first.</summary>
    public ObservableCollection<NotificationItemViewModel> Items { get; } = new();

    /// <summary>Gets or sets the number of unread notifications.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    [NotifyPropertyChangedFor(nameof(HasUnread))]
    [NotifyPropertyChangedFor(nameof(BellAutomationName))]
    private int _unreadCount;

    /// <summary>Gets or sets a value indicating whether the flyout is open.</summary>
    [ObservableProperty]
    private bool _isOpen;

    /// <summary>Gets or sets a value indicating whether a manual refresh is in flight.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isRefreshing;

    /// <summary>Gets or sets a value indicating whether there is nothing to list.</summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>Gets the badge text: empty at 0, the count up to 9, otherwise "9+".</summary>
    public string BadgeText => UnreadCount <= 0
        ? string.Empty
        : UnreadCount > _badgeCap
            ? _badgeCap.ToString(CultureInfo.InvariantCulture) + "+"
            : UnreadCount.ToString(CultureInfo.InvariantCulture);

    /// <summary>Gets a value indicating whether the badge should be visible.</summary>
    public bool HasUnread => UnreadCount > 0;

    /// <summary>Gets the accessible name of the bell, including the unread count.</summary>
    public string BellAutomationName => string.Format(
        CultureInfo.CurrentCulture,
        Strings.NotificationsBellAutomationFormat,
        UnreadCount);

    partial void OnIsOpenChanged(bool value)
    {
        if (value)
        {
            _ = MarkListedAsReadAsync();
        }
    }

    /// <summary>
    /// Re-reads the list from the center. A failed read keeps the current rows.
    /// </summary>
    /// <returns>A task that completes when the list has been updated; never faults.</returns>
    public async Task LoadAsync()
    {
        int version = ++_loadVersion;

        try
        {
            NotificationListDto? list = await _center.GetAsync();

            // A newer load started while this one was awaiting: its result wins.
            if (list is null || version != _loadVersion)
            {
                return;
            }

            DateOnly today = _clock.Today;
            CultureInfo culture = CultureInfo.CurrentCulture;

            Items.Clear();
            foreach (NotificationDto dto in list.Items)
            {
                (string title, string body) = NotificationTextFormatter.Format(dto, today, culture);
                Items.Add(new NotificationItemViewModel(dto.Id, !dto.IsRead, title, body));
            }

            UnreadCount = list.UnreadCount;
            IsEmpty = Items.Count == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load the notification list.");
        }
    }

    private async Task MarkListedAsReadAsync()
    {
        try
        {
            Guid[] unreadIds = Items.Where(i => i.IsUnread).Select(i => i.Id).ToArray();
            if (unreadIds.Length == 0)
            {
                return;
            }

            await _center.MarkReadAsync(unreadIds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to mark notifications as read.");
        }
    }

    /// <summary>
    /// Re-runs detection unless a run is already in flight (R-14).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        try
        {
            await _center.TryRefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to refresh notifications.");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private bool CanRefresh() => !IsRefreshing;

    /// <summary>
    /// Dismisses one notification; the list reloads through <see cref="INotificationCenter.Changed"/>.
    /// </summary>
    /// <param name="item">The row to dismiss.</param>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task DismissAsync(NotificationItemViewModel? item)
    {
        if (item is null || !_dismissing.Add(item.Id))
        {
            return;
        }

        try
        {
            await _center.DismissAsync(item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to dismiss notification {NotificationId}.", item.Id);
        }
        finally
        {
            _dismissing.Remove(item.Id);
        }
    }
}
