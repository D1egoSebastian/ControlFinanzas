using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.UI.Navigation;

/// <summary>
/// Contract for content-area navigation backed by an in-memory history stack.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Gets the currently active view-model, or <see langword="null"/> before the first navigation.
    /// </summary>
    ViewModelBase? CurrentViewModel { get; }

    /// <summary>
    /// Gets a value indicating whether there is a previous view-model to go back to.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Occurs whenever <see cref="CurrentViewModel"/> changes.
    /// </summary>
    event EventHandler? CurrentChanged;

    /// <summary>
    /// Navigates to the view-model registered for <paramref name="routeName"/>.
    /// </summary>
    /// <param name="routeName">The route name to navigate to.</param>
    /// <exception cref="InvalidOperationException">The route is not registered.</exception>
    void NavigateTo(string routeName);

    /// <summary>
    /// Navigates to the view-model registered for <paramref name="routeName"/>, configuring it
    /// via <paramref name="configure"/> before it becomes the active view-model.
    /// </summary>
    /// <remarks>
    /// <paramref name="configure"/> runs after the view-model is resolved but before
    /// <see cref="CurrentViewModel"/> changes and before <see cref="CurrentChanged"/> fires, so the
    /// target view-model's constructor-driven load has already started (it cannot be pre-empted) and
    /// <paramref name="configure"/> supersedes it rather than preceding it.
    /// </remarks>
    /// <typeparam name="TViewModel">The expected view-model type registered for the route.</typeparam>
    /// <param name="routeName">The route name to navigate to.</param>
    /// <param name="configure">A delegate that configures the resolved view-model before activation.</param>
    /// <exception cref="InvalidOperationException">
    /// The route is not registered, or is registered for a view-model type that is not assignable to
    /// <typeparamref name="TViewModel"/>.
    /// </exception>
    void NavigateTo<TViewModel>(string routeName, Action<TViewModel> configure)
        where TViewModel : ViewModelBase;

    /// <summary>
    /// Goes back to the previous view-model in the history stack. 
    /// </summary>
    /// <remarks>
    /// This method is a silent no-op when there is no history to go back to.
    /// </remarks>
    void GoBack();
}
