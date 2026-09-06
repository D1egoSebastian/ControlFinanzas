using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Expenses;

/// <summary>
/// Unit tests for <see cref="RegisterExpenseUseCase"/>.
/// Uses hand-rolled fakes for <see cref="IClock"/> and
/// <see cref="IExpenseRepository"/> and pure xUnit assertions.
/// </summary>
public sealed class RegisterExpenseUseCaseTests
{
    private static readonly Guid _validCategoryId = new("11111111-1111-1111-1111-000000000001");

    private readonly FakeClock _clock;
    private readonly FakeExpenseRepository _repository;
    private readonly RegisterExpenseValidator _validator;
    private readonly RegisterExpenseUseCase _useCase;

    public RegisterExpenseUseCaseTests()
    {
        _clock = new FakeClock
        {
            UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
        };
        _repository = new FakeExpenseRepository();
        _validator = new RegisterExpenseValidator();
        _useCase = new RegisterExpenseUseCase(
            _repository,
            _clock,
            _validator,
            NullLogger<RegisterExpenseUseCase>.Instance);
    }

    private RegisterExpenseCommand CreateValidCommand()
        => new(
            _validCategoryId,
            DateOnly.FromDateTime(_clock.UtcNow),
            100m,
            Currency.DOP,
            "Almuerzo",
            null);

    [Fact]
    public async Task ExecuteAsync_ValidCommand_ReturnsSuccessWithDto()
    {
        RegisterExpenseCommand command = CreateValidCommand();

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.IsType<RegisterExpenseResult>(result.Value);
        var actual = (RegisterExpenseResult)result.Value;

        Assert.NotEqual(Guid.Empty, actual.Expense.Id);
        Assert.Equal(_validCategoryId, actual.Expense.CategoryId);
        Assert.Equal(command.Date, actual.Expense.Date);
        Assert.Equal(100m, actual.Expense.Amount);
        Assert.Equal(Currency.DOP, actual.Expense.Currency);
        Assert.Equal("Almuerzo", actual.Expense.Description);
        Assert.Null(actual.Expense.Note);
        Assert.Equal(_clock.UtcNow, actual.Expense.CreatedAt);
        Assert.Equal(_clock.UtcNow, actual.Expense.UpdatedAt);
        Assert.Empty(actual.Warnings);

        Expense persisted = Assert.Single(_repository.AddedExpenses);
        Assert.Equal(actual.Expense.Id, persisted.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidAmount_ReturnsValidationFailure(decimal amount)
    {
        RegisterExpenseCommand command = CreateValidCommand() with { Amount = amount };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Amount", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedExpenses);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("  \n  ")]
    public async Task ExecuteAsync_EmptyDescription_ReturnsValidationFailure(string description)
    {
        RegisterExpenseCommand command = CreateValidCommand() with { Description = description };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Description", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedExpenses);
    }

    [Fact]
    public async Task ExecuteAsync_FutureDate_AddsFutureDateWarning()
    {
        _clock.UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        RegisterExpenseCommand command = CreateValidCommand() with
        {
            Date = new DateOnly(2026, 1, 20),
        };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.IsType<RegisterExpenseResult>(result.Value);
        var actual = (RegisterExpenseResult)result.Value;

        Assert.Contains("FutureDateWarning", actual.Warnings);
        Assert.Single(_repository.AddedExpenses);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCategoryId_PropagatesCategoryNotFound()
    {
        RegisterExpenseCommand command = CreateValidCommand();
        _repository.SetAddResult(Result.Failure("CategoryNotFound", "La categoría no existe."));

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("CategoryNotFound", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_AddAsyncSucceeds_AuditDatesFromClock()
    {
        _clock.UtcNow = new DateTime(2026, 3, 10, 14, 30, 0, DateTimeKind.Utc);
        RegisterExpenseCommand command = CreateValidCommand();

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.IsType<RegisterExpenseResult>(result.Value);
        var actual = (RegisterExpenseResult)result.Value;

        Assert.Equal(_clock.UtcNow, actual.Expense.CreatedAt);
        Assert.Equal(_clock.UtcNow, actual.Expense.UpdatedAt);
    }
}
