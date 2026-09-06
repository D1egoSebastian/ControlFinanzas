using System.Threading;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Localization;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for <see cref="NavigationService"/>.
/// </summary>
public sealed class NavigationServiceTests
{
    [Fact]
    public void NavigateTo_Dashboard_SetsCurrentAndPushesHistory()
    {
        // Arrange
        DashboardViewModel dashboard = CreateDashboardViewModel();
        FakeServiceProvider services = new(new Dictionary<Type, object>
        {
            { typeof(DashboardViewModel), dashboard }
        });
        NavigationService navigationService = new(services);

        // Act
        navigationService.NavigateTo(RouteNames.Dashboard);

        // Assert
        Assert.Same(dashboard, navigationService.CurrentViewModel);
        Assert.False(navigationService.CanGoBack);
    }

    [Fact]
    public void NavigateTo_UnknownRoute_ThrowsInvalidOperationException()
    {
        // Arrange
        FakeServiceProvider services = new(new Dictionary<Type, object>
        {
            { typeof(DashboardViewModel), CreateDashboardViewModel() }
        });
        NavigationService navigationService = new(services);

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => navigationService.NavigateTo("Nope"));
        Assert.Contains("Unknown route", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GoBack_WithoutHistory_IsSilentNoop()
    {
        // Arrange
        FakeServiceProvider services = new(new Dictionary<Type, object>
        {
            { typeof(DashboardViewModel), CreateDashboardViewModel() }
        });
        NavigationService navigationService = new(services);

        // Act
        navigationService.GoBack();

        // Assert
        Assert.Null(navigationService.CurrentViewModel);
    }

    [Fact]
    public void CurrentChanged_FiresOnEveryNavigation()
    {
        // Arrange
        FakeServiceProvider services = new(new Dictionary<Type, object>
        {
            { typeof(DashboardViewModel), CreateDashboardViewModel() }
        });
        NavigationService navigationService = new(services);
        int count = 0;
        navigationService.CurrentChanged += (_, _) => count++;

        // Act
        navigationService.NavigateTo(RouteNames.Dashboard);
        navigationService.NavigateTo(RouteNames.Dashboard);

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void NavigateTo_Twice_ThenGoBack_RestoresPreviousCurrent()
    {
        // Arrange
        DashboardViewModel first = CreateDashboardViewModel();
        DashboardViewModel second = CreateDashboardViewModel();
        int callCount = 0;
        FakeServiceProvider services = new(
            new Dictionary<Type, object>(),
            type => type == typeof(DashboardViewModel)
                ? (Interlocked.Increment(ref callCount) == 1 ? first : second)
                : null);
        NavigationService navigationService = new(services);

        // Act
        navigationService.NavigateTo(RouteNames.Dashboard);
        navigationService.NavigateTo(RouteNames.Dashboard);
        navigationService.GoBack();

        // Assert
        Assert.Same(first, navigationService.CurrentViewModel);
        Assert.False(navigationService.CanGoBack);
    }

    private static DashboardViewModel CreateDashboardViewModel()
        => new(new FakeStringLocalizer<Strings>());

    private sealed class FakeServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services;
        private readonly Func<Type, object?>? _factory;

        public FakeServiceProvider(Dictionary<Type, object> services, Func<Type, object?>? factory = null)
        {
            _services = services;
            _factory = factory;
        }

        public object? GetService(Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out object? instance))
            {
                return instance;
            }

            return _factory?.Invoke(serviceType);
        }
    }
}
