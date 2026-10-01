using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.UI.Input;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using Microsoft.Extensions.Localization;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// View-model for the application shell. Manages the navigation sidebar,
/// the current content view-model, and the back navigation command.
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase, IShortcutCommandSource
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

        // F-021 (DD-7): the order and the labels come from NavigationSections, the single source of the sidebar.
        MenuItems = [.. NavigationSections.All.Select(section => new RouteMenuItem(section.Route, _localizer[section.LabelKey]))];
        SidebarItems = BuildSidebarItems();
    }

    /// <summary>
    /// Gets the flat sidebar entries in <see cref="NavigationSections"/> order; exactly one is current when the
    /// screen on display is a section.
    /// </summary>
    public IReadOnlyList<SidebarItemViewModel> SidebarItems { get; }

    private List<SidebarItemViewModel> BuildSidebarItems()
    {
        bool isMacOS = OperatingSystem.IsMacOS();
        string currentStatus = _localizer["SidebarCurrentSection"];
        IReadOnlyList<NavigationSection> sections = NavigationSections.All;
        var items = new List<SidebarItemViewModel>(sections.Count);
        for (int i = 0; i < sections.Count; i++)
        {
            NavigationSection section = sections[i];

            // Ctrl+1..Ctrl+9 open the first nine sections and Ctrl+0 the tenth (catalog SectionIndex 1..9, 0).
            int digit = (i + 1) % 10;
            KeyboardShortcut? sectionShortcut = i < 10
                ? KeyboardShortcutCatalog.All.SingleOrDefault(s => s.Action == ShortcutAction.GoToSection && s.SectionIndex == digit)
                : null;
            var gestures = new List<string>(2);
            if (sectionShortcut is not null)
            {
                gestures.Add(KeyboardShortcutCatalog.Format(sectionShortcut, isMacOS));
            }

            if (section.Route == RouteNames.Settings)
            {
                gestures.Add(KeyboardShortcutCatalog.Format(
                    KeyboardShortcutCatalog.All.Single(s => s.Action == ShortcutAction.OpenSettings),
                    isMacOS));
            }

            items.Add(new SidebarItemViewModel(
                section,
                _localizer[section.LabelKey],
                sectionShortcut is null ? string.Empty : digit.ToString(CultureInfo.InvariantCulture),
                string.Join(", ", gestures),
                currentStatus,
                i + 1,
                sections.Count));
        }

        return items;
    }

    /// <summary>
    /// Opens the section of a sidebar entry (mouse, Enter or Space on the focused entry).
    /// </summary>
    /// <param name="item">The entry; null is a no-op.</param>
    [RelayCommand]
    private void OpenSidebarItem(SidebarItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _navService.NavigateTo(item.Route);
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

    // F-021 (DD-6): window shortcut commands. The gate lives in each command body, never in CanExecute, so the
    // key binding stays executable and a gated press is a silent no-op instead of a disabled-command surprise.

    /// <summary>
    /// A modal layer (the error overlay) is open, or the current screen is in the middle of an import commit or a
    /// restore: navigation and new-entry shortcuts must not act.
    /// </summary>
    private bool IsShortcutGated => IsModalOpen || IsCurrentBusy;

    private bool IsCurrentBusy => _navService.CurrentViewModel is IBusyAware { IsBusy: true };

    [RelayCommand]
    private void NewExpense()
    {
        if (IsShortcutGated)
        {
            return;
        }

        if (_navService.CurrentViewModel is ExpensesViewModel expenses)
        {
            expenses.BeginNewEntry();
            return;
        }

        _navService.NavigateTo<ExpensesViewModel>(RouteNames.Expenses, vm => vm.BeginNewEntry());
    }

    [RelayCommand]
    private void NewIncome()
    {
        if (IsShortcutGated)
        {
            return;
        }

        if (_navService.CurrentViewModel is IncomeViewModel income)
        {
            income.BeginNewEntry();
            return;
        }

        _navService.NavigateTo<IncomeViewModel>(RouteNames.Incomes, vm => vm.BeginNewEntry());
    }

    /// <summary>
    /// Opens the section at a digit position: <c>1</c>..<c>9</c> are the first nine, <c>0</c> the tenth. Any other
    /// digit, and the section already on screen, do nothing (no reload, no history entry).
    /// </summary>
    /// <param name="digit">The digit of the pressed <c>Ctrl+digit</c>.</param>
    [RelayCommand]
    private void GoToSection(int digit)
    {
        if (IsShortcutGated || digit is < 0 or > 9)
        {
            return;
        }

        int position = digit == 0 ? 9 : digit - 1;
        if (position >= NavigationSections.All.Count)
        {
            return;
        }

        OpenSection(NavigationSections.All[position]);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (IsShortcutGated)
        {
            return;
        }

        OpenSection(NavigationSections.All.Single(s => s.Route == RouteNames.Settings));
    }

    [RelayCommand]
    private void ToggleNotifications()
    {
        if (IsModalOpen)
        {
            return;
        }

        Notifications.IsOpen = !Notifications.IsOpen;
    }

    /// <summary>
    /// Closes the topmost layer: the error overlay always (it beats a busy operation, Reconciliation #6), otherwise
    /// the notifications popup, except while an operation that must not be interrupted is running.
    /// </summary>
    [RelayCommand]
    private void Escape()
    {
        if (ErrorOverlay is { IsOpen: true } overlay)
        {
            overlay.Close();
            return;
        }

        if (IsCurrentBusy)
        {
            return;
        }

        if (Notifications.IsOpen)
        {
            Notifications.IsOpen = false;
        }
    }

    private void OpenSection(NavigationSection section)
    {
        if (_navService.CurrentViewModel?.GetType() == section.ViewModelType)
        {
            return;
        }

        _navService.NavigateTo(section.Route);
        ContentFocusRequest = new FocusRequestToken();
    }

    /// <summary>Gets the focus request raised after a shortcut navigates to another section.</summary>
    [ObservableProperty]
    private FocusRequestToken? _contentFocusRequest;

    /// <inheritdoc />
    public ICommand Resolve(ShortcutAction action) => action switch
    {
        ShortcutAction.NewExpense => NewExpenseCommand,
        ShortcutAction.NewIncome => NewIncomeCommand,
        ShortcutAction.GoToSection => GoToSectionCommand,
        ShortcutAction.OpenSettings => OpenSettingsCommand,
        ShortcutAction.ToggleNotifications => ToggleNotificationsCommand,
        ShortcutAction.Escape => EscapeCommand,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    private void OnNavigationCurrentChanged(object? sender, EventArgs e)
    {
        Current = _navService.CurrentViewModel;

        // F-021 (DD-7): compare the VIEW-MODEL TYPE with the section types. The old name-based match never found
        // Incomes ("IncomeViewModel" vs "Incomes") or Recurring ("RecurringRulesViewModel" vs "Recurring").
        Type? currentType = _navService.CurrentViewModel?.GetType();
        foreach (SidebarItemViewModel item in SidebarItems)
        {
            item.IsCurrent = currentType is not null && item.Section.ViewModelType == currentType;
        }

        RouteMenuItem? matching = CurrentMenuItem;
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
            Type? currentType = _navService.CurrentViewModel?.GetType();
            string? currentRoute = NavigationSections.All.FirstOrDefault(s => s.ViewModelType == currentType)?.Route;
            return MenuItems.FirstOrDefault(m => m.Route == currentRoute);
        }
    }
}
