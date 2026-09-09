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
    public ShellViewModel(INavigationService navService, IStringLocalizer<Strings> localizer)
    {
        ArgumentNullException.ThrowIfNull(navService);
        ArgumentNullException.ThrowIfNull(localizer);

        _navService = navService;
        _localizer = localizer;

        _navService.CurrentChanged += OnNavigationCurrentChanged;

        MenuItems =
        [
            new RouteMenuItem(RouteNames.Dashboard, _localizer["SidebarDashboard"]),
            new RouteMenuItem(RouteNames.Expenses, _localizer["SidebarExpenses"]),
            new RouteMenuItem(RouteNames.Incomes, _localizer["SidebarIncomes"])
        ];
    }

    /// <summary>
    /// Gets or sets the currently displayed content view-model.
    /// </summary>
    [ObservableProperty]
    private ViewModelBase? _current;

    /// <summary>
    /// Gets or sets the currently selected sidebar menu item. Setting this
    /// property triggers navigation to the corresponding route, which in
    /// turn updates <see cref="Current"/>. The setter ignores null
    /// values (used during initial composition) and re-selections of
    /// the same item (to avoid navigation loops).
    /// </summary>
    [ObservableProperty]
    private RouteMenuItem? _selectedMenuItem;

    partial void OnSelectedMenuItemChanged(RouteMenuItem? value)
    {
        if (value is null)
        {
            return;
        }

        // Guard against re-navigation: if the current VM already maps to
        // this route (e.g. when OnNavigationCurrentChanged syncs the
        // SelectedItem back), calling NavigateTo again would push the
        // same route onto the history stack.
        string currentRoute = _navService.CurrentViewModel?.GetType().Name
            .Replace("ViewModel", string.Empty) ?? string.Empty;
        if (string.Equals(currentRoute, value.Route, StringComparison.Ordinal))
        {
            return;
        }

        _navService.NavigateTo(value.Route);
    }

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
    /// Navigates to the route provided as the command parameter.
    /// </summary>
    /// <param name="route">The route name to navigate to.</param>
    [RelayCommand]
    private void NavigateTo(string? route) => _navService.NavigateTo(route ?? string.Empty);

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
}
