using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="INavigationService"/> used in unit tests.
/// </summary>
/// <remarks>
/// Records navigation calls, exposes configurable <see cref="CurrentViewModel"/>
/// and <see cref="CanGoBack"/> values, and lets tests raise
/// <see cref="CurrentChanged"/> manually.
/// </remarks>
public sealed class FakeNavigationService : INavigationService
{
    private readonly Dictionary<string, ViewModelBase> _routes;
    private readonly Stack<ViewModelBase> _history = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeNavigationService"/> class.
    /// </summary>
    /// <param name="routes">
    /// Optional route map used to resolve <see cref="CurrentViewModel"/>
    /// when <see cref="NavigateTo(string)"/> is called.
    /// </param>
    public FakeNavigationService(Dictionary<string, ViewModelBase>? routes = null)
    {
        _routes = routes ?? new Dictionary<string, ViewModelBase>(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public ViewModelBase? CurrentViewModel { get; set; }

    /// <inheritdoc />
    public bool CanGoBack { get; set; }

    /// <inheritdoc />
    public event EventHandler? CurrentChanged;

    /// <summary>
    /// Gets the list of routes passed to <see cref="NavigateTo(string)"/>.
    /// </summary>
    public List<string> NavigateCalls { get; } = new();

    /// <summary>
    /// Gets a value indicating whether <see cref="GoBack"/> was called.
    /// </summary>
    public bool GoBackCalled { get; private set; }

    /// <inheritdoc />
    public void NavigateTo(string routeName)
    {
        ArgumentNullException.ThrowIfNull(routeName);

        NavigateCalls.Add(routeName);

        if (!_routes.TryGetValue(routeName, out ViewModelBase? viewModel))
        {
            throw new InvalidOperationException($"Unknown route: {routeName}");
        }

        CurrentViewModel = viewModel;
        _history.Push(viewModel);
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the list of (routeName, configure) pairs passed to
    /// <see cref="NavigateTo{TViewModel}(string, Action{TViewModel})"/>.
    /// </summary>
    public List<string> NavigateToTypedCalls { get; } = new();

    /// <inheritdoc />
    public void NavigateTo<TViewModel>(string routeName, Action<TViewModel> configure)
        where TViewModel : ViewModelBase
    {
        ArgumentNullException.ThrowIfNull(routeName);
        ArgumentNullException.ThrowIfNull(configure);

        NavigateToTypedCalls.Add(routeName);

        if (!_routes.TryGetValue(routeName, out ViewModelBase? viewModel))
        {
            throw new InvalidOperationException($"Unknown route: {routeName}");
        }

        if (viewModel is not TViewModel typed)
        {
            throw new InvalidOperationException(
                $"Route '{routeName}' resolves to '{viewModel.GetType().Name}', which is not assignable to requested type '{typeof(TViewModel).Name}'.");
        }

        configure(typed);

        CurrentViewModel = viewModel;
        _history.Push(viewModel);
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void GoBack()
    {
        GoBackCalled = true;

        if (!CanGoBack || _history.Count <= 1)
        {
            return;
        }

        _history.Pop();
        CurrentViewModel = _history.Peek();
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises <see cref="CurrentChanged"/> so tests can simulate a navigation
    /// event from the real service.
    /// </summary>
    public void RaiseCurrentChanged() => CurrentChanged?.Invoke(this, EventArgs.Empty);
}
