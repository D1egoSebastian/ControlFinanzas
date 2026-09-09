using System.ComponentModel;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Application.UseCases.Catalog.CreateTag;
using ControlDeFinanzasIA.Application.UseCases.Catalog.GetPaymentMethods;
using ControlDeFinanzasIA.Application.UseCases.Incomes.GetIncomes;
using ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome;
using ControlDeFinanzasIA.Application.UseCases.Incomes.UpdateIncome;
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
/// Unit tests for <see cref="IncomeViewModel"/>.
/// Uses hand-rolled fakes and the real sealed use cases/query (DA #1).
/// </summary>
public sealed class IncomeViewModelTests
{
    private static readonly Guid _validCategoryId = new("11111111-1111-1111-1111-000000000002");

    [Fact]
    public void Constructor_LoadsCategories_SetsDefaultDateFromClock()
    {
        // Arrange
        DateOnly expectedDate = new(2026, 8, 16);
        FakeClock clock = new() { Today = expectedDate };
        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories(
        [
            CreateCategory(Guid.NewGuid(), "Salario"),
            CreateCategory(Guid.NewGuid(), "Inversiones")
        ]);

        // Act
        IncomeViewModel viewModel = CreateViewModel(clock: clock, categoryRepo: categoryRepo);

        // Assert
        Assert.Equal(2, viewModel.Categories.Count);
        Assert.Equal(expectedDate, viewModel.Date);
        Assert.Equal(1, viewModel.Page);
    }

    [Fact]
    public async Task RegisterIncome_ValidForm_CallsUseCase_AndRefreshesList()
    {
        // Arrange
        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories([CreateCategory(_validCategoryId, "Salario")]);
        FakeIncomeRepository incomeRepo = new();
        incomeRepo.SetFilterResult(CreateIncomes(1), 1, 1, 20);

        IncomeViewModel viewModel = CreateViewModel(incomeRepo: incomeRepo, categoryRepo: categoryRepo);
        viewModel.SelectedCategoryId = _validCategoryId;
        viewModel.Amount = 100m;
        viewModel.Description = "Sueldo";

        // Act
        await viewModel.RegisterIncomeCommand.ExecuteAsync(null);

        // Assert
        Income added = Assert.Single(incomeRepo.AddedIncomes);
        Assert.Equal(_validCategoryId, added.CategoryId);
        Assert.Single(viewModel.Incomes);
        Assert.Empty(viewModel.ValidationErrors);
        Assert.Null(viewModel.Amount);
        Assert.Null(viewModel.Description);
        Assert.Null(viewModel.Note);
    }

    [Fact]
    public async Task RegisterIncome_ServerSideValidationFails_ShowsFieldErrors()
    {
        // Arrange
        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories([CreateCategory(_validCategoryId, "Salario")]);
        FakeIncomeRepository incomeRepo = new();

        IncomeViewModel viewModel = CreateViewModel(incomeRepo: incomeRepo, categoryRepo: categoryRepo);
        viewModel.SelectedCategoryId = _validCategoryId;
        viewModel.Amount = 0m;
        viewModel.Description = "Sueldo";

        // Act
        await viewModel.RegisterIncomeCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(incomeRepo.AddedIncomes);
        Assert.Contains("Amount", viewModel.ValidationErrors.Keys);
        Assert.Contains("El monto debe ser mayor que 0.", viewModel.ValidationErrors["Amount"]);
        Assert.Null(viewModel.FormError);
    }

    [Fact]
    public async Task RegisterIncome_UseCaseFailsWithNonValidation_SetsFormError()
    {
        // Arrange
        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories([CreateCategory(_validCategoryId, "Salario")]);
        FakeIncomeRepository incomeRepo = new();
        incomeRepo.SetAddResult(Result.Failure("CategoryNotFound", "La categoría no existe."));

        IncomeViewModel viewModel = CreateViewModel(incomeRepo: incomeRepo, categoryRepo: categoryRepo);
        viewModel.SelectedCategoryId = _validCategoryId;
        viewModel.Amount = 100m;
        viewModel.Description = "Sueldo";

        // Act
        await viewModel.RegisterIncomeCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("FormErrorMessage", viewModel.FormError);
        Assert.Empty(viewModel.ValidationErrors);
    }

    [Fact]
    public async Task Refresh_PopulatesIncomesAndPageMetadata()
    {
        // Arrange
        FakeIncomeRepository incomeRepo = new();
        incomeRepo.SetFilterResult(CreateIncomes(20), 45, 1, 20);

        IncomeViewModel viewModel = CreateViewModel(incomeRepo: incomeRepo);

        // Act
        await viewModel.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(20, viewModel.Incomes.Count);
        Assert.Equal(3, viewModel.TotalPages);
    }

