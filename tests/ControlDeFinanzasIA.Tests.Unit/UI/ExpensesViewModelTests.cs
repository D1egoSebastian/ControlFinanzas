using System.ComponentModel;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs;
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
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Domain.Views;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for <see cref="ExpensesViewModel"/>.
/// Uses hand-rolled fakes and the real sealed use cases (DA #1).
/// </summary>
public sealed class ExpensesViewModelTests
{
    private static readonly Guid _validCategoryId = new("11111111-1111-1111-1111-000000000001");

    [Fact]
    public void Constructor_LoadsCategories_SetsDefaultDateFromClock()
    {
        // Arrange
        DateOnly expectedDate = new(2026, 8, 16);
        FakeClock clock = new() { Today = expectedDate };
        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories(
        [
            CreateCategory(Guid.NewGuid(), "Comida"),
            CreateCategory(Guid.NewGuid(), "Transporte")
        ]);

        // Act
        ExpensesViewModel viewModel = CreateViewModel(clock: clock, categoryRepo: categoryRepo);

        // Assert
        Assert.Equal(2, viewModel.Categories.Count);
        Assert.Equal(expectedDate, viewModel.Date);
        Assert.Equal(1, viewModel.Page);
    }

    [Fact]
    public async Task RegisterExpense_ValidForm_CallsUseCase_AndRefreshesList()
    {
        // Arrange
        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories([CreateCategory(_validCategoryId, "Comida")]);
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.SetFilterResult(CreateExpenses(1), 1, 1, 20);

        ExpensesViewModel viewModel = CreateViewModel(expenseRepo: expenseRepo, categoryRepo: categoryRepo);
        viewModel.SelectedCategoryId = _validCategoryId;
        viewModel.Amount = 100m;
        viewModel.Description = "Almuerzo";

        // Act
        await viewModel.RegisterExpenseCommand.ExecuteAsync(null);

        // Assert
        Expense added = Assert.Single(expenseRepo.AddedExpenses);
        Assert.Equal(_validCategoryId, added.CategoryId);
        Assert.Single(viewModel.Expenses);
        Assert.Empty(viewModel.ValidationErrors);
        Assert.Null(viewModel.Amount);
        Assert.Null(viewModel.Description);
        Assert.Null(viewModel.Note);
    }

    [Fact]
    public async Task RegisterExpense_ServerSideValidationFails_ShowsFieldErrors()
    {
        // Arrange
        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories([CreateCategory(_validCategoryId, "Comida")]);
        FakeExpenseRepository expenseRepo = new();

        ExpensesViewModel viewModel = CreateViewModel(expenseRepo: expenseRepo, categoryRepo: categoryRepo);
        viewModel.SelectedCategoryId = _validCategoryId;
        viewModel.Amount = 0m;
        viewModel.Description = "Almuerzo";

        // Act
        await viewModel.RegisterExpenseCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(expenseRepo.AddedExpenses);
        Assert.Contains("Amount", viewModel.ValidationErrors.Keys);
        Assert.Contains("El monto debe ser mayor que 0.", viewModel.ValidationErrors["Amount"]);
        Assert.Null(viewModel.FormError);
    }

    [Fact]
    public async Task RegisterExpense_UseCaseFailsWithNonValidation_SetsFormError()
    {
        // Arrange
        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories([CreateCategory(_validCategoryId, "Comida")]);
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.SetAddResult(Result.Failure("CategoryNotFound", "La categoría no existe."));

        ExpensesViewModel viewModel = CreateViewModel(expenseRepo: expenseRepo, categoryRepo: categoryRepo);
        viewModel.SelectedCategoryId = _validCategoryId;
        viewModel.Amount = 100m;
        viewModel.Description = "Almuerzo";

        // Act
        await viewModel.RegisterExpenseCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("FormErrorMessage", viewModel.FormError);
        Assert.Empty(viewModel.ValidationErrors);
    }

