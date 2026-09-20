using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Budgets;

/// <summary>
/// Tests para las invariantes de <see cref="Budget.Create"/> y
/// <see cref="Budget.ChangeAmount"/> (R-3, S-3).
/// </summary>
public sealed class BudgetTests
{
    private static readonly Guid _validCategoryId = Guid.NewGuid();

    [Fact]
    public void Create_ValidBudget_ReturnsBudgetWithProvidedValues()
    {
        var amount = Money.Create(15000m, Currency.DOP);

        var budget = Budget.Create(2026, 9, _validCategoryId, amount);

        Assert.NotEqual(Guid.Empty, budget.Id);
        Assert.Equal(2026, budget.Year);
        Assert.Equal(9, budget.Month);
        Assert.Equal(_validCategoryId, budget.CategoryId);
        Assert.Equal(15000m, budget.Amount.Amount);
        Assert.Equal(Currency.DOP, budget.Currency);
    }

    [Fact]
    public void Create_CurrencyIsDenormalizedFromAmount()
    {
        var amount = Money.Create(500m, Currency.USD);

        var budget = Budget.Create(2026, 1, _validCategoryId, amount);

        Assert.Equal(Currency.USD, budget.Currency);
        Assert.Equal(Currency.USD, budget.Amount.Currency);
    }

    [Fact]
    public void Create_NullAmount_ThrowsArgumentNullException() => Assert.Throws<ArgumentNullException>(() => Budget.Create(2026, 1, _validCategoryId, null!));

    [Fact]
    public void Create_AmountZero_ThrowsArgumentException()
    {
        // Money.Create allows 0 (it only rejects negative amounts), so Budget.Create
        // must reject it via its own ValidateAmount to avoid BudgetEvaluator division by zero.
        var zero = Money.Create(0m, Currency.DOP);

        Assert.Throws<ArgumentException>(() => Budget.Create(2026, 1, _validCategoryId, zero));
    }

    [Fact]
    public void Create_NegativeAmount_RejectedByMoneyBeforeBudgetSeesIt() => Assert.Throws<ArgumentOutOfRangeException>(() => Money.Create(-1m, Currency.DOP));

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void Create_InvalidMonth_ThrowsArgumentException(int month)
    {
        var amount = Money.Create(1000m, Currency.DOP);

        Assert.Throws<ArgumentException>(() => Budget.Create(2026, month, _validCategoryId, amount));
    }

    [Theory]
    [InlineData(Budget.MinYear - 1)]
    [InlineData(Budget.MaxYear + 1)]
    public void Create_InvalidYear_ThrowsArgumentException(int year)
    {
        var amount = Money.Create(1000m, Currency.DOP);

        Assert.Throws<ArgumentException>(() => Budget.Create(year, 1, _validCategoryId, amount));
    }

    [Fact]
    public void Create_EmptyCategoryId_ThrowsArgumentException()
    {
        var amount = Money.Create(1000m, Currency.DOP);

        Assert.Throws<ArgumentException>(() => Budget.Create(2026, 1, Guid.Empty, amount));
    }

    [Fact]
    public void ChangeAmount_ValidAmount_ReplacesAmountAndCurrency()
    {
        var budget = Budget.Create(2026, 9, _validCategoryId, Money.Create(10000m, Currency.DOP));

        budget.ChangeAmount(Money.Create(9000m, Currency.DOP));

        Assert.Equal(9000m, budget.Amount.Amount);
        Assert.Equal(Currency.DOP, budget.Currency);
    }

    [Fact]
    public void ChangeAmount_DifferentCurrency_UpdatesDenormalizedCurrency()
    {
        var budget = Budget.Create(2026, 9, _validCategoryId, Money.Create(10000m, Currency.DOP));

        budget.ChangeAmount(Money.Create(200m, Currency.USD));

        Assert.Equal(200m, budget.Amount.Amount);
        Assert.Equal(Currency.USD, budget.Currency);
    }

    [Fact]
    public void ChangeAmount_ZeroAmount_ThrowsArgumentException()
    {
        var budget = Budget.Create(2026, 9, _validCategoryId, Money.Create(10000m, Currency.DOP));

        Assert.Throws<ArgumentException>(() => budget.ChangeAmount(Money.Create(0m, Currency.DOP)));
    }

    [Fact]
    public void ChangeAmount_NullAmount_ThrowsArgumentNullException()
    {
        var budget = Budget.Create(2026, 9, _validCategoryId, Money.Create(10000m, Currency.DOP));

        Assert.Throws<ArgumentNullException>(() => budget.ChangeAmount(null!));
    }
}
