using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Recurring;

/// <summary>
/// Tests para <see cref="RecurringRule.Create"/> y los metodos de
/// mutacion (<see cref="RecurringRule.Update"/>,
/// <see cref="RecurringRule.Activate"/>,
/// <see cref="RecurringRule.Deactivate"/>,
/// <see cref="RecurringRule.AdvanceAfterGeneration"/>).
/// </summary>
public sealed class RecurringRuleTests
{
    private static RecurringRule CreateValidRule() => RecurringRule.Create(
        name: "Netflix",
        amount: Money.Create(15.99m, Currency.DOP),
        currency: Currency.DOP,
        frequency: "Monthly",
        startDate: new DateOnly(2026, 1, 15),
        endDate: null,
        categoryId: Guid.NewGuid(),
        paymentMethodId: Guid.NewGuid());

    [Fact]
    public void Create_WithValidArgs_ReturnsSubscriptionWithExpectedState()
    {
        var start = new DateOnly(2026, 1, 15);
        var category = Guid.NewGuid();
        var payment = Guid.NewGuid();
        IReadOnlyList<Guid> tags = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        RecurringRule rule = RecurringRule.Create(
            name: "  Netflix  ",
            amount: Money.Create(15.99m, Currency.USD),
            currency: Currency.USD,
            frequency: "Monthly",
            startDate: start,
            endDate: null,
            categoryId: category,
            paymentMethodId: payment,
            tagIds: tags);

        Assert.NotEqual(Guid.Empty, rule.Id);
        Assert.Equal("Netflix", rule.Name); // trimmed
        Assert.Equal(15.99m, rule.Amount.Amount);
        Assert.Equal(Currency.USD, rule.Currency);
        Assert.Equal("Monthly", rule.Frequency);
        Assert.Equal(start, rule.StartDate);
        Assert.Null(rule.EndDate);
        Assert.Equal(category, rule.CategoryId);
        Assert.Equal(payment, rule.PaymentMethodId);
        Assert.Equal(tags, rule.TagIds);
        Assert.True(rule.Active);
        Assert.Equal(start, rule.NextGenerationDate);
    }