    [Fact]
    public async Task Refresh_PopulatesExpensesAndPageMetadata()
    {
        // Arrange
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.SetFilterResult(CreateExpenses(20), 45, 1, 20);

        ExpensesViewModel viewModel = CreateViewModel(expenseRepo: expenseRepo);

        // Act
        await viewModel.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(20, viewModel.Expenses.Count);
        Assert.Equal(3, viewModel.TotalPages);
    }

    [Fact]
    public async Task NextPage_IncrementsPage_AndRefreshes()
    {
        // Arrange
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.SetFilterResult(CreateExpenses(20), 45, 1, 20);

        ExpensesViewModel viewModel = CreateViewModel(expenseRepo: expenseRepo);
        int callsAfterConstructor = expenseRepo.GetByFilterCallCount;

        // Act
        await viewModel.NextPageCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(2, viewModel.Page);
        Assert.Equal(callsAfterConstructor + 1, expenseRepo.GetByFilterCallCount);
    }

    [Fact]
    public void PreviousPage_AtFirstPage_DoesNotDecrement()
    {
        // Arrange
        ExpensesViewModel viewModel = CreateViewModel();

        // Act & Assert
        Assert.False(viewModel.PreviousPageCommand.CanExecute(null));
        viewModel.PreviousPageCommand.Execute(null);
        Assert.Equal(1, viewModel.Page);
    }

    [Fact]
    public void NextPage_AtLastPage_DoesNotIncrement()
    {
        // Arrange
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.SetFilterResult(CreateExpenses(20), 20, 1, 20);

        ExpensesViewModel viewModel = CreateViewModel(expenseRepo: expenseRepo);

        // Act & Assert
        Assert.False(viewModel.NextPageCommand.CanExecute(null));
        viewModel.NextPageCommand.Execute(null);
        Assert.Equal(1, viewModel.Page);
    }

