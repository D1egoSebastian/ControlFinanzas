using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Incomes.RegisterIncome;

/// <summary>
/// Unit tests for <see cref="RegisterIncomeUseCase"/>.
/// Uses hand-rolled fakes for <see cref="IClock"/> and
/// <see cref="IIncomeRepository"/> and pure xUnit assertions.
/// </summary>
public sealed class RegisterIncomeUseCaseTests
{
    private static readonly Guid _validCategoryId = new("11111111-1111-1111-1111-000000000001");

    private readonly FakeClock _clock;
    private readonly FakeIncomeRepository _repository;
    private readonly RegisterIncomeValidator _validator;
    private readonly RegisterIncomeUseCase _useCase;

    public RegisterIncomeUseCaseTests()
    {
        _clock = new FakeClock
        {
            UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
        };
        _repository = new FakeIncomeRepository();
        _validator = new RegisterIncomeValidator();
        _useCase = new RegisterIncomeUseCase(
            _repository,
            _clock,
            _validator,
            NullLogger<RegisterIncomeUseCase>.Instance);
    }

    private RegisterIncomeCommand CreateValidCommand()
        => new(
            _validCategoryId,
            DateOnly.FromDateTime(_clock.UtcNow),
            100m,
            Currency.DOP,
            "Sueldo",
            null);

    [Fact]
    public async Task ExecuteAsync_WithValidCommand_ReturnsSuccessWithIncomeId()
    {
        RegisterIncomeCommand command = CreateValidCommand();

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        RegisterIncomeResult actual = Assert.IsType<RegisterIncomeResult>(result.Value);

        Assert.NotEqual(Guid.Empty, actual.IncomeId);
        Assert.Empty(actual.Warnings);

        Income persisted = Assert.Single(_repository.AddedIncomes);
        Assert.Equal(actual.IncomeId, persisted.Id);
        Assert.Equal(_validCategoryId, persisted.CategoryId);
    }

    [Fact]
    public async Task ExecuteAsync_WithZeroAmount_ReturnsValidationFailure()
    {
        RegisterIncomeCommand command = CreateValidCommand() with { Amount = 0 };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Amount", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedIncomes);
    }

    [Fact]
    public async Task ExecuteAsync_WithNegativeAmount_ReturnsValidationFailure()
    {
        RegisterIncomeCommand command = CreateValidCommand() with { Amount = -1 };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Amount", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedIncomes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task ExecuteAsync_WithEmptyDescription_ReturnsValidationFailure(string description)
    {
        RegisterIncomeCommand command = CreateValidCommand() with { Description = description };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Description", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedIncomes);
    }

    [Fact]
    public async Task ExecuteAsync_WithDescriptionOver200Chars_ReturnsValidationFailure()
    {
        RegisterIncomeCommand command = CreateValidCommand() with
        {
            Description = new string('x', 201),
        };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Description", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedIncomes);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoteOver500Chars_ReturnsValidationFailure()
    {
        RegisterIncomeCommand command = CreateValidCommand() with
        {
            Note = new string('x', 501),
        };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Note", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedIncomes);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCurrencyEnum_ReturnsValidationFailure()
    {
        RegisterIncomeCommand command = CreateValidCommand() with { Currency = (Currency)999 };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Currency", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedIncomes);
    }

    [Fact]
    public async Task ExecuteAsync_WithFutureDate_ReturnsSuccessWithFutureDateWarning()
    {
        RegisterIncomeCommand command = CreateValidCommand() with
        {
            Date = new DateOnly(2026, 1, 20),
        };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        RegisterIncomeResult actual = Assert.IsType<RegisterIncomeResult>(result.Value);

        Assert.Contains("FutureDateWarning", actual.Warnings);
        Assert.Single(_repository.AddedIncomes);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCategory_PersistsAndReturnsSuccess()
    {
        RegisterIncomeCommand command = CreateValidCommand();

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        RegisterIncomeResult actual = Assert.IsType<RegisterIncomeResult>(result.Value);

        Income persisted = Assert.Single(_repository.AddedIncomes);
        Assert.Equal(actual.IncomeId, persisted.Id);
        Assert.Equal(_validCategoryId, persisted.CategoryId);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownCategory_ReturnsCategoryNotFoundFailure()
    {
        _repository.SetAddResult(Result.Failure("CategoryNotFound", "La categoría no existe."));
        RegisterIncomeCommand command = CreateValidCommand();

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("CategoryNotFound", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullCategory_PersistsWithoutCategory()
    {
        RegisterIncomeCommand command = CreateValidCommand() with { CategoryId = null };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        Income persisted = Assert.Single(_repository.AddedIncomes);
        Assert.Null(persisted.CategoryId);
    }

    [Fact]
    public async Task ExecuteAsync_AddAsyncSucceeds_AuditDatesFromClock()
    {
        _clock.UtcNow = new DateTime(2026, 3, 10, 14, 30, 0, DateTimeKind.Utc);
        RegisterIncomeCommand command = CreateValidCommand();

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        Income persisted = Assert.Single(_repository.AddedIncomes);
        Assert.Equal(_clock.UtcNow, persisted.CreatedAt);
        Assert.Equal(_clock.UtcNow, persisted.UpdatedAt);
    }

    // === PaymentMethodId (F-505) ===

    [Fact]
    public async Task ExecuteAsync_WithPaymentMethodId_AssignsToIncome()
    {
        Guid paymentMethodId = new("33333333-3333-3333-3333-000000000003");
        RegisterIncomeCommand command = CreateValidCommand() with { PaymentMethodId = paymentMethodId };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        Income persisted = Assert.Single(_repository.AddedIncomes);
        Assert.Equal(paymentMethodId, persisted.PaymentMethodId);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyPaymentMethodId_ReturnsValidationFailure()
    {
        RegisterIncomeCommand command = CreateValidCommand() with { PaymentMethodId = Guid.Empty };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("PaymentMethodId", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedIncomes);
    }
}
