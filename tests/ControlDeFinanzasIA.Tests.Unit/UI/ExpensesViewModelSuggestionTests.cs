using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Application.UseCases.Catalog.CreateTag;
using ControlDeFinanzasIA.Application.UseCases.Catalog.GetPaymentMethods;
using ControlDeFinanzasIA.Application.UseCases.Expenses.CategorizeExpense;
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
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.ExchangeRates;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// AI category-suggestion tests for <see cref="ExpensesViewModel"/> (F-013, slice 6, R-5, R-6, D-12).
/// Uses <see cref="FakeCategorizeExpenseUseCase"/> to control exactly when a suggestion completes, is
/// cancelled, or throws, without spinning up an HTTP fake.
/// </summary>
/// <remarks>
/// Placed alongside <c>ExpensesViewModelTests.cs</c> under <c>Tests.Unit/UI/</c> rather than at the
/// task's literal <c>Tests.Unit/ViewModels/</c> path, matching this repo's established sibling-file
/// convention for the class under test (same precedent as slice 5b.2's placement note).
/// </remarks>
public sealed class ExpensesViewModelSuggestionTests
{
    private static readonly Guid _categoryId = new("22222222-2222-2222-2222-000000000001");
    private static readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(25);

    /// <summary>
    /// Polls <paramref name="condition"/> until it is <see langword="true"/> or
    /// <paramref name="timeoutMs"/> elapses. Used instead of a single fixed
    /// <c>Task.Delay</c> for "wait until settled" assertions so the suite stays
    /// robust under CI/parallel-test CPU contention, where a fixed sleep can
    /// under-shoot the real elapsed debounce+call time.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met within the timeout.");
            }

            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task RapidKeystrokes_WithinDebounceWindow_ProduceExactlyOneCall()
    {
        // Arrange (R-a)
        FakeCategorizeExpenseUseCase fake = new();
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: fake);

        // Act
        viewModel.Description = "p";
        await Task.Delay(20);
        viewModel.Description = "pi";
        await Task.Delay(20);
        viewModel.Description = "piz";
        await Task.Delay(20);
        viewModel.Description = "pizz";
        await Task.Delay(20);
        viewModel.Description = "pizza 25";
        await WaitUntilAsync(() => fake.Calls.Count > 0);
        await Task.Delay(100); // grace period: confirm no straggler call from an earlier keystroke arrives late

        // Assert
        Assert.Single(fake.Calls);
        Assert.Equal("pizza 25", fake.Calls[0].Description);
    }

    [Fact]
    public async Task SlowerFirstResponse_SupersededByFasterSecond_LaterResultWinsAndStays()
    {
        // Arrange (R-b)
        FakeCategorizeExpenseUseCase fake = new()
        {
            Handler = async (command, ct) =>
            {
                if (command.Description == "text one")
                {
                    await Task.Delay(120, ct);
                    return Result.Success(new CategorySuggestionDto(_categoryId, "A", true, "A"));
                }

                await Task.Delay(10, ct);
                return Result.Success(new CategorySuggestionDto(_categoryId, "B", true, "B"));
            },
        };
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: fake);

        // Act
        viewModel.Description = "text one";
        await Task.Delay(40); // let the debounce fire and the slow call start
        viewModel.Description = "text two";
        await WaitUntilAsync(() => viewModel.SuggestedCategoryName is not null);
        await Task.Delay(50); // grace period: confirm the slow "text one" response never overwrites it

        // Assert
        Assert.Equal("B", viewModel.SuggestedCategoryName);
    }

    [Fact]
    public async Task ManualCategoryPick_WhileSuggestionInFlight_CancelsRequest_NoChip_KeepsUserValue()
    {
        // Arrange (R-c)
        TaskCompletionSource<bool> started = new();
        FakeCategorizeExpenseUseCase fake = new()
        {
            Handler = async (command, ct) =>
            {
                started.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, ct);
                return Result.Success(new CategorySuggestionDto(_categoryId, "Comida", true, "Comida"));
            },
        };
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: fake);
        Guid manualCategoryId = new("33333333-3333-3333-3333-000000000001");

        // Act
        viewModel.Description = "pizza 25";
        await started.Task;
        viewModel.SelectedCategoryId = manualCategoryId;
        await WaitUntilAsync(() => !viewModel.IsSuggestionLoading);

        // Assert
        Assert.False(viewModel.IsSuggestionVisible);
        Assert.Null(viewModel.SuggestedCategoryId);
        Assert.Equal(manualCategoryId, viewModel.SelectedCategoryId);
    }

    [Fact]
    public async Task CancelledSuggestion_NeverSurfacesAsFormErrorOrStatusMessage()
    {
        // Arrange (R-d)
        FakeCategorizeExpenseUseCase fake = new()
        {
            Handler = (command, ct) => throw new OperationCanceledException(ct),
        };
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: fake);

        // Act
        viewModel.Description = "pizza 25";
        await WaitUntilAsync(() => fake.Calls.Count > 0);
        await Task.Delay(50);

        // Assert
        Assert.Null(viewModel.FormError);
        Assert.Null(viewModel.AiStatusMessage);
        Assert.False(viewModel.IsSuggestionLoading);
    }

    [Fact]
    public async Task BeginEdit_OnAlreadyCategorizedExpense_MakesZeroCalls()
    {
        // Arrange (R-e)
        FakeCategorizeExpenseUseCase fake = new();
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: fake);
        ExpenseDto dto = new(
            Guid.NewGuid(),
            _categoryId,
            new DateOnly(2026, 1, 10),
            100m,
            Currency.DOP,
            "Almuerzo con el equipo",
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null,
            null);

        // Act
        viewModel.BeginEditCommand.Execute(dto);
        await Task.Delay(300); // nothing should happen — no positive condition to poll for

        // Assert
        Assert.Empty(fake.Calls);
        Assert.False(viewModel.IsSuggestionVisible);
    }

    [Fact]
    public async Task AcceptSuggestion_WritesCategoryExactlyOnce_OnlyViaCommand()
    {
        // Arrange (R-f)
        FakeCategorizeExpenseUseCase fake = new()
        {
            DefaultResult = Result.Success(new CategorySuggestionDto(_categoryId, "Comida", true, "Comida")),
        };
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: fake);

        // Act
        viewModel.Description = "pizza 25";
        await WaitUntilAsync(() => viewModel.SuggestedCategoryId is not null);

        // Assert: suggestion arrived, SelectedCategoryId untouched
        Assert.Equal(_categoryId, viewModel.SuggestedCategoryId);
        Assert.Null(viewModel.SelectedCategoryId);

        // Act: accept
        viewModel.AcceptSuggestionCommand.Execute(null);

        // Assert
        Assert.Equal(_categoryId, viewModel.SelectedCategoryId);
        Assert.False(viewModel.IsSuggestionVisible);
    }

    [Theory]
    [InlineData("piz", 0)]
    [InlineData("pizz", 1)]
    public async Task DescriptionLength_BelowFour_MakesNoCall_AtFourMakesOneCall(string description, int expectedCalls)
    {
        // Arrange
        FakeCategorizeExpenseUseCase fake = new();
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: fake);

        // Act
        viewModel.Description = description;
        if (expectedCalls > 0)
        {
            await WaitUntilAsync(() => fake.Calls.Count > 0);
        }
        else
        {
            await Task.Delay(300); // nothing should happen — no positive condition to poll for
        }

        // Assert
        Assert.Equal(expectedCalls, fake.Calls.Count);
    }

    [Fact]
    public async Task RetypingSameText_HitsCache_MakesZeroExtraCalls()
    {
        // Arrange
        FakeCategorizeExpenseUseCase fake = new()
        {
            DefaultResult = Result.Success(new CategorySuggestionDto(_categoryId, "Comida", true, "Comida")),
        };
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: fake);

        viewModel.Description = "pizza 25";
        await WaitUntilAsync(() => fake.Calls.Count > 0);
        Assert.Single(fake.Calls);

        // Act: delete the whole field (below the 4-char guard, clearing the chip) then retype the
        // exact same (trimmed) text that was already answered above.
        viewModel.Description = string.Empty;
        await Task.Delay(150);
        Assert.False(viewModel.IsSuggestionVisible);

        viewModel.Description = "pizza 25";
        await WaitUntilAsync(() => viewModel.SuggestedCategoryId is not null);
        await Task.Delay(50); // grace period: confirm no extra call sneaks in for the cache hit

        // Assert
        Assert.Single(fake.Calls);
        Assert.Equal(_categoryId, viewModel.SuggestedCategoryId);
    }

    [Fact]
    public async Task IsSuggestionLoading_ReturnsToFalse_OnFailurePath()
    {
        // Arrange
        FakeCategorizeExpenseUseCase fake = new()
        {
            DefaultResult = Result.Failure("LLMUnavailable", "boom"),
        };
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: fake);

        // Act
        viewModel.Description = "pizza 25";
        await WaitUntilAsync(() => viewModel.AiStatusMessage is not null);

        // Assert
        Assert.False(viewModel.IsSuggestionLoading);
        Assert.Equal("ExpensesAiUnavailableMessage", viewModel.AiStatusMessage);
    }

    [Fact]
    public async Task SuccessfulMatch_NeverMutatesSelectedCategoryIdDirectly()
    {
        // Arrange
        FakeCategorizeExpenseUseCase fake = new()
        {
            DefaultResult = Result.Success(new CategorySuggestionDto(_categoryId, "Comida", true, "Comida")),
        };
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: fake);

        // Act
        viewModel.Description = "pizza 25";
        await WaitUntilAsync(() => viewModel.SuggestedCategoryId is not null);

        // Assert
        Assert.Equal(_categoryId, viewModel.SuggestedCategoryId);
        Assert.Null(viewModel.SelectedCategoryId);
    }

    [Fact]
    public async Task DismissSuggestion_ClearsChip_LeavesSelectedCategoryIdUntouched()
    {
        // Arrange
        FakeCategorizeExpenseUseCase fake = new()
        {
            DefaultResult = Result.Success(new CategorySuggestionDto(_categoryId, "Comida", true, "Comida")),
        };
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: fake);
        viewModel.Description = "pizza 25";
        await WaitUntilAsync(() => viewModel.IsSuggestionVisible);
        Assert.True(viewModel.IsSuggestionVisible);

        // Act
        viewModel.DismissSuggestionCommand.Execute(null);

        // Assert
        Assert.False(viewModel.IsSuggestionVisible);
        Assert.Null(viewModel.SelectedCategoryId);
    }

    [Fact]
    public async Task Failure_SetsStatusMessage_FormStaysFullyUsable()
    {
        // Arrange
        FakeCategorizeExpenseUseCase fake = new()
        {
            DefaultResult = Result.Failure("LLMUnavailable", "boom"),
        };
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: fake);

        // Act
        viewModel.Description = "pizza 25";
        viewModel.Amount = 25m;
        await WaitUntilAsync(() => viewModel.AiStatusMessage is not null);

        // Assert
        Assert.Equal("ExpensesAiUnavailableMessage", viewModel.AiStatusMessage);
        Assert.Equal(25m, viewModel.Amount);
        Assert.Equal("pizza 25", viewModel.Description);
        Assert.NotNull(viewModel.RegisterExpenseCommand);
    }

    [Fact]
    public async Task NoMatch_SetsNeitherChipNorStatusMessage()
    {
        // Arrange (S-10)
        FakeCategorizeExpenseUseCase fake = new()
        {
            DefaultResult = Result.Success(new CategorySuggestionDto(null, null, false, "no-match")),
        };
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: fake);

        // Act
        viewModel.Description = "pizza 25";
        await WaitUntilAsync(() => fake.Calls.Count > 0);
        await Task.Delay(50);

        // Assert
        Assert.False(viewModel.IsSuggestionVisible);
        Assert.Null(viewModel.AiStatusMessage);
    }

    [Fact]
    public async Task KillSwitch_NullUseCase_TypingLongDescription_NoExceptionNoStatusNoChip()
    {
        // Arrange (D-12 kill switch)
        ExpensesViewModel viewModel = CreateViewModel(categorizeExpenseUseCase: null);

        // Act
        Exception? caught = await Record.ExceptionAsync(async () =>
        {
            viewModel.Description = "a perfectly long, qualifying expense description";
            await Task.Delay(300); // nothing should happen — no positive condition to poll for
        });

        // Assert
        Assert.Null(caught);
        Assert.Null(viewModel.AiStatusMessage);
        Assert.False(viewModel.IsSuggestionVisible);
    }

    private static ExpensesViewModel CreateViewModel(CategorizeExpenseUseCase? categorizeExpenseUseCase)
    {
        FakeClock clock = new()
        {
            UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            Today = new DateOnly(2026, 1, 15),
        };
        FakeExpenseRepository expenseRepo = new();
        FakeIncomeRepository incomeRepo = new();
        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories([new Category { Id = _categoryId, Name = "Comida" }]);
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

        ICurrencyConverter currencyConverter = new CurrencyConverter(
            new StubExchangeRateProvider(
                new ConfigurationBuilder().AddInMemoryCollection().Build(),
                new FakeClock { UtcNow = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc) }),
            new MemoryCache(new MemoryCacheOptions()),
            new FakeClock(),
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            NullLogger<CurrencyConverter>.Instance);

        IStringLocalizer<Strings> localizer = new FakeStringLocalizer<Strings>();

        ExpensesViewModel viewModel = new(
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
            currencyConverter,
            clock,
            NullLogger<ExpensesViewModel>.Instance,
            localizer,
            categorizeExpenseUseCase);

        viewModel.SuggestionDebounceDelay = _debounce;
        return viewModel;
    }
}
