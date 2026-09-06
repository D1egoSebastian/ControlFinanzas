namespace ControlDeFinanzasIA.UI.Navigation;

/// <summary>
/// Represents a single entry displayed in the shell's navigation sidebar.
/// </summary>
/// <param name="Route">The route name to navigate to when the item is selected.</param>
/// <param name="DisplayName">The localized text shown to the user.</param>
public sealed record RouteMenuItem(string Route, string DisplayName);
