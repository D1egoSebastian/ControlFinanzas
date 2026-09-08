using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Incomes.UpdateIncome;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Incomes.UpdateIncome;

/// <summary>
/// Unit tests for <see cref="UpdateIncomeUseCase"/>.
/// Uses hand-rolled fakes for <see cref="IClock"/> and
/// <see cref="IIncomeRepository"/> and pure xUnit assertions.
/// </summary>
public sealed class UpdateIncomeUseCaseTests
{
    private static readonly Guid _validCategoryId = new("11111111-1111-1111-1111-000000000002");

    private readonly FakeClock _clock;
    private readonly FakeIncomeRepository _repository;
    private readonly UpdateIncomeValidator _validator;
    private readonly UpdateIncomeUseCase _useCase;

    public UpdateIncomeUseCaseTests()
    {
        _clock = new FakeClock
        {
            UtcNow = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
        };
        _repository = new FakeIncomeRepository();
        _validator = new UpdateIncomeValidator();
        _useCase = new UpdateIncomeUseCase(
            _repository,
            _clock,
            _validator,
            NullLogger<UpdateIncomeUseCase>.Instance);
    }

    private UpdateIncomeCommand ValidCommand()
        => new(
            Guid.NewGuid(),
            null,
            DateOnly.FromDateTime(_clock.UtcNow),
            100m,
            Currency.DOP,
            "Test",
            null);

    private Income SeedIncome()
    {
        var income = Income.Create(
            _validCategoryId,
            new DateOnly(2026, 1, 10),
            Money.Create(50m, Currency.DOP),
            "Original",
            null,
            "Salary",
            new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc));
        _repository.GetByIdResultOverride = income;
        return income;
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCommand_ReturnsSuccess()
    {
        Income seeded = SeedIncome();
        UpdateIncomeCommand command = ValidCommand() with { Id = seeded.Id };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Null(seeded.CategoryId);
        Assert.Equal(command.Date, seeded.Date);
        Assert.Equal(command.Amount, seeded.Amount.Amount);
        Assert.Equal(command.Currency, seeded.Amount.Currency);
        Assert.Equal(command.Description, seeded.Description);
        Assert.Null(seeded.Note);
        Assert.Null(seeded.Source);
        Assert.Equal(_clock.UtcNow, seeded.UpdatedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingIncome_ReturnsNotFound()
    {
        _repository.GetByIdResultOverride = null;
        UpdateIncomeCommand command = ValidCommand();

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCommand_ReturnsValidationFailure()
    {
        UpdateIncomeCommand command = ValidCommand() with { Amount = 0 };

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
        UpdateIncomeCommand command = ValidCommand() with { Amount = 0 };

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Contains("Amount", result.Error.Details!.Keys);
    }

    // === PaymentMethodId (F-505) ===

    [Fact]
    public async Task ExecuteAsync_WithPaymentMethodId_AssignsToIncome()
    {
        Income seeded = SeedIncome();
        Guid paymentMethodId = new("44444444-4444-4444-4444-000000000004");
        UpdateIncomeCommand command = ValidCommand() with
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
        Income seeded = SeedIncome();
        UpdateIncomeCommand command = ValidCommand() with
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
