using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Expenses.UpdateExpense;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Expenses.UpdateExpense;

/// <summary>
/// Unit tests for <see cref="UpdateExpenseUseCase"/>.
/// Uses hand-rolled fakes for <see cref="IClock"/> and
/// <see cref="IExpenseRepository"/> and pure xUnit assertions.
/// </summary>
public sealed class UpdateExpenseUseCaseTests
{
    private static readonly Guid _validCategoryId = new("11111111-1111-1111-1111-000000000001");

    private readonly FakeClock _clock;
    private readonly FakeExpenseRepository _repository;
    private readonly UpdateExpenseValidator _validator;
    private readonly UpdateExpenseUseCase _useCase;

    public UpdateExpenseUseCaseTests()
    {
        _clock = new FakeClock
        {
            UtcNow = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
        };
        _repository = new FakeExpenseRepository();
        _validator = new UpdateExpenseValidator();
        _useCase = new UpdateExpenseUseCase(
            _repository,
            _clock,
            _validator,
            NullLogger<UpdateExpenseUseCase>.Instance);
    }

    private UpdateExpenseCommand ValidCommand()
        => new(
            Guid.NewGuid(),
            _validCategoryId,
            DateOnly.FromDateTime(_clock.UtcNow),
            100m,
            Currency.DOP,
            "Test",
            null);

    private Expense SeedExpense()
    {
        var expense = Expense.Create(
            _validCategoryId,
            new DateOnly(2026, 1, 10),
            Money.Create(50m, Currency.DOP),
            "Original",
            null,
            new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc));
        _repository.GetByIdResultOverride = expense;
        return expense;
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCommand_ReturnsSuccess()
    {
        Expense seeded = SeedExpense();
        UpdateExpenseCommand command = ValidCommand() with { Id = seeded.Id };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(command.CategoryId, seeded.CategoryId);
        Assert.Equal(command.Date, seeded.Date);
        Assert.Equal(command.Amount, seeded.Amount.Amount);
        Assert.Equal(command.Currency, seeded.Amount.Currency);
        Assert.Equal(command.Description, seeded.Description);
        Assert.Null(seeded.Note);
        Assert.Equal(_clock.UtcNow, seeded.UpdatedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingExpense_ReturnsNotFound()
    {
        _repository.GetByIdResultOverride = null;
        UpdateExpenseCommand command = ValidCommand();

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCommand_ReturnsValidationFailure()
    {
        UpdateExpenseCommand command = ValidCommand() with { Amount = 0 };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Amount", result.Error.Details!.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_WithZeroAmount_ReturnsValidationFailure()
    {
        UpdateExpenseCommand command = ValidCommand() with { Amount = 0 };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Contains("Amount", result.Error.Details!.Keys);
    }

    // === PaymentMethodId (F-505) ===

    [Fact]
    public async Task ExecuteAsync_WithPaymentMethodId_AssignsToExpense()
    {
        Expense seeded = SeedExpense();
        Guid paymentMethodId = new("33333333-3333-3333-3333-000000000003");
        UpdateExpenseCommand command = ValidCommand() with
        {
            Id = seeded.Id,
            PaymentMethodId = paymentMethodId,
        };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(paymentMethodId, seeded.PaymentMethodId);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyPaymentMethodId_ReturnsValidationFailure()
    {
        Expense seeded = SeedExpense();
        UpdateExpenseCommand command = ValidCommand() with
        {
            Id = seeded.Id,
            PaymentMethodId = Guid.Empty,
        };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("PaymentMethodId", result.Error.Details!.Keys);
    }
}
