namespace ControlDeFinanzasIA.UI.Navigation;

/// <summary>
/// Opt-in lifecycle hook for view-models that need an explicit signal when the user
/// navigates away from their route.
/// </summary>
/// <remarks>
/// F-015 (D-7): <see cref="NavigationService.GoBack"/> reactivates the same view-model
/// instance already on the history stack instead of resolving a new one, so a transient DI
/// registration alone cannot tell a view-model "the user left this screen". A view-model
/// implements this interface to receive that signal explicitly. The hook is opt-in: any
/// view-model that does not implement it is completely unaffected by navigation.
/// </remarks>
public interface INavigationAware
{
    /// <summary>
    /// Called after this view-model becomes the navigation service's active view-model.
    /// </summary>
    void OnNavigatedTo();

    /// <summary>
    /// Called before another view-model replaces this one as the navigation service's
    /// active view-model.
    /// </summary>
    void OnNavigatedFrom();
}
