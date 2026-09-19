using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Calendar.GetCalendarMonth;
using ControlDeFinanzasIA.Application.UseCases.Catalog.CreateTag;
using ControlDeFinanzasIA.Application.UseCases.Catalog.GetPaymentMethods;
using ControlDeFinanzasIA.Application.UseCases.Expenses.GetTransactions;
using ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense;
using ControlDeFinanzasIA.Application.UseCases.Expenses.UpdateExpense;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.ApplySavedView;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.DeleteSavedView;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.GetSavedViews;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.SaveCurrentFilter;
using ControlDeFinanzasIA.Application.UseCases.Transactions.DeleteTransaction;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for <see cref="CalendarViewModel"/> (F-009, slice 3). Covers
/// the 42-cell grid invariant, month navigation recomputing the whole grid
/// (S-2), and the R-3/R-4 drill-down-vs-calendar-navigation split (S-3,
/// S-4, S-6) via the typed <see cref="INavigationService"/> overload.
/// </summary>
public sealed class CalendarViewModelTests
{
    // 2026-03-01 es un domingo (mismo fixture que GetCalendarMonthQueryTests).
    private static readonly DateOnly _march15 = new(2026, 3, 15);

    [Fact]
    public void Constructor_LoadsCurrentMonth_PopulatesFortyTwoCells()
    {
        // Arrange & Act
        CalendarViewModel viewModel = CreateViewModel();

        // Assert
        Assert.Equal(CalendarGrid.CellCount, viewModel.Cells.Count);
        Assert.Equal(MonthPeriod.Anchor(_march15), viewModel.SelectedMonth);
    }

    [Fact]
    public async Task PreviousMonthCommand_RecomputesWholeGridForPreviousMonth()
    {
        // Arrange
        CalendarViewModel viewModel = CreateViewModel();
        DateOnly originalMonth = viewModel.SelectedMonth;
        DateOnly firstCellDate = viewModel.Cells[0].Date;

        // Act
        await viewModel.PreviousMonthCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(MonthPeriod.Previous(originalMonth), viewModel.SelectedMonth);
        Assert.Equal(CalendarGrid.CellCount, viewModel.Cells.Count);
        Assert.NotEqual(firstCellDate, viewModel.Cells[0].Date);
    }

    [Fact]
    public async Task NextMonthCommand_RecomputesWholeGridForNextMonth()
    {
        // Arrange
        CalendarViewModel viewModel = CreateViewModel();
        DateOnly originalMonth = viewModel.SelectedMonth;
        DateOnly firstCellDate = viewModel.Cells[0].Date;

        // Act
        await viewModel.NextMonthCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(MonthPeriod.Next(originalMonth), viewModel.SelectedMonth);
        Assert.Equal(CalendarGrid.CellCount, viewModel.Cells.Count);
        Assert.NotEqual(firstCellDate, viewModel.Cells[0].Date);
    }

    [Fact]
    public async Task SelectDayCommand_CurrentMonthDay_NavigatesToExpensesFilteredToThatDay()
    {
        // Arrange (S-3/S-4): a click on a day of the navigated month must drill down to Gastos with
        // DateFrom == DateTo == the clicked day, via the additive typed NavigateTo overload.
        ExpensesViewModel expensesViewModel = CreateExpensesViewModel();
        FakeNavigationService navigationService = new(new Dictionary<string, ViewModelBase>(StringComparer.Ordinal)
        {
            { RouteNames.Expenses, expensesViewModel }
        });
        CalendarViewModel viewModel = CreateViewModel(navigationService: navigationService);
        DateOnly clickedDay = new(2026, 3, 10);
        CalendarCell cell = new(clickedDay, "10", string.Empty, IsCurrentMonth: true, IsToday: false);

        // Act
        await viewModel.SelectDayCommand.ExecuteAsync(cell);

        // Assert
        Assert.Single(navigationService.NavigateToTypedCalls);
        Assert.Equal(RouteNames.Expenses, navigationService.NavigateToTypedCalls[0]);
        Assert.Equal(clickedDay, expensesViewModel.CurrentFilter.DateFrom);
        Assert.Equal(clickedDay, expensesViewModel.CurrentFilter.DateTo);
        // The calendar's own SelectedMonth must not change on a current-month drill-down.
        Assert.Equal(MonthPeriod.Anchor(_march15), viewModel.SelectedMonth);
    }

