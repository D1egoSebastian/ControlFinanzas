using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Localization;
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
            { RouteNames.Dashboard, new DashboardViewModel(CreateLocalizer()) }
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
        DashboardViewModel dashboard = new(CreateLocalizer());

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
}