    [Fact]
    public void Create_WithEmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => RecurringRule.Create(
            name: string.Empty,
            amount: Money.Create(10m, Currency.DOP),
            currency: Currency.DOP,
            frequency: "Monthly",
            startDate: new DateOnly(2026, 1, 15),
            endDate: null,
            categoryId: Guid.NewGuid(),
            paymentMethodId: Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithWhitespaceName_Throws()
    {
        Assert.Throws<ArgumentException>(() => RecurringRule.Create(
            name: "   ",
            amount: Money.Create(10m, Currency.DOP),
            currency: Currency.DOP,
            frequency: "Monthly",
            startDate: new DateOnly(2026, 1, 15),
            endDate: null,
            categoryId: Guid.NewGuid(),
            paymentMethodId: Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithNameOver100Chars_Throws()
    {
        Assert.Throws<ArgumentException>(() => RecurringRule.Create(
            name: new string('a', 101),
            amount: Money.Create(10m, Currency.DOP),
            currency: Currency.DOP,
            frequency: "Monthly",
            startDate: new DateOnly(2026, 1, 15),
            endDate: null,
            categoryId: Guid.NewGuid(),
            paymentMethodId: Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithZeroAmount_Throws()
    {
        // Money.Create(0, ...) succeeds (0 is non-negative per Money's
        // invariant), so the failure comes from RecurringRule's
        // "strictly positive" check (ArgumentException).
        Assert.Throws<ArgumentException>(() => RecurringRule.Create(
            name: "Test",
            amount: Money.Create(0m, Currency.DOP),
            currency: Currency.DOP,
            frequency: "Monthly",
            startDate: new DateOnly(2026, 1, 15),
            endDate: null,
            categoryId: Guid.NewGuid(),
            paymentMethodId: Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithNegativeAmount_Throws()
    {
        // Money.Create(-1, ...) throws ArgumentOutOfRangeException
        // (negative violates Money's non-negative invariant).
        Assert.Throws<ArgumentOutOfRangeException>(() => RecurringRule.Create(
            name: "Test",
            amount: Money.Create(-1m, Currency.DOP),
            currency: Currency.DOP,
            frequency: "Monthly",
            startDate: new DateOnly(2026, 1, 15),
            endDate: null,
            categoryId: Guid.NewGuid(),
            paymentMethodId: Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithInvalidFrequency_Throws()
    {
        Assert.Throws<ArgumentException>(() => RecurringRule.Create(
            name: "Test",
            amount: Money.Create(10m, Currency.DOP),
            currency: Currency.DOP,
            frequency: "Weekly",
            startDate: new DateOnly(2026, 1, 15),
            endDate: null,
            categoryId: Guid.NewGuid(),
            paymentMethodId: Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithStartDateBefore1900_Throws()
    {
        Assert.Throws<ArgumentException>(() => RecurringRule.Create(
            name: "Test",
            amount: Money.Create(10m, Currency.DOP),
            currency: Currency.DOP,
            frequency: "Monthly",
            startDate: new DateOnly(1899, 12, 31),
            endDate: null,
            categoryId: Guid.NewGuid(),
            paymentMethodId: Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithEndDateBeforeStartDate_Throws()
    {
        Assert.Throws<ArgumentException>(() => RecurringRule.Create(
            name: "Test",
            amount: Money.Create(10m, Currency.DOP),
            currency: Currency.DOP,
            frequency: "Monthly",
            startDate: new DateOnly(2026, 6, 1),
            endDate: new DateOnly(2026, 5, 31),
            categoryId: Guid.NewGuid(),
            paymentMethodId: Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithEmptyCategoryId_Throws()
    {
        Assert.Throws<ArgumentException>(() => RecurringRule.Create(
            name: "Test",
            amount: Money.Create(10m, Currency.DOP),
            currency: Currency.DOP,
            frequency: "Monthly",
            startDate: new DateOnly(2026, 1, 15),
            endDate: null,
            categoryId: Guid.Empty,
            paymentMethodId: Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithEmptyPaymentMethodId_Throws()
    {
        Assert.Throws<ArgumentException>(() => RecurringRule.Create(
            name: "Test",
            amount: Money.Create(10m, Currency.DOP),
            currency: Currency.DOP,
            frequency: "Monthly",
            startDate: new DateOnly(2026, 1, 15),
            endDate: null,
            categoryId: Guid.NewGuid(),
            paymentMethodId: Guid.Empty));
    }

    [Fact]
    public void Update_WithValidArgs_UpdatesFieldsAndPreservesId()
    {
        RecurringRule rule = CreateValidRule();
        Guid originalId = rule.Id;

        rule.Update(
            name: "Netflix Premium",
            amount: Money.Create(25m, Currency.USD),
            currency: Currency.USD,
            frequency: "Yearly",
            startDate: new DateOnly(2026, 3, 1),
            endDate: new DateOnly(2027, 3, 1),
            categoryId: Guid.NewGuid(),
            paymentMethodId: Guid.NewGuid());

        Assert.Equal(originalId, rule.Id);
        Assert.Equal("Netflix Premium", rule.Name);
        Assert.Equal(25m, rule.Amount.Amount);
        Assert.Equal(Currency.USD, rule.Currency);
        Assert.Equal("Yearly", rule.Frequency);
        Assert.Equal(new DateOnly(2026, 3, 1), rule.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 1), rule.EndDate);
    }

    [Fact]
    public void Update_WithStartDateAfterNextGenerationDate_AdvancesCursor()
    {
        RecurringRule rule = CreateValidRule(); // NextGenerationDate = 2026-01-15
        rule.AdvanceAfterGeneration(new DateOnly(2026, 1, 15), 1); // → 2026-02-15

        rule.Update(
            name: "Netflix",
            amount: Money.Create(15.99m, Currency.DOP),
            currency: Currency.DOP,
            frequency: "Monthly",
            startDate: new DateOnly(2026, 6, 1),
            endDate: null,
            categoryId: rule.CategoryId,
            paymentMethodId: rule.PaymentMethodId);

        Assert.Equal(new DateOnly(2026, 6, 1), rule.NextGenerationDate);
    }

    [Fact]
    public void Deactivate_SetsActiveFalse()
    {
        RecurringRule rule = CreateValidRule();
        Assert.True(rule.Active);

        rule.Deactivate();

        Assert.False(rule.Active);
    }

    [Fact]
    public void Activate_SetsActiveTrue()
    {
        RecurringRule rule = CreateValidRule();
        rule.Deactivate();
        Assert.False(rule.Active);

        rule.Activate();

        Assert.True(rule.Active);
    }

    [Fact]
    public void AdvanceAfterGeneration_Monthly_AdvancesByOneMonth()
    {
        RecurringRule rule = CreateValidRule();
        rule.AdvanceAfterGeneration(new DateOnly(2026, 1, 15), 1);
        Assert.Equal(new DateOnly(2026, 2, 15), rule.NextGenerationDate);
    }

    [Fact]
    public void AdvanceAfterGeneration_Bimonthly_AdvancesByTwoMonths()
    {
        RecurringRule rule = CreateValidRule();
        rule.AdvanceAfterGeneration(new DateOnly(2026, 1, 15), 2);
        Assert.Equal(new DateOnly(2026, 3, 15), rule.NextGenerationDate);
    }

    [Fact]
    public void AdvanceAfterGeneration_Yearly_AdvancesByTwelveMonths()
    {
        RecurringRule rule = CreateValidRule();
        rule.AdvanceAfterGeneration(new DateOnly(2026, 1, 15), 12);
        Assert.Equal(new DateOnly(2027, 1, 15), rule.NextGenerationDate);
    }

    [Fact]
    public void AdvanceAfterGeneration_DateBeforeCursor_Throws()
    {
        RecurringRule rule = CreateValidRule();
        Assert.Throws<ArgumentException>(() => rule.AdvanceAfterGeneration(
            generatedDate: new DateOnly(2025, 12, 1),
            monthInterval: 1));
    }
}
