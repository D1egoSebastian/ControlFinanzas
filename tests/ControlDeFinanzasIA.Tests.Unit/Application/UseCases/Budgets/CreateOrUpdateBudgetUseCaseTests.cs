using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Budgets.CreateOrUpdateBudget;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Budgets;

/// <summary>
/// Tests for <see cref="CreateOrUpdateBudgetUseCase"/> (R-1, R-2, R-8,
/// S-2, S-9).
/// </summary>
public sealed class CreateOrUpdateBudgetUseCaseTests
{
    private static readonly Guid _categoryId = Guid.NewGuid();

    private static CreateOrUpdateBudgetUseCase CreateUseCase(
        FakeBudgetRepository budgetRepository, FakeCategoryRepository? categoryRepository = null)
    {
        categoryRepository ??= CategoryRepositoryWithSeededCategory();
        return new CreateOrUpdateBudgetUseCase(
            budgetRepository,
            categoryRepository,
            new CreateOrUpdateBudgetValidator(),
            NullLogger<CreateOrUpdateBudgetUseCase>.Instance);
    }

    private static FakeCategoryRepository CategoryRepositoryWithSeededCategory()
    {
        var repo = new FakeCategoryRepository();
        repo.SetCategories(new[] { new Category { Id = _categoryId, Name = "Comida" } });
        return repo;
    }

    private static CreateOrUpdateBudgetCommand ValidCommand() =>
        new(2026, 9, _categoryId, 10000m, Currency.DOP);

    [Fact]
    public async Task ExecuteAsync_NewCategoryAndMonth_AddsBudget()
    {
        var budgetRepository = new FakeBudgetRepository();
        CreateOrUpdateBudgetUseCase useCase = CreateUseCase(budgetRepository);

        Result result = await useCase.ExecuteAsync(ValidCommand());

        Assert.True(result.IsSuccess);
        Assert.True(budgetRepository.AddAsyncCalled);
        Assert.False(budgetRepository.UpdateAsyncCalled);
        Budget budget = Assert.Single(budgetRepository.Budgets);
        Assert.Equal(10000m, budget.Amount.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_SameCategoryAndMonthAgain_UpdatesInsteadOfDuplicating()
    {
        var budgetRepository = new FakeBudgetRepository();
        CreateOrUpdateBudgetUseCase useCase = CreateUseCase(budgetRepository);
        await useCase.ExecuteAsync(ValidCommand());

        Result result = await useCase.ExecuteAsync(ValidCommand() with { Amount = 15000m });

        Assert.True(result.IsSuccess);
        Assert.True(budgetRepository.UpdateAsyncCalled);
        Budget budget = Assert.Single(budgetRepository.Budgets);
        Assert.Equal(15000m, budget.Amount.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_NullCategoryId_ReturnsValidationFailureWithoutWriting()
    {
        var budgetRepository = new FakeBudgetRepository();
        CreateOrUpdateBudgetUseCase useCase = CreateUseCase(budgetRepository);
        CreateOrUpdateBudgetCommand command = ValidCommand() with { CategoryId = null };

        Result result = await useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(budgetRepository.Budgets);
        Assert.False(budgetRepository.AddAsyncCalled);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCategory_ReturnsCategoryNotFound()
    {
        var budgetRepository = new FakeBudgetRepository();
        var emptyCategoryRepository = new FakeCategoryRepository();
        CreateOrUpdateBudgetUseCase useCase = CreateUseCase(budgetRepository, emptyCategoryRepository);

        Result result = await useCase.ExecuteAsync(ValidCommand());

        Assert.False(result.IsSuccess);
        Assert.Equal("CategoryNotFound", result.Error!.Code);
        Assert.Empty(budgetRepository.Budgets);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task ExecuteAsync_NonPositiveAmount_ReturnsValidationFailureWithoutWriting(decimal amount)
    {
        var budgetRepository = new FakeBudgetRepository();
        CreateOrUpdateBudgetUseCase useCase = CreateUseCase(budgetRepository);
        CreateOrUpdateBudgetCommand command = ValidCommand() with { Amount = amount };

        Result result = await useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(budgetRepository.Budgets);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidMonth_ReturnsValidationFailureWithoutWriting()
    {
        var budgetRepository = new FakeBudgetRepository();
        CreateOrUpdateBudgetUseCase useCase = CreateUseCase(budgetRepository);
        CreateOrUpdateBudgetCommand command = ValidCommand() with { Month = 13 };

        Result result = await useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(budgetRepository.Budgets);
    }

    [Fact]
    public async Task ExecuteAsync_RepositoryFailure_PropagatesErrorAndDoesNotExposeSuccess()
    {
        var budgetRepository = new FakeBudgetRepository
        {
            AddResult = Result.Failure("RepositoryError", "boom"),
        };
        CreateOrUpdateBudgetUseCase useCase = CreateUseCase(budgetRepository);

        Result result = await useCase.ExecuteAsync(ValidCommand());

        Assert.False(result.IsSuccess);
        Assert.Equal("RepositoryError", result.Error!.Code);
        Assert.Empty(budgetRepository.Budgets);
    }
}
