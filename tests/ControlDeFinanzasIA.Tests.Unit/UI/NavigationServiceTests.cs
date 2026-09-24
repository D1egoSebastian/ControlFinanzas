using System.Threading;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthlyEvolution;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public void NavigateToTyped_ConfigureRunsBeforeCurrentViewModelChanges_AndBeforeCurrentChanged()
    {
        // Arrange
        DashboardViewModel dashboard = CreateDashboardViewModel();
        FakeServiceProvider services = new(new Dictionary<Type, object>
        {
            { typeof(DashboardViewModel), dashboard }
        });
        NavigationService navigationService = new(services);

        ViewModelBase? currentDuringConfigure = dashboard; // sentinel distinct from the expected null
        bool currentChangedFiredBeforeConfigure = false;
        bool currentChangedFired = false;
        navigationService.CurrentChanged += (_, _) => currentChangedFired = true;

        // Act
        navigationService.NavigateTo<DashboardViewModel>(RouteNames.Dashboard, vm =>
        {
            currentDuringConfigure = navigationService.CurrentViewModel;
            currentChangedFiredBeforeConfigure = currentChangedFired;
            Assert.Same(dashboard, vm);
        });

        // Assert
        Assert.Null(currentDuringConfigure);
        Assert.False(currentChangedFiredBeforeConfigure);
        Assert.Same(dashboard, navigationService.CurrentViewModel);
        Assert.True(currentChangedFired);
    }

    [Fact]
    public void NavigateToTyped_UnknownRoute_ThrowsInvalidOperationException()
    {
        // Arrange
        FakeServiceProvider services = new(new Dictionary<Type, object>
        {
            { typeof(DashboardViewModel), CreateDashboardViewModel() }
        });
        NavigationService navigationService = new(services);

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => navigationService.NavigateTo<DashboardViewModel>("Nope", _ => { }));
        Assert.Contains("Unknown route", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NavigateToTyped_RouteTypeMismatch_ThrowsInvalidOperationException()
    {
        // Arrange
        FakeServiceProvider services = new(new Dictionary<Type, object>
        {
            { typeof(DashboardViewModel), CreateDashboardViewModel() }
        });
        NavigationService navigationService = new(services);

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => navigationService.NavigateTo<IncomeViewModel>(RouteNames.Dashboard, _ => { }));
        Assert.Contains("not assignable", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NavigateToTyped_ValidRoute_PushesHistoryAndSupportsGoBack()
    {
        // Arrange
        DashboardViewModel dashboard = CreateDashboardViewModel();
        FakeServiceProvider services = new(new Dictionary<Type, object>
        {
            { typeof(DashboardViewModel), dashboard }
        });
        NavigationService navigationService = new(services);
        navigationService.NavigateTo(RouteNames.Dashboard);

        // Act
        navigationService.NavigateTo<DashboardViewModel>(RouteNames.Dashboard, _ => { });

        // Assert
        Assert.True(navigationService.CanGoBack);
    }

    [Fact]
    public void NavigateTo_TargetIsNavigationAware_CallsOnNavigatedToAfterActivation()
    {
        // Arrange
        RecordingNavigationAwareViewModel aware = new();
        const string awareRoute = "Aware";
        FakeServiceProvider services = new(new Dictionary<Type, object>
        {
            { typeof(RecordingNavigationAwareViewModel), aware }
        });
        NavigationService navigationService = new(
            services,
            new Dictionary<string, Type>(StringComparer.Ordinal) { { awareRoute, typeof(RecordingNavigationAwareViewModel) } });

        // Act
        navigationService.NavigateTo(awareRoute);

        // Assert
        Assert.Equal(1, aware.OnNavigatedToCallCount);
        Assert.Same(aware, navigationService.CurrentViewModel);
    }

    [Fact]
    public void NavigateTo_PreviousViewModelIsNavigationAware_CallsOnNavigatedFromOnLeave()
    {
        // Arrange
        RecordingNavigationAwareViewModel first = new();
        DashboardViewModel second = CreateDashboardViewModel();
        const string awareRoute = "Aware";
        FakeServiceProvider services = new(new Dictionary<Type, object>
        {
            { typeof(RecordingNavigationAwareViewModel), first },
            { typeof(DashboardViewModel), second }
        });
        NavigationService navigationService = new(
            services,
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                { awareRoute, typeof(RecordingNavigationAwareViewModel) },
                { RouteNames.Dashboard, typeof(DashboardViewModel) }
            });
        navigationService.NavigateTo(awareRoute);

        // Act
        navigationService.NavigateTo(RouteNames.Dashboard);

        // Assert
        Assert.Equal(1, first.OnNavigatedFromCallCount);
    }

    [Fact]
    public void GoBack_PreviousViewModelIsNavigationAware_CallsOnNavigatedToAfterReactivation()
    {
        // Arrange
        RecordingNavigationAwareViewModel first = new();
        DashboardViewModel second = CreateDashboardViewModel();
        const string awareRoute = "Aware";
        FakeServiceProvider services = new(new Dictionary<Type, object>
        {
            { typeof(RecordingNavigationAwareViewModel), first },
            { typeof(DashboardViewModel), second }
        });
        NavigationService navigationService = new(
            services,
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                { awareRoute, typeof(RecordingNavigationAwareViewModel) },
                { RouteNames.Dashboard, typeof(DashboardViewModel) }
            });
        navigationService.NavigateTo(awareRoute);
        navigationService.NavigateTo(RouteNames.Dashboard);

        // Act
        navigationService.GoBack();

        // Assert: once on the original NavigateTo, once more when GoBack reactivates it.
        Assert.Equal(2, first.OnNavigatedToCallCount);
        Assert.Same(first, navigationService.CurrentViewModel);
    }

    [Fact]
    public void GoBack_CurrentViewModelIsNavigationAware_CallsOnNavigatedFromBeforeSwitching()
    {
        // Arrange
        DashboardViewModel first = CreateDashboardViewModel();
        RecordingNavigationAwareViewModel second = new();
        const string awareRoute = "Aware";
        FakeServiceProvider services = new(new Dictionary<Type, object>
        {
            { typeof(DashboardViewModel), first },
            { typeof(RecordingNavigationAwareViewModel), second }
        });
        NavigationService navigationService = new(
            services,
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                { RouteNames.Dashboard, typeof(DashboardViewModel) },
                { awareRoute, typeof(RecordingNavigationAwareViewModel) }
            });
        navigationService.NavigateTo(RouteNames.Dashboard);
        navigationService.NavigateTo(awareRoute);

        // Act
        navigationService.GoBack();

        // Assert
        Assert.Equal(1, second.OnNavigatedFromCallCount);
        Assert.Same(first, navigationService.CurrentViewModel);
    }

    [Fact]
    public void NavigateTo_NonNavigationAwareViewModel_NavigatesNormallyWithoutError()
    {
        // Arrange: DashboardViewModel does not implement INavigationAware; the hook must be a
        // pure no-op for it.
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
    }

    private static DashboardViewModel CreateDashboardViewModel()
    {
        FakeExpenseRepository expenseRepo = new();
        FakeIncomeRepository incomeRepo = new();
        FakeCurrencyConverter converter = new();
        FakeClock clock = new() { Today = new DateOnly(2026, 3, 15) };

        FakeBudgetRepository budgetRepo = new();
        FakeCategoryRepository categoryRepo = new();

        return new DashboardViewModel(
            new GetMonthSummaryQuery(expenseRepo, incomeRepo, converter),
            new GetCategoryBreakdownQuery(expenseRepo, converter),
            new GetMonthlyEvolutionQuery(expenseRepo, incomeRepo, converter),
            new GetUpcomingPaymentsQuery(
                new FakeDebtPaymentRepository(), new FakeDebtRepository(), new FakeRecurringRuleRepository(), clock),
            new GetBudgetVsActualQuery(budgetRepo, expenseRepo, categoryRepo, converter),
            DashboardInsightsQueryBuilder.Build(expenseRepo, incomeRepo, converter, clock, budgetRepo, categoryRepo),
            clock,
            NullLogger<DashboardViewModel>.Instance);
    }

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

    /// <summary>
    /// Recording <see cref="INavigationAware"/> test double (F-015, slice S4) used to assert
    /// exactly when <see cref="NavigationService"/> invokes the navigate-away/navigate-to hooks.
    /// </summary>
    private sealed class RecordingNavigationAwareViewModel : ViewModelBase, INavigationAware
    {
        public int OnNavigatedToCallCount { get; private set; }

        public int OnNavigatedFromCallCount { get; private set; }

        public void OnNavigatedTo() => OnNavigatedToCallCount++;

        public void OnNavigatedFrom() => OnNavigatedFromCallCount++;
    }
}