    [Fact]
    public async Task SelectDayCommand_AdjacentMonthDay_NavigatesCalendarGrid_DoesNotDrillDown()
    {
        // Arrange (R-3, S-6): a click on an adjacent-month filler day navigates the calendar grid to
        // that day's month instead of drilling down to Gastos.
        ExpensesViewModel expensesViewModel = CreateExpensesViewModel();
        FakeNavigationService navigationService = new(new Dictionary<string, ViewModelBase>(StringComparer.Ordinal)
        {
            { RouteNames.Expenses, expensesViewModel }
        });
        CalendarViewModel viewModel = CreateViewModel(navigationService: navigationService);
        DateOnly adjacentDay = new(2026, 4, 2); // April, while the calendar is navigated to March.
        CalendarCell cell = new(adjacentDay, "2", string.Empty, IsCurrentMonth: false, IsToday: false);

        // Act
        await viewModel.SelectDayCommand.ExecuteAsync(cell);

        // Assert
        Assert.Empty(navigationService.NavigateToTypedCalls);
        Assert.Equal(MonthPeriod.Anchor(adjacentDay), viewModel.SelectedMonth);
        Assert.Equal(CalendarGrid.CellCount, viewModel.Cells.Count);
    }

    [Fact]
    public async Task SelectDayCommand_NullCell_IsNoOp()
    {
        // Arrange
        FakeNavigationService navigationService = new();
        CalendarViewModel viewModel = CreateViewModel(navigationService: navigationService);
        DateOnly monthBefore = viewModel.SelectedMonth;

        // Act
        await viewModel.SelectDayCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(navigationService.NavigateToTypedCalls);
        Assert.Equal(monthBefore, viewModel.SelectedMonth);
    }

    private static CalendarViewModel CreateViewModel(
        FakeClock? clock = null,
        FakeExpenseRepository? expenseRepo = null,
        FakeNavigationService? navigationService = null)
    {
        clock ??= new FakeClock
        {
            UtcNow = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Today = _march15,
        };
        expenseRepo ??= new FakeExpenseRepository();
        navigationService ??= new FakeNavigationService();

        GetCalendarMonthQuery query = new(expenseRepo, new FakeCurrencyConverter(), clock);

        return new CalendarViewModel(
            query,
            navigationService,
            clock,
            NullLogger<CalendarViewModel>.Instance);
    }

    /// <summary>
    /// Builds a minimal but real <see cref="ExpensesViewModel"/> so the
    /// drill-down tests can assert against <see cref="ExpensesViewModel.ApplyDayFilter"/>'s
    /// actual effect on <see cref="ExpensesViewModel.CurrentFilter"/>, exactly
    /// like <see cref="NavigationService"/> would resolve it via DI.
    /// </summary>
    private static ExpensesViewModel CreateExpensesViewModel()
    {
        FakeClock clock = new()
        {
            UtcNow = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Today = _march15,
        };
        FakeExpenseRepository expenseRepo = new();
        FakeIncomeRepository incomeRepo = new();
        FakeCategoryRepository categoryRepo = new();
        FakeTagRepository tagRepo = new();
        FakeSavedViewRepository savedViewRepo = new();
        FakePaymentMethodRepository paymentMethodRepo = new();

        RegisterExpenseUseCase registerUseCase = new(
            expenseRepo, clock, new RegisterExpenseValidator(), NullLogger<RegisterExpenseUseCase>.Instance);
        GetTransactionsQuery getTransactionsQuery = new(expenseRepo, NullLogger<GetTransactionsQuery>.Instance);
        UpdateExpenseUseCase updateExpenseUseCase = new(
            expenseRepo, clock, new UpdateExpenseValidator(), NullLogger<UpdateExpenseUseCase>.Instance);
        DeleteTransactionUseCase deleteTransactionUseCase = new(
            expenseRepo, incomeRepo, NullLogger<DeleteTransactionUseCase>.Instance);
        SaveCurrentFilterUseCase saveCurrentFilterUseCase = new(
            savedViewRepo, clock, new SaveCurrentFilterValidator(), NullLogger<SaveCurrentFilterUseCase>.Instance);
        ApplySavedViewUseCase applySavedViewUseCase = new(savedViewRepo, NullLogger<ApplySavedViewUseCase>.Instance);
        GetSavedViewsUseCase getSavedViewsUseCase = new(savedViewRepo);
        DeleteSavedViewUseCase deleteSavedViewUseCase = new(
            savedViewRepo, NullLogger<DeleteSavedViewUseCase>.Instance);
        CreateTagUseCase createTagUseCase = new(
            tagRepo, clock, new CreateTagValidator(), NullLogger<CreateTagUseCase>.Instance);
        GetPaymentMethodsQuery getPaymentMethodsQuery = new(
            paymentMethodRepo, NullLogger<GetPaymentMethodsQuery>.Instance);
        IStringLocalizer<Strings> localizer = new FakeStringLocalizer<Strings>();

        return new ExpensesViewModel(
            registerUseCase,
            getTransactionsQuery,
            updateExpenseUseCase,
            deleteTransactionUseCase,
            saveCurrentFilterUseCase,
            applySavedViewUseCase,
            getSavedViewsUseCase,
            deleteSavedViewUseCase,
            createTagUseCase,
            categoryRepo,
            tagRepo,
            getPaymentMethodsQuery,
            new FakeCurrencyConverter(),
            clock,
            NullLogger<ExpensesViewModel>.Instance,
            localizer);
    }
}
