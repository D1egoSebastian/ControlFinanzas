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
/// 1.0 blockers D1, D2 and D3 (QA audit) for the Income list: paging, delete with confirmation and filters, driven
/// through the REAL view-model and use cases over an in-memory repository that really pages and filters.
/// </summary>
public sealed class IncomeViewModelLedgerTests
{
    private static readonly Guid _salary = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _other = new("11111111-1111-1111-1111-000000000002");

    private static Income NewIncome(Guid? category, DateOnly date, string description) =>
        Income.Create(category, date, Money.Create(100m, Currency.DOP), description, null, null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    private static async Task<(IncomeViewModel Vm, FakeIncomeRepository Repo)> LoadedAsync(params Income[] incomes)
    {
        var repo = new FakeIncomeRepository { PageAddedItems = true };
        foreach (Income income in incomes)
        {
            await repo.AddAsync(income);
        }

        IncomeViewModel vm = IncomeViewModelTests.CreateViewModel(incomeRepo: repo);
        await vm.RefreshCommand.ExecuteAsync(null);
        return (vm, repo);
    }

    private static Task<(IncomeViewModel Vm, FakeIncomeRepository Repo)> LoadedManyAsync(int count) =>
        LoadedAsync([.. Enumerable.Range(0, count).Select(i => NewIncome(_salary, new DateOnly(2026, 1, 1).AddDays(i), $"Ingreso {i}"))]);

    [Fact]
    public async Task AfterTheFirstLoad_TheTotalCountIsTheNumberOfRecords_NotThePageSize()
    {
        (IncomeViewModel vm, _) = await LoadedManyAsync(45);

        Assert.Equal(20, vm.Incomes.Count);
        Assert.Equal(45, vm.TotalCount);
        Assert.Equal(3, vm.TotalPages);
    }

    [Fact]
    public async Task ThePageSummary_RaisesAChangeNotification_WhenThePageMoves()
    {
        (IncomeViewModel vm, _) = await LoadedManyAsync(45);
        var raised = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        await vm.NextPageCommand.ExecuteAsync(null);

        Assert.Contains(nameof(IncomeViewModel.PageSummary), raised);
        Assert.Equal(2, vm.Page);
    }

    [Fact]
    public async Task AnOldRecord_IsReachableOnALaterPage()
    {
        (IncomeViewModel vm, _) = await LoadedManyAsync(45);
        Assert.DoesNotContain(vm.Incomes, i => i.Description == "Ingreso 0");

        await vm.NextPageCommand.ExecuteAsync(null);
        await vm.NextPageCommand.ExecuteAsync(null);

        Assert.Contains(vm.Incomes, i => i.Description == "Ingreso 0");
    }

    [Fact]
    public async Task ConfirmingTheDelete_RemovesTheRecord_AndTheTotalDecrements_WhileTheFirstStepOnlyArms()
    {
        (IncomeViewModel vm, FakeIncomeRepository repo) = await LoadedManyAsync(3);
        IncomeDto row = vm.Incomes[0];

        await vm.DeleteIncomeCommand.ExecuteAsync(row);
        Assert.Equal(row.Id, vm.PendingDeleteIncomeId);
        Assert.Equal(3, repo.AddedIncomes.Count);

        await vm.DeleteIncomeCommand.ExecuteAsync(row);

        Assert.Null(vm.PendingDeleteIncomeId);
        Assert.Equal(2, repo.AddedIncomes.Count);
        Assert.Equal(2, vm.TotalCount);
        Assert.DoesNotContain(vm.Incomes, i => i.Id == row.Id);
    }

    [Fact]
    public async Task CancellingTheDelete_DisarmsTheRow_AndKeepsTheRecord()
    {
        (IncomeViewModel vm, FakeIncomeRepository repo) = await LoadedManyAsync(3);
        IncomeDto row = vm.Incomes[0];
        await vm.DeleteIncomeCommand.ExecuteAsync(row);

        vm.CancelDeleteCommand.Execute(null);

        Assert.Null(vm.PendingDeleteIncomeId);
        Assert.Equal(3, repo.AddedIncomes.Count);
        Assert.Equal(3, vm.TotalCount);
    }

    [Fact]
    public async Task DeletingTheLastRowOfTheLastPage_StepsBackToTheNewLastPage()
    {
        (IncomeViewModel vm, _) = await LoadedManyAsync(21);
        await vm.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Page);
        IncomeDto only = Assert.Single(vm.Incomes);

        await vm.DeleteIncomeCommand.ExecuteAsync(only);
        await vm.DeleteIncomeCommand.ExecuteAsync(only);

        Assert.Equal(1, vm.Page);
        Assert.Equal(1, vm.TotalPages);
        Assert.Equal(20, vm.Incomes.Count);
        Assert.Equal(20, vm.TotalCount);
        Assert.False(vm.IsEmptyNoData);
    }

