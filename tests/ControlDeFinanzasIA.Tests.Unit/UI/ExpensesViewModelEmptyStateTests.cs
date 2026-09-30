using System.ComponentModel;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Input;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S5a (R-14, R-20, S-39, S-40, S-41): the empty-state flags and <c>BeginNewEntry</c> of the REAL
/// <see cref="ExpensesViewModel"/> over the real use cases (repositories are the existing doubles).
/// </summary>
public sealed class ExpensesViewModelEmptyStateTests
{
    private static Expense NewExpense(DateOnly date, string description) =>
        Expense.Create(
            Guid.NewGuid(),
            date,
            Money.Create(100m, Currency.DOP),
            description,
            null,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    private static async Task<(ExpensesViewModel Vm, FakeExpenseRepository Repository)> Create(int expenses)
    {
        var repository = new FakeExpenseRepository { PageAddedItems = true };
        for (int i = 0; i < expenses; i++)
        {
            await repository.AddAsync(NewExpense(new DateOnly(2026, 1, 10 + i), $"Gasto {i}"));
        }

        ExpensesViewModel vm = ExpensesViewModelTests.CreateViewModel(expenseRepo: repository);
        await WaitUntil(() => vm.HasLoaded);
        return (vm, repository);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task AfterTheFirstSuccessfulLoad_AnEmptyDatabase_IsNoData_AndNotFiltered()
    {
        (ExpensesViewModel vm, _) = await Create(0);

        Assert.True(vm.HasLoaded);
        Assert.True(vm.IsEmptyNoData);
        Assert.False(vm.IsEmptyFiltered);
        Assert.False(vm.HasActiveFilter);
    }

    [Fact]
    public async Task WithData_NeitherEmptyFlagIsSet()
    {
        (ExpensesViewModel vm, _) = await Create(2);

        Assert.True(vm.HasLoaded);
        Assert.False(vm.IsEmptyNoData);
        Assert.False(vm.IsEmptyFiltered);
    }

    [Fact]
    public async Task AFailedFirstLoad_NeverReportsLoaded_SoNoEmptyStateAppears()
    {
        var repository = new FakeExpenseRepository();
        repository.SetThrowOnGetFilter(new InvalidOperationException("boom"));

        ExpensesViewModel vm = ExpensesViewModelTests.CreateViewModel(expenseRepo: repository);
        await Task.Delay(150);

        Assert.False(vm.HasLoaded);
        Assert.False(vm.IsEmptyNoData);
        Assert.False(vm.IsEmptyFiltered);
    }

    [Fact]
    public async Task HasActiveFilter_IsTrueOnlyWhenTheAppliedFilterDiffersFromTheDefault()
    {
        (ExpensesViewModel vm, _) = await Create(2);
        Assert.False(vm.HasActiveFilter);

        // Editing the rail without applying changes nothing.
        vm.FilterDateFrom = new DateOnly(2026, 3, 1);
        Assert.False(vm.HasActiveFilter);

        vm.FilterDateTo = new DateOnly(2026, 3, 31);
        await vm.ApplyFilterCommand.ExecuteAsync(null);
        Assert.True(vm.HasActiveFilter);
        Assert.True(vm.IsEmptyFiltered);
        Assert.False(vm.IsEmptyNoData);

        await vm.ClearFilterCommand.ExecuteAsync(null);
        Assert.False(vm.HasActiveFilter);
        Assert.False(vm.IsEmptyFiltered);
        Assert.Equal(2, vm.Expenses.Count);
    }

    [Fact]
    public async Task AFilterThatStillMatchesRows_IsActive_ButNotEmpty()
    {
        (ExpensesViewModel vm, _) = await Create(2);

        vm.FilterDateFrom = new DateOnly(2026, 1, 10);
        vm.FilterDateTo = new DateOnly(2026, 1, 10);
        await vm.ApplyFilterCommand.ExecuteAsync(null);

        Assert.True(vm.HasActiveFilter);
        Assert.False(vm.IsEmptyFiltered);
        Assert.Single(vm.Expenses);
    }

    [Fact]
    public async Task BeginNewEntry_CancelsAnEditInProgress_AndClearsTheForm()
    {
        (ExpensesViewModel vm, _) = await Create(1);
        ExpenseDto row = vm.Expenses.Single();
        vm.BeginEditCommand.Execute(row);
        Assert.True(vm.IsEditMode);
        Assert.Equal(row.Description, vm.Description);

        vm.BeginNewEntry();

        Assert.False(vm.IsEditMode);
        Assert.Null(vm.EditableId);
        Assert.Null(vm.Description);
        Assert.Null(vm.Amount);
    }

    [Fact]
    public async Task BeginNewEntry_ProducesANewFocusTokenEachCall_AndRaisesTheChangeNotification()
    {
        (ExpensesViewModel vm, _) = await Create(0);
        var raised = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.BeginNewEntry();
        FocusRequestToken? first = vm.DescriptionFocusRequest;
        vm.BeginNewEntry();
        FocusRequestToken? second = vm.DescriptionFocusRequest;

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.Equal(2, raised.Count(n => n == nameof(ExpensesViewModel.DescriptionFocusRequest)));
    }

    [Fact]
    public async Task TheBeginNewEntryCommand_RunsBeginNewEntry()
    {
        (ExpensesViewModel vm, _) = await Create(0);

        Assert.True(vm.BeginNewEntryCommand.CanExecute(null));
        vm.BeginNewEntryCommand.Execute(null);

        Assert.NotNull(vm.DescriptionFocusRequest);
    }
}