    [Fact]
    public async Task NextPage_IncrementsPage_AndRefreshes()
    {
        // Arrange
        FakeIncomeRepository incomeRepo = new();
        incomeRepo.SetFilterResult(CreateIncomes(20), 45, 1, 20);

        IncomeViewModel viewModel = CreateViewModel(incomeRepo: incomeRepo);
        int callsAfterConstructor = incomeRepo.GetByFilterCallCount;

        // Act
        await viewModel.NextPageCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(2, viewModel.Page);
        Assert.Equal(callsAfterConstructor + 1, incomeRepo.GetByFilterCallCount);
    }

    [Fact]
    public void PreviousPage_AtFirstPage_DoesNotDecrement()
    {
        // Arrange
        IncomeViewModel viewModel = CreateViewModel();

        // Act & Assert
        Assert.False(viewModel.PreviousPageCommand.CanExecute(null));
        viewModel.PreviousPageCommand.Execute(null);
        Assert.Equal(1, viewModel.Page);
    }

    [Fact]
    public void NextPage_AtLastPage_DoesNotIncrement()
    {
        // Arrange
        FakeIncomeRepository incomeRepo = new();
        incomeRepo.SetFilterResult(CreateIncomes(20), 20, 1, 20);

        IncomeViewModel viewModel = CreateViewModel(incomeRepo: incomeRepo);

        // Act & Assert
        Assert.False(viewModel.NextPageCommand.CanExecute(null));
        viewModel.NextPageCommand.Execute(null);
        Assert.Equal(1, viewModel.Page);
    }

