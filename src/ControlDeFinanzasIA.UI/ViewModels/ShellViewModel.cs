using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using Microsoft.Extensions.Localization;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// View-model for the application shell. Manages the navigation sidebar,
/// the current content view-model, and the back navigation command.
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly INavigationService _navService;
    private readonly IStringLocalizer<Strings> _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellViewModel"/> class.
    /// </summary>
    /// <param name="navService">The navigation service used to drive content changes.</param>
    /// <param name="localizer">The localizer for UI strings.</param>
    /// <param name="notifications">The route-agnostic notification list shown by the header bell (F-017).</param>
    /// <param name="errorOverlay">The in-window unexpected-error overlay (F-021); optional so shells built without one keep working.</param>
    public ShellViewModel(
        INavigationService navService,
        IStringLocalizer<Strings> localizer,
        NotificationListViewModel notifications,
        ErrorOverlayViewModel? errorOverlay = null)
    {
        ArgumentNullException.ThrowIfNull(navService);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(notifications);

        _navService = navService;
        _localizer = localizer;
        Notifications = notifications;
        ErrorOverlay = errorOverlay;
        if (errorOverlay is not null)
        {
            errorOverlay.PropertyChanged += OnErrorOverlayPropertyChanged;
        }

        _navService.CurrentChanged += OnNavigationCurrentChanged;

        MenuItems =
        [
            new RouteMenuItem(RouteNames.Dashboard, _localizer["SidebarDashboard"]),
            new RouteMenuItem(RouteNames.Expenses, _localizer["SidebarExpenses"]),
            new RouteMenuItem(RouteNames.Incomes, _localizer["SidebarIncomes"])
            ,new RouteMenuItem(RouteNames.Recurring, _localizer["SidebarRecurring"])
            ,new RouteMenuItem(RouteNames.Debts, _localizer["SidebarDebts"])
            ,new RouteMenuItem(RouteNames.Calendar, _localizer["SidebarCalendar"])
            ,new RouteMenuItem(RouteNames.Budgets, _localizer["SidebarBudgets"])
            ,new RouteMenuItem(RouteNames.Goals, _localizer["SidebarGoals"])
            ,new RouteMenuItem(RouteNames.Chat, _localizer["SidebarChat"])
            ,new RouteMenuItem(RouteNames.Settings, _localizer["SidebarSettings"])
        ];
    }

    /// <summary>
    /// Gets the notification list bound by the header bell, badge and flyout (F-017, R-3).
    /// </summary>
    public NotificationListViewModel Notifications { get; }

    /// <summary>
    /// Gets the unexpected-error overlay hosted over the shell (F-021, DD-4), or <see langword="null"/> when the
    /// shell was built without one.
    /// </summary>
    public ErrorOverlayViewModel? ErrorOverlay { get; }

    /// <summary>
    /// Gets a value indicating whether a modal overlay is open. While it is, the shell content is disabled: it
    /// leaves the tab order, ignores pointer input and cannot fire an <c>IsDefault</c> button behind the overlay.
    /// </summary>
    public bool IsModalOpen => ErrorOverlay?.IsOpen == true;

    private void OnErrorOverlayPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ErrorOverlayViewModel.IsOpen))
        {
            return;
        }

        if (IsModalOpen)
        {
            // The popup is a separate TopLevel: it must not stay open on top of, or behind, the modal.
            Notifications.IsOpen = false;
        }

        OnPropertyChanged(nameof(IsModalOpen));
    }

    /// <summary>
    /// Gets or sets the currently displayed content view-model.
    /// </summary>
    [ObservableProperty]
    private ViewModelBase? _current;

    /// <summary>
    /// Gets or sets the currently selected sidebar menu item. Kept in
    /// sync with <see cref="Current"/> via
    /// <see cref="OnNavigationCurrentChanged"/> when navigation happens
    /// outside the sidebar (GoBack, programmatic NavigateTo). Drives
    /// the sidebar's visual highlight.
    /// </summary>
    [ObservableProperty]
    private RouteMenuItem? _selectedMenuItem;

    /// <summary>
    /// Gets the list of sidebar menu items.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<RouteMenuItem> _menuItems;

    /// <summary>
    /// Gets a value indicating whether the back command can execute.
    /// </summary>
    public bool CanGoBack => _navService.CanGoBack;

    /// <summary>
    /// Navigates to the route associated with the sidebar menu item
    /// passed as the command parameter.
    /// </summary>
    /// <param name="item">Menu item whose <see cref="RouteMenuItem.Route"/>
    /// identifies the destination. Null is treated as no-op.</param>
    [RelayCommand]
    private void NavigateTo(RouteMenuItem? item)
    {
        if (item is null)
        {
            return;
        }

        _navService.NavigateTo(item.Route);
    }

    /// <summary>
    /// Goes back to the previous view-model in the navigation history.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack() => _navService.GoBack();

    private void OnNavigationCurrentChanged(object? sender, EventArgs e)
    {
        Current = _navService.CurrentViewModel;

        // Keep the sidebar selection in sync when navigation happens
        // through other paths (e.g. GoBack, programmatic NavigateTo).
        // MenuItem.Route is the short name (e.g. "Expenses"); the VM
        // type is e.g. "ExpensesViewModel". We strip the "ViewModel" suffix
        // to match them.
        string? currentTypeName = _navService.CurrentViewModel?.GetType().Name;
        string? currentRoute = currentTypeName?.Replace("ViewModel", string.Empty);
        RouteMenuItem? matching = MenuItems
            .FirstOrDefault(m => m.Route == currentRoute);
        if (matching is not null)
        {
            SelectedMenuItem = matching;
        }

        GoBackCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Gets the currently selected sidebar menu item. Exposed as a
    /// read-only property (computed from <see cref="CurrentViewModel"/>)
    /// so the sidebar's visual highlight can be data-bound even when
    /// navigation happens outside the sidebar.
    /// </summary>
    public RouteMenuItem? CurrentMenuItem
    {
        get
        {
            string? currentTypeName = _navService.CurrentViewModel?.GetType().Name;
            string? currentRoute = currentTypeName?.Replace("ViewModel", string.Empty);
            return MenuItems.FirstOrDefault(m => m.Route == currentRoute);
        }
    }
}
