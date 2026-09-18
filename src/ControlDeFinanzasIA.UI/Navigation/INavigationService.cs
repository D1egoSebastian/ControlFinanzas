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
    /// Goes back to the previous view-model in the history stack. 
    /// </summary>
    /// <remarks>
    /// This method is a silent no-op when there is no history to go back to.
    /// </remarks>
    void GoBack();
}
