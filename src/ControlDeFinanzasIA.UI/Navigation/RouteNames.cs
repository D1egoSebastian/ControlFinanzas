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

    public const string Recurring = "Recurring";

    /// <summary>
    /// The debts route (consumed by F-001 of Phase 7).
    /// </summary>
    public const string Debts = "Debts";

    /// <summary>
    /// The expense calendar route (consumed by F-009 of Phase 9).
    /// </summary>
    public const string Calendar = "Calendar";

    /// <summary>
    /// The monthly budgets management route (consumed by F-010 of Phase 10).
    /// The literal value matters: <see cref="UI.ViewModels.ShellViewModel"/>
    /// derives the sidebar highlight by stripping "ViewModel" from the
    /// current view-model's type name, so this must stay <c>"Budgets"</c>.
    /// </summary>
    public const string Budgets = "Budgets";
}
