using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Budgets.CreateOrUpdateBudget;
using ControlDeFinanzasIA.Application.UseCases.Budgets.DeleteBudget;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetsForMonth;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for <see cref="BudgetsViewModel"/> (F-010, slice 3): load
/// populates categories and budgets (R-1), save dispatches the upsert use
/// case with the current form values, a failed save/delete surfaces into
/// <see cref="BudgetsViewModel.FormError"/> without clearing the form,
/// selecting a row hydrates the form (S-1), and month navigation reloads
/// for the newly navigated month (S-1, S-10).
/// </summary>
public sealed class BudgetsViewModelTests
{
    private static readonly DateOnly _march1 = new(2026, 3, 1);
    private static readonly Guid _comidaId = Guid.NewGuid();

    [Fact]
    public async Task Constructor_LoadsCurrentMonth_PopulatesCategoriesAndBudgets()
    {
        var budgets = new FakeBudgetRepository();
        budgets.Seed(Budget.Create(2026, 3, _comidaId, Money.Create(15000m, Currency.DOP)));

        BudgetsViewModel viewModel = CreateViewModel(budgets: budgets);
        await Task.Delay(1); // allow the constructor's fire-and-forget LoadAsync to complete

        Assert.Equal(MonthPeriod.Anchor(_march1), viewModel.SelectedMonth);
        Assert.Single(viewModel.Categories);
        Assert.Single(viewModel.Budgets);
        Assert.Equal("Comida", viewModel.Budgets[0].CategoryName);
        Assert.Equal(15000m, viewModel.Budgets[0].Amount);
    }

    [Fact]
    public async Task SaveCommand_ValidForm_CallsUpsertUseCaseAndAddsBudgetToList()
    {
        BudgetsViewModel viewModel = CreateViewModel();
        await Task.Delay(1);
        viewModel.SelectedCategoryId = _comidaId;
        viewModel.Amount = 12000m;
        viewModel.Currency = Currency.DOP;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Budgets);
        Assert.Equal(12000m, viewModel.Budgets[0].Amount);
        Assert.Equal(string.Empty, viewModel.FormError);
        Assert.NotEqual(string.Empty, viewModel.StatusMessage);

        // The form is reset after a successful save.
        Assert.Null(viewModel.SelectedCategoryId);
        Assert.Equal(0m, viewModel.Amount);
    }

    [Fact]
    public async Task SaveCommand_InvalidForm_SetsFormErrorAndDoesNotAddBudget()
    {
        BudgetsViewModel viewModel = CreateViewModel();
        await Task.Delay(1);
        viewModel.SelectedCategoryId = null; // invalid: global budgets rejected (R-8, S-9)
        viewModel.Amount = 12000m;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Budgets);
        Assert.NotEqual(string.Empty, viewModel.FormError);

        // The form is NOT cleared on failure.
        Assert.Equal(12000m, viewModel.Amount);
    }

    [Fact]
    public async Task EditCommand_SelectingRow_HydratesFormFields()
    {
        var budgets = new FakeBudgetRepository();
        budgets.Seed(Budget.Create(2026, 3, _comidaId, Money.Create(9000m, Currency.USD)));
        BudgetsViewModel viewModel = CreateViewModel(budgets: budgets);
        await Task.Delay(1);
        BudgetRow row = viewModel.Budgets[0];

        viewModel.EditCommand.Execute(row);

        Assert.Equal(row, viewModel.SelectedBudget);
        Assert.Equal(_comidaId, viewModel.SelectedCategoryId);
        Assert.Equal(9000m, viewModel.Amount);
        Assert.Equal(Currency.USD, viewModel.Currency);
    }

    [Fact]
    public async Task DeleteCommand_RemovesBudgetFromList()
    {
        var budgets = new FakeBudgetRepository();
        budgets.Seed(Budget.Create(2026, 3, _comidaId, Money.Create(9000m, Currency.DOP)));
        BudgetsViewModel viewModel = CreateViewModel(budgets: budgets);
        await Task.Delay(1);
        BudgetRow row = viewModel.Budgets[0];

        await viewModel.DeleteCommand.ExecuteAsync(row);

        Assert.Empty(viewModel.Budgets);
        Assert.NotEqual(string.Empty, viewModel.StatusMessage);
    }

    [Fact]
    public async Task PreviousMonthCommand_ReloadsBudgetsForThePreviousMonth()
    {
        var budgets = new FakeBudgetRepository();
        budgets.Seed(Budget.Create(2026, 2, _comidaId, Money.Create(5000m, Currency.DOP)));
        BudgetsViewModel viewModel = CreateViewModel(budgets: budgets);
        await Task.Delay(1);
        Assert.Empty(viewModel.Budgets); // no budget for March yet

        await viewModel.PreviousMonthCommand.ExecuteAsync(null);

        Assert.Equal(MonthPeriod.Previous(MonthPeriod.Anchor(_march1)), viewModel.SelectedMonth);
        Assert.Single(viewModel.Budgets);
        Assert.Equal(5000m, viewModel.Budgets[0].Amount);
    }

    [Fact]
    public async Task NextMonthCommand_ReloadsBudgetsForTheNextMonth()
    {
        var budgets = new FakeBudgetRepository();
        budgets.Seed(Budget.Create(2026, 4, _comidaId, Money.Create(7000m, Currency.DOP)));
        BudgetsViewModel viewModel = CreateViewModel(budgets: budgets);
        await Task.Delay(1);
        Assert.Empty(viewModel.Budgets); // no budget for March yet

        await viewModel.NextMonthCommand.ExecuteAsync(null);

        Assert.Equal(MonthPeriod.Next(MonthPeriod.Anchor(_march1)), viewModel.SelectedMonth);
        Assert.Single(viewModel.Budgets);
        Assert.Equal(7000m, viewModel.Budgets[0].Amount);
    }

    private static BudgetsViewModel CreateViewModel(
        FakeBudgetRepository? budgets = null,
        FakeCategoryRepository? categories = null,
        FakeClock? clock = null)
    {
        budgets ??= new FakeBudgetRepository();
        categories ??= CreateCategoryRepository();
        clock ??= new FakeClock { Today = _march1 };

        var getBudgetsForMonthQuery = new GetBudgetsForMonthQuery(budgets);
        var createOrUpdateUseCase = new CreateOrUpdateBudgetUseCase(
            budgets, categories, new CreateOrUpdateBudgetValidator(), NullLogger<CreateOrUpdateBudgetUseCase>.Instance);
        var deleteUseCase = new DeleteBudgetUseCase(budgets);

        return new BudgetsViewModel(
            getBudgetsForMonthQuery,
            createOrUpdateUseCase,
            deleteUseCase,
            categories,
            clock,
            NullLogger<BudgetsViewModel>.Instance);
    }

    private static FakeCategoryRepository CreateCategoryRepository()
    {
        var repository = new FakeCategoryRepository();
        repository.SetCategories(new[]
        {
            new Category { Id = _comidaId, Name = "Comida", IsSystem = false },
        });
        return repository;
    }
}
