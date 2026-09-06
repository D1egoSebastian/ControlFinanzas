namespace ControlDeFinanzasIA.UI.Navigation;

/// <summary>
/// Defines the named routes used by the application's content-area navigation.
/// </summary>
/// <remarks>
/// New features add their route constants here; the actual view-model mapping
/// is registered in <see cref="NavigationService"/>.
/// </remarks>
public static class RouteNames
{
    /// <summary>
    /// The dashboard route shown on startup.
    /// </summary>
    public const string Dashboard = "Dashboard";

    /// <summary>
    /// The expenses route (consumed by F-007).
    /// </summary>
    public const string Expenses = "Expenses";

    /// <summary>
    /// The incomes route (consumed by F-008 of Phase 4).
    /// </summary>
    public const string Incomes = "Incomes";
}
