using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Chat.AskFinanceChat;
using ControlDeFinanzasIA.Application.UseCases.Chat.BuildChatContext;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthExpenses;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthlyEvolution;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebts;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebtSchedule;
using ControlDeFinanzasIA.Application.UseCases.Goals.GetGoals;
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
        ShellViewModel shell = new(fakeNav, CreateLocalizer(), CreateNotifications());
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
        ShellViewModel shell = new(fakeNav, CreateLocalizer(), CreateNotifications());

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
        ShellViewModel shell = new(fakeNav, CreateLocalizer(), CreateNotifications());
        DashboardViewModel dashboard = CreateDashboardViewModel();

        // Act
        fakeNav.CurrentViewModel = dashboard;
        fakeNav.RaiseCurrentChanged();

        // Assert
        Assert.Same(dashboard, shell.Current);
    }

    // ------------------------------------------------------------------
    // F-015 slice S6 (R-1): full routing checklist coverage.
    // ------------------------------------------------------------------

    [Fact]
    public void MenuItems_IncludesExactlyOneChatEntry()
    {
        // Arrange
        FakeNavigationService fakeNav = new();

        // Act
        ShellViewModel shell = new(fakeNav, CreateLocalizer(), CreateNotifications());

        // Assert
        Assert.Equal(10, shell.MenuItems.Count);
        Assert.Single(shell.MenuItems, item => item.Route == RouteNames.Chat);
    }

    [Fact]
    public void CurrentMenuItem_WhenChatIsActive_HighlightsChatEntry()
    {
        // Arrange
        FakeNavigationService fakeNav = new();
        ShellViewModel shell = new(fakeNav, CreateLocalizer(), CreateNotifications());

        // Act
        fakeNav.CurrentViewModel = CreateChatViewModel();
        fakeNav.RaiseCurrentChanged();

        // Assert
        Assert.NotNull(shell.SelectedMenuItem);
        Assert.Equal(RouteNames.Chat, shell.SelectedMenuItem!.Route);
        Assert.Equal(RouteNames.Chat, shell.CurrentMenuItem!.Route);
    }

    // ------------------------------------------------------------------
    // F-017 slice S5b (R-3): the shell exposes the notification list.
    // ------------------------------------------------------------------

    [Fact]
    public void Notifications_ExposesTheInjectedNotificationListViewModel()
    {
        // Arrange
        NotificationListViewModel notifications = CreateNotifications();

        // Act
        ShellViewModel shell = new(new FakeNavigationService(), CreateLocalizer(), notifications);

        // Assert
        Assert.Same(notifications, shell.Notifications);
    }

    [Fact]
    public void Constructor_NullNotifications_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ShellViewModel(new FakeNavigationService(), CreateLocalizer(), null!));
    }

    private static NotificationListViewModel CreateNotifications()
        => new(
            new FakeNotificationCenter(),
            new FakeClock { Today = new DateOnly(2026, 9, 20) },
            NullLogger<NotificationListViewModel>.Instance);

    private static IStringLocalizer<Strings> CreateLocalizer()
        => new FakeStringLocalizer<Strings>(new Dictionary<string, string>
        {
            { "SidebarDashboard", "Panel" },
            { "SidebarExpenses", "Gastos" },
            { "SidebarChat", "Chat" }
        });

    /// <summary>
    /// Builds a minimally-wired <see cref="ChatViewModel"/> for routing tests: real dependency
    /// types with default "not configured"/empty fakes, since this test only asserts sidebar
    /// highlight wiring, never chat behavior (already covered by <c>ChatViewModelTests</c>, slice S5).
    /// </summary>
    private static ChatViewModel CreateChatViewModel()
    {
        FakeSettingsStore settingsStore = new() { AppSettings = AppSettings.Defaults };
        FakeLlmClientFactory factory = new();
        FakePromptProvider prompts = new();

        FakeExpenseRepository expenseRepo = new();
        FakeIncomeRepository incomeRepo = new();
        FakeBudgetRepository budgetRepo = new();
        FakeCategoryRepository categoryRepo = new();
        FakeGoalRepository goalRepo = new();
        FakeGoalContributionRepository goalContributionRepo = new();
        FakeDebtRepository debtRepo = new();
        FakeDebtPaymentRepository debtPaymentRepo = new();
        FakeRecurringRuleRepository recurringRuleRepo = new();
        FakeCurrencyConverter converter = new();
        FakeClock clock = new() { Today = new DateOnly(2026, 9, 20) };

        var buildContext = new BuildChatContextQuery(
            new GetMonthSummaryQuery(expenseRepo, incomeRepo, converter),
            new GetCategoryBreakdownQuery(expenseRepo, converter),
            new GetBudgetVsActualQuery(budgetRepo, expenseRepo, categoryRepo, converter),
            new GetGoalsQuery(goalRepo, goalContributionRepo, clock),
            new GetDebtsQuery(debtRepo),
            new GetDebtScheduleQuery(debtRepo, debtPaymentRepo),
            new GetUpcomingPaymentsQuery(debtPaymentRepo, debtRepo, recurringRuleRepo, clock),
            converter,
            clock,
            NullLogger<BuildChatContextQuery>.Instance);

        var useCase = new AskFinanceChatUseCase(
            settingsStore, factory, buildContext, prompts, NullLogger<AskFinanceChatUseCase>.Instance);

        return new ChatViewModel(
            useCase,
            new FakeClipboardService(),
            new FakeStringLocalizer<Strings>(),
            NullLogger<ChatViewModel>.Instance);
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
            NullLogger<DashboardViewModel>.Instance,
            ExportFixtures.Create(expenseRepo, incomeRepo, converter).UseCase,
            new FakeFileSaveDialog(),
            new GetMonthExpensesQuery(expenseRepo, categoryRepo, converter),
            new FakeNavigationService());
    }
}