    [Fact]
    public async Task PreviousPage_OnSecondPage_DecrementsAndRefreshes()
    {
        // Arrange
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.SetFilterResult(CreateExpenses(20), 45, 1, 20);

        ExpensesViewModel viewModel = CreateViewModel(expenseRepo: expenseRepo);
        await viewModel.NextPageCommand.ExecuteAsync(null);
        int callsAfterNext = expenseRepo.GetByFilterCallCount;

        // Act
        await viewModel.PreviousPageCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, viewModel.Page);
        Assert.True(viewModel.PreviousPageCommand.CanExecute(null) == false);
        Assert.Equal(callsAfterNext + 1, expenseRepo.GetByFilterCallCount);
    }

    [Fact]
    public async Task Refresh_OnRepositoryError_ClearsExpensesAndLogs()
    {
        // Arrange
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.SetFilterResult(CreateExpenses(5), 5, 1, 20);

        ExpensesViewModel viewModel = CreateViewModel(expenseRepo: expenseRepo);
        Assert.Equal(5, viewModel.Expenses.Count);

        expenseRepo.SetThrowOnGetFilter(new InvalidOperationException("boom"));

        // Act
        await viewModel.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(viewModel.Expenses);
        Assert.Equal(0, viewModel.TotalPages);
        Assert.Equal("FormErrorMessage", viewModel.FormError);
    }

    [Fact]
    public async Task ApplyFilterCommand_PassesCurrentFilter_ToGetTransactionsQuery()
    {
        // Arrange
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.SetFilterResult(CreateExpenses(5), 5, 1, 20);

        DateOnly from = new(2026, 1, 1);
        DateOnly to = new(2026, 1, 31);

        ExpensesViewModel viewModel = CreateViewModel(expenseRepo: expenseRepo);
        viewModel.Page = 2;
        viewModel.FilterCategoryId = _validCategoryId;
        viewModel.FilterDateFrom = from;
        viewModel.FilterDateTo = to;

        // Act
        await viewModel.ApplyFilterCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, viewModel.Page);
        Assert.NotNull(expenseRepo.LastExpenseFilter);
        Assert.Equal(_validCategoryId, expenseRepo.LastExpenseFilter!.CategoryId);
        Assert.Equal(from, expenseRepo.LastExpenseFilter.DateFrom);
        Assert.Equal(to, expenseRepo.LastExpenseFilter.DateTo);
    }

    [Fact]
    public async Task SaveViewCommand_CallsSaveCurrentFilterUseCase_AndRefreshesSavedViews()
    {
        // Arrange
        FakeSavedViewRepository savedViewRepo = new();
        FakeClock clock = new()
        {
            UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            Today = new DateOnly(2026, 1, 15)
        };

        ExpensesViewModel viewModel = CreateViewModel(savedViewRepo: savedViewRepo, clock: clock);
        viewModel.NewViewName = "Vista de prueba";

        // Act
        await viewModel.SaveViewCommand.ExecuteAsync(null);

        // Assert
        SavedView added = Assert.Single(savedViewRepo.AddedSavedViews);
        Assert.Equal("Vista de prueba", added.Name);
        Assert.Equal(TransactionType.Expense, added.EntityType);
        Assert.Null(viewModel.NewViewName);
        Assert.Single(viewModel.SavedViews);
    }

    [Fact]
    public async Task DeleteViewCommand_CallsDeleteSavedViewUseCase_AndRefreshesSavedViews()
    {
        // Arrange
        FakeSavedViewRepository savedViewRepo = new();
        FakeClock clock = new()
        {
            UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            Today = new DateOnly(2026, 1, 15)
        };

        var view = SavedView.Create(
            "Para eliminar",
            TransactionType.Expense,
            FilterJsonSerializer.Serialize(new ExpenseFilter { Page = 1, PageSize = 20 }),
            clock.UtcNow);
        await savedViewRepo.AddAsync(view);

        ExpensesViewModel viewModel = CreateViewModel(savedViewRepo: savedViewRepo);
        viewModel.SelectedSavedView = view;

        // Act
        await viewModel.DeleteSavedViewCommand.ExecuteAsync(null);
        await viewModel.DeleteSavedViewCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(savedViewRepo.AddedSavedViews);
        Assert.Null(viewModel.SelectedSavedView);
        Assert.Empty(viewModel.SavedViews);
    }

    [Fact]
    public async Task CreateTagCommand_CallsCreateTagUseCase_AndRefreshesAvailableTags()
    {
        // Arrange
        FakeTagRepository tagRepo = new();
        ExpensesViewModel viewModel = CreateViewModel(tagRepo: tagRepo);
        viewModel.NewTagName = "Transporte";
        viewModel.NewTagColor = "#E8A33D";

        // Act
        await viewModel.CreateTagCommand.ExecuteAsync(null);

        // Assert
        Tag added = Assert.Single(tagRepo.AddedTags);
        Assert.Equal("Transporte", added.Name);
        Assert.Equal("#E8A33D", added.Color);
        Assert.Single(viewModel.AvailableTags);
        Assert.Null(viewModel.NewTagName);
        Assert.Null(viewModel.NewTagColor);
    }

    [Fact]
    public async Task SelectedSavedView_Set_LoadsFilterIntoCurrentFilter()
    {
        // Arrange
        FakeSavedViewRepository savedViewRepo = new();
        FakeClock clock = new()
        {
            UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            Today = new DateOnly(2026, 1, 15)
        };

        var view = SavedView.Create(
            "Con categoría",
            TransactionType.Expense,
            FilterJsonSerializer.Serialize(new ExpenseFilter
            {
                Page = 1,
                PageSize = 20,
                CategoryId = _validCategoryId
            }),
            clock.UtcNow);
        await savedViewRepo.AddAsync(view);

        ExpensesViewModel viewModel = CreateViewModel(savedViewRepo: savedViewRepo);
        viewModel.SelectedSavedView = view;

        // Act
        await viewModel.ApplySavedViewCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(_validCategoryId, viewModel.FilterCategoryId);
        Assert.Equal(_validCategoryId, viewModel.CurrentFilter.CategoryId);
        Assert.Equal(1, viewModel.CurrentFilter.Page);
    }

    [Fact]
    public void NewViewName_SetRaisesPropertyChanged()
    {
        // Arrange
        ExpensesViewModel viewModel = CreateViewModel();
        bool raised = false;
        viewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(viewModel.NewViewName))
            {
                raised = true;
            }
        };

        // Act
        viewModel.NewViewName = "Nueva vista";

        // Assert
        Assert.True(raised);
        Assert.Equal("Nueva vista", viewModel.NewViewName);
    }

    private static ExpensesViewModel CreateViewModel(
        FakeClock? clock = null,
        FakeExpenseRepository? expenseRepo = null,
        FakeIncomeRepository? incomeRepo = null,
        FakeCategoryRepository? categoryRepo = null,
        FakeTagRepository? tagRepo = null,
        FakeSavedViewRepository? savedViewRepo = null)
    {
        clock ??= new FakeClock
        {
            UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            Today = new DateOnly(2026, 1, 15)
        };
        expenseRepo ??= new FakeExpenseRepository();
        incomeRepo ??= new FakeIncomeRepository();
        categoryRepo ??= new FakeCategoryRepository();
        tagRepo ??= new FakeTagRepository();
        savedViewRepo ??= new FakeSavedViewRepository();

        RegisterExpenseUseCase registerUseCase = new(
            expenseRepo,
            clock,
            new RegisterExpenseValidator(),
            NullLogger<RegisterExpenseUseCase>.Instance);

        GetTransactionsQuery getTransactionsQuery = new(
            expenseRepo,
            NullLogger<GetTransactionsQuery>.Instance);

        UpdateExpenseUseCase updateExpenseUseCase = new(
            expenseRepo,
            clock,
            new UpdateExpenseValidator(),
            NullLogger<UpdateExpenseUseCase>.Instance);

        DeleteTransactionUseCase deleteTransactionUseCase = new(
            expenseRepo,
            incomeRepo,
            NullLogger<DeleteTransactionUseCase>.Instance);

        SaveCurrentFilterUseCase saveCurrentFilterUseCase = new(
            savedViewRepo,
            clock,
            new SaveCurrentFilterValidator(),
            NullLogger<SaveCurrentFilterUseCase>.Instance);

        ApplySavedViewUseCase applySavedViewUseCase = new(
            savedViewRepo,
            NullLogger<ApplySavedViewUseCase>.Instance);

        GetSavedViewsUseCase getSavedViewsUseCase = new(savedViewRepo);

        DeleteSavedViewUseCase deleteSavedViewUseCase = new(
            savedViewRepo,
            NullLogger<DeleteSavedViewUseCase>.Instance);

        CreateTagUseCase createTagUseCase = new(
            tagRepo,
            clock,
            new CreateTagValidator(),
            NullLogger<CreateTagUseCase>.Instance);

        FakePaymentMethodRepository paymentMethodRepo = new();
        GetPaymentMethodsQuery getPaymentMethodsQuery = new(
            paymentMethodRepo,
            NullLogger<GetPaymentMethodsQuery>.Instance);

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
            clock,
            NullLogger<ExpensesViewModel>.Instance,
            localizer);
    }

    private static Category CreateCategory(Guid id, string name)
        => new() { Id = id, Name = name };

    private static List<Expense> CreateExpenses(int count)
    {
        var expenses = new List<Expense>();
        for (int i = 0; i < count; i++)
        {
            var expense = Expense.Create(
                Guid.NewGuid(),
                new DateOnly(2026, 1, 10 + (i % 20)),
                Money.Create(10m * (i + 1), Currency.DOP),
                $"Gasto {i}",
                null,
                new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
            expenses.Add(expense);
        }

        return expenses;
    }
}
