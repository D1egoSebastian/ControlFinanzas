using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.UI.Navigation;

/// <summary>One sidebar section: its route, the view-model type the route resolves to and its label resource key.</summary>
/// <param name="Route">The <see cref="RouteNames"/> route.</param>
/// <param name="ViewModelType">The view-model type <see cref="NavigationService"/> maps the route to.</param>
/// <param name="LabelKey">The <c>Strings</c> resource key of the sidebar label.</param>
public sealed record NavigationSection(string Route, Type ViewModelType, string LabelKey);

/// <summary>
/// The single source of the ordered sidebar sections (F-021, DD-7). The position drives the sidebar order and the
/// <c>Ctrl+1</c>..<c>Ctrl+9</c>, <c>Ctrl+0</c> shortcuts (Ctrl+0 is the tenth).
/// </summary>
public static class NavigationSections
{
    /// <summary>Gets the ten sections in sidebar order.</summary>
    public static IReadOnlyList<NavigationSection> All { get; } =
    [
        new(RouteNames.Dashboard, typeof(DashboardViewModel), "SidebarDashboard"),
        new(RouteNames.Expenses, typeof(ExpensesViewModel), "SidebarExpenses"),
        new(RouteNames.Incomes, typeof(IncomeViewModel), "SidebarIncomes"),
        new(RouteNames.Recurring, typeof(RecurringRulesViewModel), "SidebarRecurring"),
        new(RouteNames.Debts, typeof(DebtsViewModel), "SidebarDebts"),
        new(RouteNames.Calendar, typeof(CalendarViewModel), "SidebarCalendar"),
        new(RouteNames.Budgets, typeof(BudgetsViewModel), "SidebarBudgets"),
        new(RouteNames.Goals, typeof(GoalsViewModel), "SidebarGoals"),
        new(RouteNames.Chat, typeof(ChatViewModel), "SidebarChat"),
        new(RouteNames.Settings, typeof(SettingsViewModel), "SidebarSettings"),
    ];
}
