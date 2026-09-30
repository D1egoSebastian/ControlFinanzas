using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.Tests.Unit.UI;
using ControlDeFinanzasIA.UI.Input;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.ViewModels;

/// <summary>
/// F-021 S7a (R-12, S-25..S-28): the shortcut commands of the REAL <see cref="ShellViewModel"/>. Navigation uses the
/// REAL <see cref="NavigationService"/> over real Expenses/Income view-models wherever the route callback matters.
/// </summary>
public sealed class ShellShortcutTests
{
    private sealed class ProbeViewModel : ViewModelBase
    {
    }

    private sealed class Rig
    {
        private readonly FakeExpenseRepository _expenses = new() { PageAddedItems = true };
        private readonly FakeIncomeRepository _incomes = new() { PageAddedItems = true };

        public Rig()
        {
            var services = new ServiceCollection();
            services.AddTransient<ProbeViewModel>();
            services.AddTransient(_ =>
            {
                ExpensesResolved++;
                return LastExpenses = ExpensesViewModelTests.CreateViewModel(expenseRepo: _expenses);
            });
            services.AddTransient(_ =>
            {
                IncomeResolved++;
                return LastIncome = IncomeViewModelTests.CreateViewModel(incomeRepo: _incomes);
            });

            Navigation = new NavigationService(
                services.BuildServiceProvider(),
                new Dictionary<string, Type>
                {
                    ["Probe"] = typeof(ProbeViewModel),
                    [RouteNames.Expenses] = typeof(ExpensesViewModel),
                    [RouteNames.Incomes] = typeof(IncomeViewModel),
                });
            Overlay = new ErrorOverlayViewModel(new FakeClipboardService(), "1.0.0");
            Shell = new ShellViewModel(
                Navigation,
                new FakeStringLocalizer<Strings>(),
                new NotificationListViewModel(
                    new FakeNotificationCenter(),
                    new FakeClock { Today = new DateOnly(2026, 9, 20) },
                    NullLogger<NotificationListViewModel>.Instance),
                Overlay);
            Navigation.NavigateTo("Probe");
        }

        public int ExpensesResolved { get; private set; }

        public int IncomeResolved { get; private set; }

        public ExpensesViewModel? LastExpenses { get; private set; }

        public IncomeViewModel? LastIncome { get; private set; }

        public NavigationService Navigation { get; }

        public ErrorOverlayViewModel Overlay { get; }

        public ShellViewModel Shell { get; }

