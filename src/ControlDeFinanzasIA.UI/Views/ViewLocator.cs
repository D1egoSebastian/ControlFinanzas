using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.UI.Views;

/// <summary>
/// Resolves navigation view-models to their concrete Avalonia views.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    /// <inheritdoc />
    public Control? Build(object? data)
        => data switch
        {
            DashboardViewModel viewModel => new DashboardView
            {
                DataContext = viewModel,
            },
            ExpensesViewModel viewModel => new ExpensesView
            {
                DataContext = viewModel,
            },
            IncomeViewModel viewModel => new IncomeView
            {
                DataContext = viewModel,
            },
            RecurringRulesViewModel viewModel => new RecurringRulesView
            {
                DataContext = viewModel,
            },
            DebtsViewModel viewModel => new DebtsView
            {
                DataContext = viewModel,
            },
            CalendarViewModel viewModel => new CalendarView
            {
                DataContext = viewModel,
            },
            _ => null,
        };

    /// <inheritdoc />
    public bool Match(object? data)
        => data is DashboardViewModel
            or ExpensesViewModel
            or IncomeViewModel
            or RecurringRulesViewModel
            or DebtsViewModel
            or CalendarViewModel;
}