    [Fact]
    public async Task DeletingTheOnlyRecord_LeavesPageOne_AndShowsTheNoDataState()
    {
        (IncomeViewModel vm, _) = await LoadedManyAsync(1);
        IncomeDto only = Assert.Single(vm.Incomes);

        await vm.DeleteIncomeCommand.ExecuteAsync(only);
        await vm.DeleteIncomeCommand.ExecuteAsync(only);

        Assert.Equal(1, vm.Page);
        Assert.Equal(0, vm.TotalCount);
        Assert.True(vm.IsEmptyNoData);
    }

    private static Task<(IncomeViewModel Vm, FakeIncomeRepository Repo)> LoadedMixedAsync() =>
        LoadedAsync(
            NewIncome(_salary, new DateOnly(2026, 1, 5), "Salario enero"),
            NewIncome(_salary, new DateOnly(2026, 2, 5), "Salario febrero"),
            NewIncome(_other, new DateOnly(2026, 2, 20), "Bono"),
            NewIncome(_other, new DateOnly(2026, 3, 1), "Venta de salario atrasado"));

    [Fact]
    public async Task TheTextFilter_NarrowsTheList_AndCountsAsAnActiveFilter()
    {
        (IncomeViewModel vm, _) = await LoadedMixedAsync();
        Assert.Equal(4, vm.TotalCount);

        vm.FilterText = "salario";
        await vm.ApplyFilterCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.TotalCount);
        Assert.True(vm.HasActiveFilter);
    }

    [Fact]
    public async Task TheCategoryTextAndDateRange_NarrowTheListTogether()
    {
        (IncomeViewModel vm, _) = await LoadedMixedAsync();

        vm.FilterCategoryId = _other;
        await vm.ApplyFilterCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.TotalCount);

        vm.FilterText = "salario";
        await vm.ApplyFilterCommand.ExecuteAsync(null);
        Assert.Equal("Venta de salario atrasado", Assert.Single(vm.Incomes).Description);

        vm.FilterDateTo = new DateOnly(2026, 2, 28);
        await vm.ApplyFilterCommand.ExecuteAsync(null);
        Assert.Equal(0, vm.TotalCount);
        Assert.True(vm.IsEmptyFiltered);
        Assert.False(vm.IsEmptyNoData);
    }

    [Fact]
    public async Task ClearFilter_ResetsTheTextAndBringsEveryRecordBack()
    {
        (IncomeViewModel vm, _) = await LoadedMixedAsync();
        vm.FilterText = "bono";
        await vm.ApplyFilterCommand.ExecuteAsync(null);
        Assert.Equal(1, vm.TotalCount);

        await vm.ClearFilterCommand.ExecuteAsync(null);

        Assert.Null(vm.FilterText);
        Assert.False(vm.HasActiveFilter);
        Assert.Equal(4, vm.TotalCount);
    }

    [Fact]
    public async Task AnInvertedDateRange_IsReportedAsAFilterError_AndTheListIsLeftAlone()
    {
        (IncomeViewModel vm, _) = await LoadedMixedAsync();
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
        (IncomeViewModel vm, _) = await LoadedManyAsync(45);
        await vm.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Page);

        vm.FilterText = "Ingreso 4";
        await vm.ApplyFilterCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.Page);
        Assert.Equal(6, vm.TotalCount);
    }
}
