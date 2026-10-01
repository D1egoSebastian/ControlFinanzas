using System.Runtime.CompilerServices;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.Navigation;

/// <summary>
/// F-021 S7b (R-15, S-32, T11, Reconciliation #3): the sidebar sections agree with the REAL route map of
/// <see cref="NavigationService"/>, and the REAL <see cref="ShellViewModel"/> marks the right item current for all
/// ten routes (the old name-based sync missed Incomes and Recurring).
/// </summary>
public sealed class NavigationSectionsTests
{
    private static readonly string[] _orderedRoutes =
    [
        RouteNames.Dashboard,
        RouteNames.Expenses,
        RouteNames.Incomes,
        RouteNames.Recurring,
        RouteNames.Debts,
        RouteNames.Calendar,
        RouteNames.Budgets,
        RouteNames.Goals,
        RouteNames.Chat,
        RouteNames.Settings,
    ];

    public static IEnumerable<object[]> SectionPositions => Enumerable.Range(0, 10).Select(i => new object[] { i });

    private static (ShellViewModel Shell, FakeNavigationService Navigation) NewShell()
    {
        // Only the concrete type of each screen matters to the shell, so the screens are allocated without running
        // their constructors (they need databases and use cases).
        var screens = NavigationSections.All.ToDictionary(
            s => s.Route,
            s => (ViewModelBase)RuntimeHelpers.GetUninitializedObject(s.ViewModelType),
            StringComparer.Ordinal);
        var navigation = new FakeNavigationService(screens);
        var shell = new ShellViewModel(
            navigation,
            new FakeStringLocalizer<Strings>(new Dictionary<string, string> { ["SidebarCurrentSection"] = "current section" }),
            new NotificationListViewModel(
                new FakeNotificationCenter(),
                new FakeClock { Today = new DateOnly(2026, 9, 20) },
                NullLogger<NotificationListViewModel>.Instance));
        return (shell, navigation);
    }

    [Fact]
    public void EverySectionType_EqualsTheTypeTheRealNavigationServiceMapsForItsRoute()
    {
        Dictionary<string, Type> routeMap = NavigationService.BuildDefaultRoutes();

        Assert.Equal(routeMap.Count, NavigationSections.All.Count);
        foreach (NavigationSection section in NavigationSections.All)
        {
            Assert.True(routeMap.TryGetValue(section.Route, out Type? mapped), $"{section.Route} is not a route.");
            Assert.Equal(mapped, section.ViewModelType);
        }
    }

    [Fact]
    public void TheCatalog_HasTenSections_InSidebarOrder_WithDistinctSidebarLabelKeys()
    {
        Assert.Equal(_orderedRoutes, NavigationSections.All.Select(s => s.Route));
        Assert.Equal(10, NavigationSections.All.Select(s => s.LabelKey).Distinct().Count());
        Assert.All(NavigationSections.All, s => Assert.StartsWith("Sidebar", s.LabelKey, StringComparison.Ordinal));
    }

    [Fact]
    public void TheShellSidebar_FollowsTheCatalogOrder_AndStartsWithNothingCurrent()
    {
        (ShellViewModel shell, _) = NewShell();

        Assert.Equal(_orderedRoutes, shell.SidebarItems.Select(i => i.Route));
        Assert.Equal(_orderedRoutes, shell.MenuItems.Select(i => i.Route));
        Assert.DoesNotContain(shell.SidebarItems, i => i.IsCurrent);
        Assert.Equal(Enumerable.Range(1, 10), shell.SidebarItems.Select(i => i.PositionInSet));
        Assert.All(shell.SidebarItems, i => Assert.Equal(10, i.SizeOfSet));
    }

    [Theory]
    [MemberData(nameof(SectionPositions))]
    public void IsCurrent_FollowsTheCurrentViewModelType_ForEveryRoute(int position)
    {
        (ShellViewModel shell, FakeNavigationService navigation) = NewShell();
        NavigationSection section = NavigationSections.All[position];

        navigation.NavigateTo(section.Route);

        SidebarItemViewModel current = Assert.Single(shell.SidebarItems, i => i.IsCurrent);
        Assert.Equal(section.Route, current.Route);
        Assert.Equal("current section", current.ItemStatus);
        Assert.All(shell.SidebarItems.Where(i => !i.IsCurrent), i => Assert.Equal(string.Empty, i.ItemStatus));
        Assert.Equal(section.Route, shell.SelectedMenuItem?.Route);
        Assert.Equal(section.Route, shell.CurrentMenuItem?.Route);
    }

    [Fact]
    public void GoingBack_MovesTheCurrentMarker_ToThePreviousSection()
    {
        (ShellViewModel shell, FakeNavigationService navigation) = NewShell();
        navigation.NavigateTo(RouteNames.Incomes);
        navigation.NavigateTo(RouteNames.Recurring);
        navigation.CanGoBack = true;

        shell.GoBackCommand.Execute(null);

        Assert.Equal(RouteNames.Incomes, Assert.Single(shell.SidebarItems, i => i.IsCurrent).Route);
    }

    [Fact]
    public void TheDigitHints_AreOneToNineThenZero_AndComeFromTheCatalog()
    {
        (ShellViewModel shell, _) = NewShell();

        Assert.Equal(["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"], shell.SidebarItems.Select(i => i.DigitHint));
        Assert.Equal("Ctrl+1", shell.SidebarItems[0].ShortcutText);
        Assert.Equal("Ctrl+0, Ctrl+,", shell.SidebarItems[9].ShortcutText);
    }
}
