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
        GoBackCommand.NotifyCanExecuteChanged();
    }
}