    [Fact]
    public async Task PreviousPage_OnSecondPage_DecrementsAndRefreshes()
    {
        // Arrange
        FakeIncomeRepository incomeRepo = new();
        incomeRepo.SetFilterResult(CreateIncomes(20), 45, 1, 20);

        IncomeViewModel viewModel = CreateViewModel(incomeRepo: incomeRepo);
        await viewModel.NextPageCommand.ExecuteAsync(null);
        int callsAfterNext = incomeRepo.GetByFilterCallCount;

        // Act
        await viewModel.PreviousPageCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, viewModel.Page);
        Assert.True(viewModel.PreviousPageCommand.CanExecute(null) == false);
        Assert.Equal(callsAfterNext + 1, incomeRepo.GetByFilterCallCount);
    }

    [Fact]
    public async Task Refresh_OnRepositoryError_ClearsIncomesAndLogs()
    {
        // Arrange
        FakeIncomeRepository incomeRepo = new();
        incomeRepo.SetFilterResult(CreateIncomes(5), 5, 1, 20);

        IncomeViewModel viewModel = CreateViewModel(incomeRepo: incomeRepo);
        Assert.Equal(5, viewModel.Incomes.Count);

        incomeRepo.SetThrowOnGetFilter(new InvalidOperationException("boom"));

        // Act
        await viewModel.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(viewModel.Incomes);
        Assert.Equal(0, viewModel.TotalPages);
        Assert.Equal("FormErrorMessage", viewModel.FormError);
    }

    [Fact]
    public async Task ApplyFilterCommand_PassesCurrentFilter_ToGetIncomesQuery()
    {
        // Arrange
        FakeIncomeRepository incomeRepo = new();
        incomeRepo.SetFilterResult(CreateIncomes(5), 5, 1, 20);

        DateOnly from = new(2026, 1, 1);
        DateOnly to = new(2026, 1, 31);

        IncomeViewModel viewModel = CreateViewModel(incomeRepo: incomeRepo);
        viewModel.Page = 2;
        viewModel.FilterCategoryId = _validCategoryId;
        viewModel.FilterDateFrom = from;
        viewModel.FilterDateTo = to;

        // Act
        await viewModel.ApplyFilterCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, viewModel.Page);
        Assert.NotNull(incomeRepo.LastIncomeFilter);
        Assert.Equal(_validCategoryId, incomeRepo.LastIncomeFilter!.CategoryId);
        Assert.Equal(from, incomeRepo.LastIncomeFilter.DateFrom);
        Assert.Equal(to, incomeRepo.LastIncomeFilter.DateTo);
    }

    [Fact]
    public async Task ApplyFilterCommand_WithSelectedTags_PassesTagIds()
    {
        // Arrange
        FakeIncomeRepository incomeRepo = new();
        incomeRepo.SetFilterResult(CreateIncomes(5), 5, 1, 20);

        FakeTagRepository tagRepo = new();
        var tag = Tag.Create("Inversión", "#7FB069", new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        await tagRepo.AddAsync(tag);

        IncomeViewModel viewModel = CreateViewModel(incomeRepo: incomeRepo, tagRepo: tagRepo);
        viewModel.AvailableTags[0].IsSelected = true;

        // Act
        await viewModel.ApplyFilterCommand.ExecuteAsync(null);

        // Assert
        Assert.NotNull(incomeRepo.LastIncomeFilter);
        Assert.NotNull(incomeRepo.LastIncomeFilter!.TagIds);
        Assert.Contains(tag.Id, incomeRepo.LastIncomeFilter.TagIds!);
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

        IncomeViewModel viewModel = CreateViewModel(savedViewRepo: savedViewRepo, clock: clock);
        viewModel.NewViewName = "Vista de prueba";

        // Act
        await viewModel.SaveViewCommand.ExecuteAsync(null);

        // Assert
        SavedView added = Assert.Single(savedViewRepo.AddedSavedViews);
        Assert.Equal("Vista de prueba", added.Name);
        Assert.Equal(TransactionType.Income, added.EntityType);
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
            TransactionType.Income,
            FilterJsonSerializer.Serialize(new IncomeFilter { Page = 1, PageSize = 20 }),
            clock.UtcNow);
        await savedViewRepo.AddAsync(view);

        IncomeViewModel viewModel = CreateViewModel(savedViewRepo: savedViewRepo);
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
        IncomeViewModel viewModel = CreateViewModel(tagRepo: tagRepo);
        viewModel.NewTagName = "Inversión";
        viewModel.NewTagColor = "#7FB069";

        // Act
        await viewModel.CreateTagCommand.ExecuteAsync(null);

        // Assert
        Tag added = Assert.Single(tagRepo.AddedTags);
        Assert.Equal("Inversión", added.Name);
        Assert.Equal("#7FB069", added.Color);
        Assert.Single(viewModel.AvailableTags);
        Assert.Null(viewModel.NewTagName);
        Assert.Null(viewModel.NewTagColor);
    }

    [Fact]
    public async Task CreateTagCommand_WithEmptyName_SetsTagCreationError()
    {
        // Arrange
        FakeTagRepository tagRepo = new();
        IncomeViewModel viewModel = CreateViewModel(tagRepo: tagRepo);
        viewModel.NewTagName = "   ";

        // Act
        await viewModel.CreateTagCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(tagRepo.AddedTags);
        Assert.Equal("TagNameRequiredMessage", viewModel.TagCreationError);
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
            TransactionType.Income,
            FilterJsonSerializer.Serialize(new IncomeFilter
            {
                Page = 1,
                PageSize = 20,
                CategoryId = _validCategoryId
            }),
            clock.UtcNow);
        await savedViewRepo.AddAsync(view);

        IncomeViewModel viewModel = CreateViewModel(savedViewRepo: savedViewRepo);
        viewModel.SelectedSavedView = view;

        // Act
        await viewModel.ApplySavedViewCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(_validCategoryId, viewModel.FilterCategoryId);
        Assert.Equal(_validCategoryId, viewModel.CurrentFilter.CategoryId);
        Assert.Equal(1, viewModel.CurrentFilter.Page);
    }

    [Fact]
    public void BeginEditCommand_LoadsDtoIntoForm_AndSetsEditMode()
    {
        // Arrange
        IncomeViewModel viewModel = CreateViewModel();
        IncomeDto dto = CreateIncomeDto();

        // Act
        viewModel.BeginEditCommand.Execute(dto);

        // Assert
        Assert.True(viewModel.IsEditMode);
        Assert.Equal(dto.Id, viewModel.EditableId);
        Assert.Equal(dto.Date, viewModel.Date);
        Assert.Equal(dto.Amount, viewModel.Amount);
        Assert.Equal(dto.CategoryId, viewModel.SelectedCategoryId);
        Assert.Equal(dto.Description, viewModel.Description);
    }

    [Fact]
    public async Task UpdateIncomeCommand_WithValidForm_UpdatesAndRefreshes()
    {
        // Arrange
        FakeIncomeRepository incomeRepo = new();
        Income income = CreateSingleIncome();
        await incomeRepo.AddAsync(income);
        incomeRepo.SetFilterResult(new List<Income> { income }, 1, 1, 20);

        IncomeViewModel viewModel = CreateViewModel(incomeRepo: incomeRepo);
        IncomeDto dto = income.ToDto();
        viewModel.BeginEditCommand.Execute(dto);
        viewModel.Description = "Sueldo actualizado";

        // Act
        await viewModel.UpdateIncomeCommand.ExecuteAsync(null);

        // Assert
        Assert.False(viewModel.IsEditMode);
        Assert.Null(viewModel.EditableId);
        Assert.Equal("IncomeUpdatedMessage", viewModel.StatusMessage);
    }

    [Fact]
    public async Task DeleteIncomeCommand_TwoStepConfirm_DeletesOnSecondInvocation()
    {
        // Arrange
        FakeIncomeRepository incomeRepo = new();
        Income income = CreateSingleIncome();
        await incomeRepo.AddAsync(income);
        incomeRepo.SetFilterResult(new List<Income> { income }, 1, 1, 20);

        IncomeViewModel viewModel = CreateViewModel(incomeRepo: incomeRepo);
        await viewModel.RefreshCommand.ExecuteAsync(null);
        IncomeDto dto = viewModel.Incomes[0];

        // Act
        await viewModel.DeleteIncomeCommand.ExecuteAsync(dto);

        // Assert: first invocation only arms pending state
        Assert.Equal(dto.Id, viewModel.PendingDeleteIncomeId);
        Assert.Single(incomeRepo.AddedIncomes);

        // Act
        await viewModel.DeleteIncomeCommand.ExecuteAsync(dto);

        // Assert: second invocation deletes
        Assert.Null(viewModel.PendingDeleteIncomeId);
        Assert.Empty(incomeRepo.AddedIncomes);
        Assert.Equal("TransactionDeletedMessage", viewModel.StatusMessage);
    }

    [Fact]
    public async Task DeleteIncomeCommand_OnRepositoryError_SetsFormError()
    {
        // Arrange
        FakeIncomeRepository incomeRepo = new();
        incomeRepo.SetFilterResult(CreateIncomes(1), 1, 1, 20);
        incomeRepo.RemoveResultOverride = Result.Failure("NotFound", "Income not found.");

        IncomeViewModel viewModel = CreateViewModel(incomeRepo: incomeRepo);
        await viewModel.RefreshCommand.ExecuteAsync(null);
        IncomeDto dto = viewModel.Incomes[0];

        // Act
        await viewModel.DeleteIncomeCommand.ExecuteAsync(dto);
        await viewModel.DeleteIncomeCommand.ExecuteAsync(dto);

        // Assert
        Assert.Equal("FormErrorMessage", viewModel.FormError);
        Assert.Null(viewModel.PendingDeleteIncomeId);
    }

    [Fact]
    public void NewViewName_SetRaisesPropertyChanged()
    {
        // Arrange
        IncomeViewModel viewModel = CreateViewModel();
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

    private static IncomeViewModel CreateViewModel(
        FakeClock? clock = null,
        FakeIncomeRepository? incomeRepo = null,
        FakeExpenseRepository? expenseRepo = null,
        FakeCategoryRepository? categoryRepo = null,
        FakeTagRepository? tagRepo = null,
        FakeSavedViewRepository? savedViewRepo = null)
    {
        clock ??= new FakeClock
        {
            UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            Today = new DateOnly(2026, 1, 15)
        };
        incomeRepo ??= new FakeIncomeRepository();
        expenseRepo ??= new FakeExpenseRepository();
        categoryRepo ??= new FakeCategoryRepository();
        tagRepo ??= new FakeTagRepository();
        savedViewRepo ??= new FakeSavedViewRepository();

        RegisterIncomeUseCase registerUseCase = new(
            incomeRepo,
            clock,
            new RegisterIncomeValidator(),
            NullLogger<RegisterIncomeUseCase>.Instance);

        GetIncomesQuery getIncomesQuery = new(
            incomeRepo,
            NullLogger<GetIncomesQuery>.Instance);

        UpdateIncomeUseCase updateIncomeUseCase = new(
            incomeRepo,
            clock,
            new UpdateIncomeValidator(),
            NullLogger<UpdateIncomeUseCase>.Instance);

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

        return new IncomeViewModel(
            registerUseCase,
            getIncomesQuery,
            updateIncomeUseCase,
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
            NullLogger<IncomeViewModel>.Instance,
            localizer);
    }

    private static Category CreateCategory(Guid id, string name)
        => new() { Id = id, Name = name };

    private static List<Income> CreateIncomes(int count)
    {
        var incomes = new List<Income>();
        for (int i = 0; i < count; i++)
        {
            var income = Income.Create(
                null,
                new DateOnly(2026, 1, 10 + (i % 20)),
                Money.Create(10m * (i + 1), Currency.DOP),
                $"Income {i}",
                null,
                null,
                new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
            incomes.Add(income);
        }

        return incomes;
    }

    private static Income CreateSingleIncome()
        => Income.Create(
            null,
            new DateOnly(2026, 1, 15),
            Money.Create(100m, Currency.DOP),
            "Test income",
            null,
            null,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    private static IncomeDto CreateIncomeDto()
        => new(
            Guid.NewGuid(),
            null,
            new DateOnly(2026, 1, 15),
            100m,
            Currency.DOP,
            "Test income",
            null,
            null,
            new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            null,
            null);
}
