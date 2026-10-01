using System.ComponentModel;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.ViewModels;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// 1.0 blockers D1 and D2 (QA audit): the paging and delete behavior behind the Expenses list, driven through the
/// REAL view-model and the real use cases over an in-memory repository that really pages.
/// </summary>
public sealed class ExpensesViewModelLedgerTests
{
    private static async Task<(ExpensesViewModel Vm, FakeExpenseRepository Repo)> LoadedAsync(int count)
    {
        var repo = new FakeExpenseRepository { PageAddedItems = true };
        for (int i = 0; i < count; i++)
        {
            await repo.AddAsync(
                Expense.Create(
                    Guid.NewGuid(),
                    new DateOnly(2026, 1, 1).AddDays(i),
                    Money.Create(10m + i, Currency.DOP),
                    $"Gasto {i}",
                    null,
                    new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        }

        ExpensesViewModel vm = ExpensesViewModelTests.CreateViewModel(expenseRepo: repo);
        await vm.RefreshCommand.ExecuteAsync(null);
        return (vm, repo);
    }

    [Fact]
    public async Task AfterTheFirstLoad_TheTotalCountIsTheNumberOfRecords_NotThePageSize()
    {
        (ExpensesViewModel vm, _) = await LoadedAsync(45);

        Assert.Equal(20, vm.Expenses.Count);
        Assert.Equal(45, vm.TotalCount);
        Assert.Equal(3, vm.TotalPages);
    }

    [Fact]
    public async Task TheNextAndPreviousCommands_ReflectThePosition_AsThePageMoves()
    {
        (ExpensesViewModel vm, _) = await LoadedAsync(45);

        // First page: nothing before it, something after it. Without change notifications the commands would stay
        // frozen at the state they had when the view-model was built (page 1, zero pages).
        Assert.False(vm.PreviousPageCommand.CanExecute(null));
        Assert.True(vm.NextPageCommand.CanExecute(null));

        // A bound button only re-evaluates when the command says its state changed.
        int previousChanged = 0;
        int nextChanged = 0;
        vm.PreviousPageCommand.CanExecuteChanged += (_, _) => previousChanged++;
        vm.NextPageCommand.CanExecuteChanged += (_, _) => nextChanged++;

        await vm.NextPageCommand.ExecuteAsync(null);
        await vm.NextPageCommand.ExecuteAsync(null);

        Assert.True(previousChanged > 0);
        Assert.True(nextChanged > 0);

        Assert.Equal(3, vm.Page);
        Assert.Equal(5, vm.Expenses.Count);
        Assert.True(vm.PreviousPageCommand.CanExecute(null));
        Assert.False(vm.NextPageCommand.CanExecute(null));
    }

    [Fact]
    public async Task ThePagerCommands_RaiseCanExecuteChanged_WhenOnlyThePositionChanges()
    {
        (ExpensesViewModel vm, _) = await LoadedAsync(45);
        int previousChanged = 0;
        int nextChanged = 0;
        vm.PreviousPageCommand.CanExecuteChanged += (_, _) => previousChanged++;
        vm.NextPageCommand.CanExecuteChanged += (_, _) => nextChanged++;

        vm.Page = 2;

        Assert.Equal(1, previousChanged);
        Assert.Equal(1, nextChanged);
    }

    [Fact]
    public async Task ThePageSummary_RaisesAChangeNotification_WhenThePageMoves()
    {
        (ExpensesViewModel vm, _) = await LoadedAsync(45);
        var raised = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        await vm.NextPageCommand.ExecuteAsync(null);

        Assert.Contains(nameof(ExpensesViewModel.PageSummary), raised);
    }

    [Fact]
    public async Task AnOldRecord_IsReachableOnALaterPage()
    {
        (ExpensesViewModel vm, _) = await LoadedAsync(45);
        Assert.DoesNotContain(vm.Expenses, e => e.Description == "Gasto 0");

        await vm.NextPageCommand.ExecuteAsync(null);
        await vm.NextPageCommand.ExecuteAsync(null);

        Assert.Contains(vm.Expenses, e => e.Description == "Gasto 0");
    }

    [Fact]
    public async Task Delete_NeedsTwoSteps_AndTheFirstOnlyArmsTheRow()
    {
        (ExpensesViewModel vm, FakeExpenseRepository repo) = await LoadedAsync(3);
        ExpenseDto row = vm.Expenses[0];

        await vm.DeleteExpenseCommand.ExecuteAsync(row);

        Assert.Equal(row.Id, vm.PendingDeleteExpenseId);
        Assert.Equal(3, repo.AddedExpenses.Count);
        Assert.Equal(3, vm.TotalCount);
    }

    [Fact]
    public async Task ConfirmingTheDelete_RemovesTheRecord_AndTheTotalDecrements()
    {
        (ExpensesViewModel vm, FakeExpenseRepository repo) = await LoadedAsync(3);
        ExpenseDto row = vm.Expenses[0];

        await vm.DeleteExpenseCommand.ExecuteAsync(row);
        await vm.DeleteExpenseCommand.ExecuteAsync(row);

        Assert.Null(vm.PendingDeleteExpenseId);
        Assert.Equal(2, repo.AddedExpenses.Count);
        Assert.Equal(2, vm.TotalCount);
        Assert.DoesNotContain(vm.Expenses, e => e.Id == row.Id);
    }

    [Fact]
    public async Task CancellingTheDelete_DisarmsTheRow_AndKeepsTheRecord()
    {
        (ExpensesViewModel vm, FakeExpenseRepository repo) = await LoadedAsync(3);
        ExpenseDto row = vm.Expenses[0];
        await vm.DeleteExpenseCommand.ExecuteAsync(row);

        vm.CancelDeleteCommand.Execute(null);

        Assert.Null(vm.PendingDeleteExpenseId);
        Assert.Equal(3, repo.AddedExpenses.Count);
        Assert.Equal(3, vm.TotalCount);
    }

    [Fact]
    public async Task DeletingTheLastRowOfTheLastPage_StepsBackToTheNewLastPage()
    {
        (ExpensesViewModel vm, _) = await LoadedAsync(21);
        await vm.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Page);
        ExpenseDto only = Assert.Single(vm.Expenses);

        await vm.DeleteExpenseCommand.ExecuteAsync(only);
        await vm.DeleteExpenseCommand.ExecuteAsync(only);

        Assert.Equal(1, vm.Page);
        Assert.Equal(1, vm.TotalPages);
        Assert.Equal(20, vm.Expenses.Count);
        Assert.Equal(20, vm.TotalCount);
        Assert.False(vm.NextPageCommand.CanExecute(null));
        Assert.False(vm.IsEmptyNoData);
    }

    [Fact]
    public async Task DeletingTheOnlyRecord_LeavesPageOne_AndShowsTheNoDataState()
    {
        (ExpensesViewModel vm, _) = await LoadedAsync(1);
        ExpenseDto only = Assert.Single(vm.Expenses);

        await vm.DeleteExpenseCommand.ExecuteAsync(only);
        await vm.DeleteExpenseCommand.ExecuteAsync(only);

        Assert.Equal(1, vm.Page);
        Assert.Equal(0, vm.TotalCount);
        Assert.Empty(vm.Expenses);
        Assert.True(vm.IsEmptyNoData);
    }

    private static readonly Guid _food = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _transport = new("11111111-1111-1111-1111-000000000002");

    private static async Task<ExpensesViewModel> LoadedMixedAsync()
    {
        var repo = new FakeExpenseRepository { PageAddedItems = true };
        (Guid Cat, DateOnly Date, string Text)[] seed =
        [
            (_food, new DateOnly(2026, 1, 5), "Pizza almuerzo"),
            (_food, new DateOnly(2026, 1, 20), "Supermercado"),
            (_transport, new DateOnly(2026, 2, 3), "Gasolina"),
            (_transport, new DateOnly(2026, 2, 10), "Pizza taxi"),
        ];

        foreach ((Guid cat, DateOnly date, string text) in seed)
        {
            await repo.AddAsync(
                Expense.Create(cat, date, Money.Create(100m, Currency.DOP), text, null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        }

        ExpensesViewModel vm = ExpensesViewModelTests.CreateViewModel(expenseRepo: repo);
        await vm.RefreshCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task TheTextFilter_NarrowsTheList_AndCountsAsAnActiveFilter()
    {
        ExpensesViewModel vm = await LoadedMixedAsync();
        Assert.Equal(4, vm.TotalCount);

        vm.FilterText = "pizza";
        await vm.ApplyFilterCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.TotalCount);
        Assert.All(vm.Expenses, e => Assert.Contains("Pizza", e.Description));
        Assert.True(vm.HasActiveFilter);
    }

    [Fact]
    public async Task TheCategoryAndTheDateRange_NarrowTheListTogetherWithTheText()
    {
        ExpensesViewModel vm = await LoadedMixedAsync();

        vm.FilterCategoryId = _transport;
        await vm.ApplyFilterCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.TotalCount);

        vm.FilterText = "pizza";
        await vm.ApplyFilterCommand.ExecuteAsync(null);
        Assert.Equal("Pizza taxi", Assert.Single(vm.Expenses).Description);

        vm.FilterDateTo = new DateOnly(2026, 2, 5);
        await vm.ApplyFilterCommand.ExecuteAsync(null);
        Assert.Equal(0, vm.TotalCount);
        Assert.True(vm.IsEmptyFiltered);
    }

    [Fact]
    public async Task ATextThatMatchesNothing_ShowsTheFilteredEmptyState_NotTheNoDataOne()
    {
        ExpensesViewModel vm = await LoadedMixedAsync();

        vm.FilterText = "zzz";
        await vm.ApplyFilterCommand.ExecuteAsync(null);

        Assert.Empty(vm.Expenses);
        Assert.True(vm.IsEmptyFiltered);
        Assert.False(vm.IsEmptyNoData);
    }

    [Fact]
    public async Task ClearFilter_ResetsTheTextAndBringsEveryRecordBack()
    {
        ExpensesViewModel vm = await LoadedMixedAsync();
        vm.FilterText = "pizza";
        await vm.ApplyFilterCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.TotalCount);

        await vm.ClearFilterCommand.ExecuteAsync(null);

        Assert.Null(vm.FilterText);
        Assert.False(vm.HasActiveFilter);
        Assert.Equal(4, vm.TotalCount);
    }

    [Fact]
    public async Task AnInvertedDateRange_IsReportedAsAFilterError_AndTheListIsLeftAlone()
    {
        ExpensesViewModel vm = await LoadedMixedAsync();
        vm.FilterDateFrom = new DateOnly(2026, 3, 1);
        vm.FilterDateTo = new DateOnly(2026, 1, 1);

        await vm.ApplyFilterCommand.ExecuteAsync(null);

        Assert.Equal("FilterDateRangeInvalidMessage", vm.FilterError);
        Assert.Null(vm.FormError);
        Assert.Equal(4, vm.TotalCount);
        Assert.False(vm.HasActiveFilter);

        vm.FilterDateTo = new DateOnly(2026, 3, 31);
        await vm.ApplyFilterCommand.ExecuteAsync(null);

        Assert.Null(vm.FilterError);
    }

    [Fact]
    public async Task ApplyingAFilter_ReturnsToPageOne()
    {
        (ExpensesViewModel vm, _) = await LoadedAsync(45);
        await vm.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Page);

        vm.FilterText = "Gasto 4";
        await vm.ApplyFilterCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.Page);
        Assert.Equal(6, vm.TotalCount);
    }

    [Fact]
    public async Task ASavedView_RestoresTheTextFilter_AndNarrowsTheList()
    {
        var repo = new FakeExpenseRepository { PageAddedItems = true };
        foreach (string text in new[] { "Pizza uno", "Gasolina", "Pizza dos" })
        {
            await repo.AddAsync(
                Expense.Create(_food, new DateOnly(2026, 1, 5), Money.Create(1m, Currency.DOP), text, null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        }

        ExpensesViewModel vm = ExpensesViewModelTests.CreateViewModel(expenseRepo: repo, savedViewRepo: new FakeSavedViewRepository());
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.FilterText = "pizza";
        await vm.ApplyFilterCommand.ExecuteAsync(null);
        vm.NewViewName = "Pizzas";
        await vm.SaveViewCommand.ExecuteAsync(null);
        await vm.ClearFilterCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.TotalCount);

        vm.SelectedSavedView = Assert.Single(vm.SavedViews);
        await vm.ApplySavedViewCommand.ExecuteAsync(null);

        Assert.Equal("pizza", vm.FilterText);
        Assert.Equal(2, vm.TotalCount);
    }

    [Fact]
    public async Task TheSavedViewDelete_IsPendingOnlyForTheSelectedView_UntilItIsCancelled()
    {
        var views = new FakeSavedViewRepository();
        ExpensesViewModel vm = ExpensesViewModelTests.CreateViewModel(savedViewRepo: views);
        vm.NewViewName = "Una";
        await vm.SaveViewCommand.ExecuteAsync(null);
        for (int i = 0; i < 100 && vm.SavedViews.Count == 0; i++)
        {
            await Task.Delay(10);
        }

        vm.SelectedSavedView = Assert.Single(vm.SavedViews);
        Assert.False(vm.IsSavedViewDeletePending);

        await vm.DeleteSavedViewCommand.ExecuteAsync(null);
        Assert.True(vm.IsSavedViewDeletePending);
        Assert.Single(views.AddedSavedViews);

        vm.CancelSavedViewDeleteCommand.Execute(null);
        Assert.False(vm.IsSavedViewDeletePending);
        Assert.Single(views.AddedSavedViews);
    }
}