        public async Task<ExpensesViewModel> OpenExpensesWithOneRowAsync()
        {
            await _expenses.AddAsync(Expense.Create(
                Guid.NewGuid(),
                new DateOnly(2026, 1, 10),
                Money.Create(100m, Currency.DOP),
                "Gasto",
                null,
                new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
            Navigation.NavigateTo(RouteNames.Expenses);
            ExpensesViewModel vm = LastExpenses!;
            await Until(() => vm.HasLoaded);
            return vm;
        }

        public async Task<IncomeViewModel> OpenIncomeWithOneRowAsync()
        {
            await _incomes.AddAsync(Income.Create(
                null,
                new DateOnly(2026, 1, 10),
                Money.Create(100m, Currency.DOP),
                "Ingreso",
                null,
                null,
                new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
            Navigation.NavigateTo(RouteNames.Incomes);
            IncomeViewModel vm = LastIncome!;
            await Until(() => vm.HasLoaded);
            return vm;
        }

        private static async Task Until(Func<bool> condition)
        {
            for (int i = 0; i < 300 && !condition(); i++)
            {
                await Task.Delay(10);
            }

            Assert.True(condition(), "Condition was not reached in time.");
        }
    }

    private static ShellViewModel ShellOver(FakeNavigationService navigation) =>
        new(
            navigation,
            new FakeStringLocalizer<Strings>(),
            new NotificationListViewModel(
                new FakeNotificationCenter(),
                new FakeClock { Today = new DateOnly(2026, 9, 20) },
                NullLogger<NotificationListViewModel>.Instance),
            new ErrorOverlayViewModel(new FakeClipboardService(), "1.0.0"));

    private static FakeNavigationService AllSectionsNavigation(ViewModelBase? start = null)
    {
        var routes = NavigationSections.All.ToDictionary(s => s.Route, _ => (ViewModelBase)new ProbeViewModel(), StringComparer.Ordinal);
        return new FakeNavigationService(routes) { CurrentViewModel = start ?? new ProbeViewModel() };
    }

    [Fact]
    public void NewExpense_FromAnotherSection_NavigatesToExpenses_AndConfiguresItThroughTheRouteCallback()
    {
        var rig = new Rig();
        bool tokenWasSetBeforeActivation = false;
        rig.Navigation.CurrentChanged += (_, _) =>
            tokenWasSetBeforeActivation = rig.LastExpenses?.DescriptionFocusRequest is not null;

        rig.Shell.NewExpenseCommand.Execute(null);

        ExpensesViewModel current = Assert.IsType<ExpensesViewModel>(rig.Navigation.CurrentViewModel);
        Assert.Same(rig.LastExpenses, current);
        Assert.NotNull(current.DescriptionFocusRequest);
        Assert.True(tokenWasSetBeforeActivation);
        Assert.Equal(1, rig.ExpensesResolved);
    }

    [Fact]
    public async Task NewExpense_FromExpenses_ReusesTheSameInstance_AndRaisesANewFocusRequest()
    {
        var rig = new Rig();
        ExpensesViewModel expenses = await rig.OpenExpensesWithOneRowAsync();
        expenses.BeginNewEntry();
        FocusRequestToken? before = expenses.DescriptionFocusRequest;
        int resolved = rig.ExpensesResolved;
        int historyProbe = 0;
        rig.Navigation.CurrentChanged += (_, _) => historyProbe++;

        rig.Shell.NewExpenseCommand.Execute(null);

        Assert.Same(expenses, rig.Navigation.CurrentViewModel);
        Assert.Equal(resolved, rig.ExpensesResolved);
        Assert.Equal(0, historyProbe);
        Assert.NotNull(expenses.DescriptionFocusRequest);
        Assert.NotSame(before, expenses.DescriptionFocusRequest);
    }

    [Fact]
    public async Task NewExpense_CancelsAnEditInProgress()
    {
        var rig = new Rig();
        ExpensesViewModel expenses = await rig.OpenExpensesWithOneRowAsync();
        expenses.BeginEditCommand.Execute(expenses.Expenses.Single());
        Assert.True(expenses.IsEditMode);

        rig.Shell.NewExpenseCommand.Execute(null);

        Assert.False(expenses.IsEditMode);
        Assert.Null(expenses.EditableId);
        Assert.Null(expenses.Description);
    }

    [Fact]
    public void NewIncome_FromAnotherSection_NavigatesToIncome_AndConfiguresItThroughTheRouteCallback()
    {
        var rig = new Rig();
        bool tokenWasSetBeforeActivation = false;
        rig.Navigation.CurrentChanged += (_, _) =>
            tokenWasSetBeforeActivation = rig.LastIncome?.DescriptionFocusRequest is not null;

        rig.Shell.NewIncomeCommand.Execute(null);

        IncomeViewModel current = Assert.IsType<IncomeViewModel>(rig.Navigation.CurrentViewModel);
        Assert.Same(rig.LastIncome, current);
        Assert.NotNull(current.DescriptionFocusRequest);
        Assert.True(tokenWasSetBeforeActivation);
        Assert.Equal(1, rig.IncomeResolved);
    }

    [Fact]
    public async Task NewIncome_FromIncome_ReusesTheSameInstance_AndCancelsAnEditInProgress()
    {
        var rig = new Rig();
        IncomeViewModel income = await rig.OpenIncomeWithOneRowAsync();
        income.BeginEditCommand.Execute(income.Incomes.Single());
        Assert.True(income.IsEditMode);
        int resolved = rig.IncomeResolved;

        rig.Shell.NewIncomeCommand.Execute(null);

        Assert.Same(income, rig.Navigation.CurrentViewModel);
        Assert.Equal(resolved, rig.IncomeResolved);
        Assert.False(income.IsEditMode);
        Assert.NotNull(income.DescriptionFocusRequest);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 4)]
    [InlineData(6, 5)]
    [InlineData(7, 6)]
    [InlineData(8, 7)]
    [InlineData(9, 8)]
    [InlineData(0, 9)]
    public void GoToSection_OpensTheSectionAtTheDigitPosition_AndAsksForFocusInTheNewScreen(int digit, int position)
    {
        FakeNavigationService navigation = AllSectionsNavigation();
        ShellViewModel shell = ShellOver(navigation);

        shell.GoToSectionCommand.Execute(digit);

        Assert.Equal([NavigationSections.All[position].Route], navigation.NavigateCalls);
        Assert.NotNull(shell.ContentFocusRequest);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(-1)]
    public void GoToSection_WithADigitBeyondTheTenSections_DoesNothing(int digit)
    {
        FakeNavigationService navigation = AllSectionsNavigation();
        ShellViewModel shell = ShellOver(navigation);

        shell.GoToSectionCommand.Execute(digit);

        Assert.Empty(navigation.NavigateCalls);
        Assert.Null(shell.ContentFocusRequest);
    }

    [Fact]
    public async Task GoToSection_ToTheCurrentSection_DoesNotNavigateReloadOrPushHistory()
    {
        var rig = new Rig();
        ExpensesViewModel expenses = await rig.OpenExpensesWithOneRowAsync();
        int resolved = rig.ExpensesResolved;
        int changes = 0;
        rig.Navigation.CurrentChanged += (_, _) => changes++;
        bool canGoBack = rig.Navigation.CanGoBack;

        rig.Shell.GoToSectionCommand.Execute(2);

        Assert.Same(expenses, rig.Navigation.CurrentViewModel);
        Assert.Equal(resolved, rig.ExpensesResolved);
        Assert.Equal(0, changes);
        Assert.Equal(canGoBack, rig.Navigation.CanGoBack);
        Assert.Null(rig.Shell.ContentFocusRequest);
    }

    [Fact]
    public void OpenSettings_OpensTheSettingsSection()
    {
        FakeNavigationService navigation = AllSectionsNavigation();
        ShellViewModel shell = ShellOver(navigation);

        shell.OpenSettingsCommand.Execute(null);

        Assert.Equal([RouteNames.Settings], navigation.NavigateCalls);
        Assert.NotNull(shell.ContentFocusRequest);
    }

    [Fact]
    public void ToggleNotifications_FlipsTheBellPopup()
    {
        ShellViewModel shell = ShellOver(AllSectionsNavigation());

        shell.ToggleNotificationsCommand.Execute(null);
        Assert.True(shell.Notifications.IsOpen);

        shell.ToggleNotificationsCommand.Execute(null);
        Assert.False(shell.Notifications.IsOpen);
    }

    [Fact]
    public void Escape_ClosesTheOverlayFirst_ElseThePopup_ElseNothing()
    {
        var overlay = new ErrorOverlayViewModel(new FakeClipboardService(), "1.0.0");
        ShellViewModel shell = new(
            AllSectionsNavigation(),
            new FakeStringLocalizer<Strings>(),
            new NotificationListViewModel(
                new FakeNotificationCenter(),
                new FakeClock { Today = new DateOnly(2026, 9, 20) },
                NullLogger<NotificationListViewModel>.Instance),
            overlay);

        overlay.Present(Guid.NewGuid(), DateTime.UtcNow);
        shell.EscapeCommand.Execute(null);
        Assert.False(overlay.IsOpen);

        shell.Notifications.IsOpen = true;
        shell.EscapeCommand.Execute(null);
        Assert.False(shell.Notifications.IsOpen);

        shell.EscapeCommand.Execute(null);
        Assert.False(shell.Notifications.IsOpen);
        Assert.False(overlay.IsOpen);

        // Overlay beats popup: with both requested, the overlay closes and the popup state is untouched by Escape.
        shell.Notifications.IsOpen = true;
        overlay.Present(Guid.NewGuid(), DateTime.UtcNow);
        shell.Notifications.IsOpen = true;
        shell.EscapeCommand.Execute(null);
        Assert.False(overlay.IsOpen);
        Assert.True(shell.Notifications.IsOpen);
    }

    [Fact]
    public void Escape_WithoutAnOverlayInstance_StillClosesThePopup()
    {
        var shell = new ShellViewModel(
            AllSectionsNavigation(),
            new FakeStringLocalizer<Strings>(),
            new NotificationListViewModel(
                new FakeNotificationCenter(),
                new FakeClock { Today = new DateOnly(2026, 9, 20) },
                NullLogger<NotificationListViewModel>.Instance));
        shell.Notifications.IsOpen = true;

        shell.EscapeCommand.Execute(null);

        Assert.False(shell.Notifications.IsOpen);
    }
}
