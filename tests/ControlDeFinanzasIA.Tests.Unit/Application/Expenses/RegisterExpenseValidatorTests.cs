using ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense;
using ControlDeFinanzasIA.Domain.Enums;
using FluentValidation.Results;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Expenses;

/// <summary>
/// Unit tests for <see cref="RegisterExpenseValidator"/>.
/// Validates the six synchronous FluentValidation rules using pure xUnit assertions.
/// </summary>
public sealed class RegisterExpenseValidatorTests
{
    private static readonly Guid _validCategoryId = new("11111111-1111-1111-1111-000000000001");

    private readonly RegisterExpenseValidator _validator = new();

    private static RegisterExpenseCommand CreateValidCommand()
        => new(
            _validCategoryId,
            new DateOnly(2026, 1, 15),
            100m,
            Currency.DOP,
            "Almuerzo",
            null);

    [Fact]
    public void Validate_ValidCommand_ReturnsSuccess()
    {
        RegisterExpenseCommand command = CreateValidCommand();

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.50)]
    public void Validate_ZeroOrNegativeAmount_ReturnsFailure(decimal amount)
    {
        RegisterExpenseCommand command = CreateValidCommand() with { Amount = amount };

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Amount");
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("  \n  ")]
    public void Validate_WhitespaceOnlyDescription_ReturnsFailure(string description)
    {
        RegisterExpenseCommand command = CreateValidCommand() with { Description = description };

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Description");
    }

    [Fact]
    public void Validate_OverMaxLengthDescription_ReturnsFailure()
    {
        RegisterExpenseCommand command = CreateValidCommand() with
        {
            Description = new string('a', 201),
        };

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Description");
    }

    // === PaymentMethodId (F-505) ===

    [Fact]
    public void Validate_NullPaymentMethodId_ReturnsSuccess()
    {
        RegisterExpenseCommand command = CreateValidCommand() with { PaymentMethodId = null };

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NonEmptyPaymentMethodId_ReturnsSuccess()
    {
        RegisterExpenseCommand command = CreateValidCommand() with
        {
            PaymentMethodId = new Guid("22222222-2222-2222-2222-000000000002"),
        };

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyPaymentMethodId_ReturnsFailure()
    {
        RegisterExpenseCommand command = CreateValidCommand() with { PaymentMethodId = Guid.Empty };

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "PaymentMethodId");
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("método de pago"));
    }
}
