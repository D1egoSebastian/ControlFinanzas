using CommunityToolkit.Mvvm.ComponentModel;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ControlDeFinanzasIA.UI.Navigation;

/// <summary>
/// Default implementation of <see cref="INavigationService"/> that resolves view-models
/// from a <see cref="IServiceProvider"/> and maintains an in-memory navigation history.
/// </summary>
public sealed class NavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, Type> _routes;
    private readonly Stack<ViewModelBase> _history = new();

    private ViewModelBase? _currentViewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationService"/> class.
    /// </summary>
    /// <param name="services">The service provider used to resolve view-models.</param>
    public NavigationService(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services;
        _routes = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            { RouteNames.Dashboard, typeof(DashboardViewModel) },
            { RouteNames.Expenses, typeof(ExpensesViewModel) },
            { RouteNames.Incomes, typeof(IncomeViewModel) },
            { RouteNames.Recurring, typeof(RecurringRulesViewModel) },
            { RouteNames.Debts, typeof(DebtsViewModel) }
        };
    }

    /// <inheritdoc />
    public ViewModelBase? CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    /// <inheritdoc />
    public bool CanGoBack => _history.Count > 1;

    /// <inheritdoc />
    public event EventHandler? CurrentChanged;

    /// <inheritdoc />
    public void NavigateTo(string routeName) => NavigateCore(routeName, configure: null);

    /// <inheritdoc />
    public void NavigateTo<TViewModel>(string routeName, Action<TViewModel> configure)
        where TViewModel : ViewModelBase
    {
        ArgumentNullException.ThrowIfNull(routeName);
        ArgumentNullException.ThrowIfNull(configure);

        if (!_routes.TryGetValue(routeName, out Type? viewModelType))
        {
            throw new InvalidOperationException($"Unknown route: {routeName}");
        }

        if (!typeof(TViewModel).IsAssignableFrom(viewModelType))
        {
            throw new InvalidOperationException(
                $"Route '{routeName}' resolves to '{viewModelType.Name}', which is not assignable to requested type '{typeof(TViewModel).Name}'.");
        }

        NavigateCore(routeName, viewModel => configure((TViewModel)viewModel));
    }

    /// <summary>
    /// Resolves and activates the view-model registered for <paramref name="routeName"/>, optionally
    /// running <paramref name="configure"/> on the resolved instance before it becomes the active
    /// view-model. Shared by <see cref="NavigateTo(string)"/> and the typed overload so both produce
    /// byte-for-byte identical navigation behavior.
    /// </summary>
    /// <param name="routeName">The route name to navigate to.</param>
    /// <param name="configure">
    /// An optional delegate invoked after resolution and before activation. <see langword="null"/> for
    /// the untyped overload.
    /// </param>
    private void NavigateCore(string routeName, Action<ViewModelBase>? configure)
    {
        ArgumentNullException.ThrowIfNull(routeName);

        if (!_routes.TryGetValue(routeName, out Type? viewModelType))
        {
            throw new InvalidOperationException($"Unknown route: {routeName}");
        }

        object resolved = _services.GetRequiredService(viewModelType);
        if (resolved is not ViewModelBase viewModel)
        {
            throw new InvalidOperationException($"Route '{routeName}' resolved to an invalid type.");
        }

        configure?.Invoke(viewModel);

        _history.Push(viewModel);
        CurrentViewModel = viewModel;
        OnCurrentChanged();
    }

    /// <inheritdoc />
    public void GoBack()
    {
        if (_history.Count <= 1)
        {
            return;
        }

        _history.Pop();
        CurrentViewModel = _history.Peek();
        OnCurrentChanged();
    }

    private void OnCurrentChanged() => CurrentChanged?.Invoke(this, EventArgs.Empty);
}
