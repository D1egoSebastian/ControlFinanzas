using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthlyEvolution;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for <see cref="ShellViewModel"/>.
/// </summary>
public sealed class ShellViewModelTests
{
    [Fact]
    public void NavigateToCommand_DelegatesToNavigationService()
    {
        // Arrange
        FakeNavigationService fakeNav = new(new Dictionary<string, ViewModelBase>
        {
            { RouteNames.Dashboard, CreateDashboardViewModel() }
        });
        ShellViewModel shell = new(fakeNav, CreateLocalizer());
        RouteMenuItem dashboardItem = new(RouteNames.Dashboard, "Panel");

        // Act
        shell.NavigateToCommand.Execute(dashboardItem);

        // Assert
        Assert.Contains(RouteNames.Dashboard, fakeNav.NavigateCalls);
    }

    [Fact]
    public void GoBackCommand_CanExecute_BoundToCanGoBack()
    {
        // Arrange
        FakeNavigationService fakeNav = new();
        ShellViewModel shell = new(fakeNav, CreateLocalizer());

        // Act & Assert
        fakeNav.CanGoBack = false;
        Assert.False(shell.GoBackCommand.CanExecute(null));

        fakeNav.CanGoBack = true;
        Assert.True(shell.GoBackCommand.CanExecute(null));
    }

    [Fact]
    public void Current_SyncedWhenNavigationServiceRaisesCurrentChanged()
    {
        // Arrange
        FakeNavigationService fakeNav = new();
        ShellViewModel shell = new(fakeNav, CreateLocalizer());
        DashboardViewModel dashboard = CreateDashboardViewModel();

        // Act
        fakeNav.CurrentViewModel = dashboard;
        fakeNav.RaiseCurrentChanged();

        // Assert
        Assert.Same(dashboard, shell.Current);
    }

    private static IStringLocalizer<Strings> CreateLocalizer()
        => new FakeStringLocalizer<Strings>(new Dictionary<string, string>
        {
            { "SidebarDashboard", "Panel" },
            { "SidebarExpenses", "Gastos" }
        });

    private static DashboardViewModel CreateDashboardViewModel()
    {
        FakeExpenseRepository expenseRepo = new();
        FakeIncomeRepository incomeRepo = new();
        FakeCurrencyConverter converter = new();
        FakeClock clock = new() { Today = new DateOnly(2026, 3, 15) };

        return new DashboardViewModel(
            new GetMonthSummaryQuery(expenseRepo, incomeRepo, converter),
            new GetCategoryBreakdownQuery(expenseRepo, converter),
            new GetMonthlyEvolutionQuery(expenseRepo, incomeRepo, converter),
            new GetUpcomingPaymentsQuery(
                new FakeDebtPaymentRepository(), new FakeDebtRepository(), new FakeRecurringRuleRepository(), clock),
            new GetBudgetVsActualQuery(
                new FakeBudgetRepository(), expenseRepo, new FakeCategoryRepository(), converter),
            clock,
            NullLogger<DashboardViewModel>.Instance);
    }
}
