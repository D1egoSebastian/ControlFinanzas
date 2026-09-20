using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Budgets;

/// <summary>
/// Tests for <see cref="GetBudgetVsActualQuery"/> (R-4, R-5, D-8, S-4,
/// S-5, S-6). Fakes all 4 dependencies per the design's testing strategy.
/// </summary>
public sealed class GetBudgetVsActualQueryTests
{
    private static readonly DateOnly _september = new(2026, 9, 15);

    private static GetBudgetVsActualQuery CreateQuery(
        FakeBudgetRepository budgets,
        FakeExpenseRepository expenses,
        FakeCategoryRepository categories,
        FakeCurrencyConverter converter)
        => new(budgets, expenses, categories, converter);

    private static Expense CreateExpense(Guid categoryId, decimal amount, Currency currency)
        => Expense.Create(
            categoryId,
            _september,
            Money.Create(amount, currency),
            "Test expense",
            null,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task ExecuteAsync_ForeignCurrencyExpense_CountsAgainstDopBudgetAtConvertedAmount()
    {
        var categoryId = Guid.NewGuid();
        var budgets = new FakeBudgetRepository();
        budgets.Seed(Budget.Create(2026, 9, categoryId, Money.Create(15000m, Currency.DOP)));

        var expenses = new FakeExpenseRepository();
        expenses.CategoryNames[categoryId] = "Comida";
        await expenses.AddAsync(CreateExpense(categoryId, 5000m, Currency.DOP));
        await expenses.AddAsync(CreateExpense(categoryId, 100m, Currency.USD)); // converts to 6000 DOP below

        var categories = new FakeCategoryRepository();
        categories.SetCategories(new[] { new Category { Id = categoryId, Name = "Comida" } });

        // 1 USD -> 60 DOP for this test, so 100 USD -> 6000 DOP.
        var converter = new FakeCurrencyConverter(onConvert: (money, target) =>
            Result.Success(Money.Create(money.Amount * 60m, target)));

        GetBudgetVsActualQuery query = CreateQuery(budgets, expenses, categories, converter);

        Result result = await query.ExecuteAsync(_september);

        Assert.True(result.IsSuccess);
        BudgetVsActualResult vsActual = Assert.IsType<BudgetVsActualResult>(result.Value);
        BudgetVsActualLine line = Assert.Single(vsActual.Lines);
        Assert.Equal(11000m, line.ActualDop); // 5000 DOP + 6000 converted USD
        Assert.Equal(15000m, line.BudgetDop);
        Assert.False(line.IsIndeterminate);
        Assert.Equal(73.33m, line.PercentConsumed); // 11000 / 15000 * 100, rounded to 2 places
        Assert.Equal(BudgetStatus.Ok, line.Status); // 73.33% < 80% Near threshold
    }

    [Fact]
    public async Task ExecuteAsync_BudgetedCategoryWithZeroSpend_ResolvesNameFromCategoryRepositoryAndIsOk()
    {
        var categoryId = Guid.NewGuid();
        var budgets = new FakeBudgetRepository();
        budgets.Seed(Budget.Create(2026, 9, categoryId, Money.Create(10000m, Currency.DOP)));

        var expenses = new FakeExpenseRepository(); // no expenses recorded at all
        var categories = new FakeCategoryRepository();
        categories.SetCategories(new[] { new Category { Id = categoryId, Name = "Entretenimiento" } });
        var converter = new FakeCurrencyConverter();

        GetBudgetVsActualQuery query = CreateQuery(budgets, expenses, categories, converter);

        Result result = await query.ExecuteAsync(_september);

        BudgetVsActualResult vsActual = Assert.IsType<BudgetVsActualResult>(result.Value);
        BudgetVsActualLine line = Assert.Single(vsActual.Lines);
        Assert.Equal("Entretenimiento", line.CategoryName);
        Assert.Equal(0m, line.ActualDop);
        Assert.Equal(0.00m, line.PercentConsumed);
        Assert.Equal(BudgetStatus.Ok, line.Status);
    }

    [Fact]
    public async Task ExecuteAsync_BudgetSideConversionFails_LineIsIndeterminateAndSetsHasMissingRatesNeverZeroOrHundred()
    {
        var categoryId = Guid.NewGuid();
        var budgets = new FakeBudgetRepository();
        budgets.Seed(Budget.Create(2026, 9, categoryId, Money.Create(10000m, Currency.USD)));

        var expenses = new FakeExpenseRepository();
        expenses.CategoryNames[categoryId] = "Viajes";
        await expenses.AddAsync(CreateExpense(categoryId, 2000m, Currency.DOP));

        var categories = new FakeCategoryRepository();
        categories.SetCategories(new[] { new Category { Id = categoryId, Name = "Viajes" } });

        // USD -> DOP conversion fails (budget side); DOP -> DOP short-circuits (actual side).
        var converter = new FakeCurrencyConverter(onConvert: (_, _) =>
            Result.Failure("ExchangeRateUnavailable", "No rate."));

        GetBudgetVsActualQuery query = CreateQuery(budgets, expenses, categories, converter);

        Result result = await query.ExecuteAsync(_september);

        BudgetVsActualResult vsActual = Assert.IsType<BudgetVsActualResult>(result.Value);
        Assert.True(vsActual.HasMissingRates);
        BudgetVsActualLine line = Assert.Single(vsActual.Lines);
        Assert.True(line.IsIndeterminate);
        Assert.Null(line.Status);
        Assert.Null(line.PercentConsumed);
        Assert.Null(line.BudgetDop);
    }

    [Fact]
    public async Task ExecuteAsync_ActualSidePartialFailure_PoisonsTheWholeCategory()
    {
        var categoryId = Guid.NewGuid();
        var budgets = new FakeBudgetRepository();
        budgets.Seed(Budget.Create(2026, 9, categoryId, Money.Create(10000m, Currency.DOP)));

        var expenses = new FakeExpenseRepository();
        expenses.CategoryNames[categoryId] = "Comida";
        await expenses.AddAsync(CreateExpense(categoryId, 3000m, Currency.DOP));  // convertible
        await expenses.AddAsync(CreateExpense(categoryId, 50m, Currency.USD));    // fails

        var categories = new FakeCategoryRepository();
        categories.SetCategories(new[] { new Category { Id = categoryId, Name = "Comida" } });

        var converter = new FakeCurrencyConverter(onConvert: (money, _) =>
            money.Currency == Currency.USD
                ? Result.Failure("ExchangeRateUnavailable", "No rate.")
                : Result.Success(money));

        GetBudgetVsActualQuery query = CreateQuery(budgets, expenses, categories, converter);

        Result result = await query.ExecuteAsync(_september);

        BudgetVsActualResult vsActual = Assert.IsType<BudgetVsActualResult>(result.Value);
        Assert.True(vsActual.HasMissingRates);
        BudgetVsActualLine line = Assert.Single(vsActual.Lines);
        Assert.True(line.IsIndeterminate);
        Assert.Null(line.ActualDop); // the whole category is poisoned, not partially summed to 3000
    }

    [Fact]
    public async Task ExecuteAsync_FailureInNonBudgetedCategory_LeavesHasMissingRatesFalse()
    {
        var budgetedCategoryId = Guid.NewGuid();
        var nonBudgetedCategoryId = Guid.NewGuid();
        var budgets = new FakeBudgetRepository();
        budgets.Seed(Budget.Create(2026, 9, budgetedCategoryId, Money.Create(5000m, Currency.DOP)));

        var expenses = new FakeExpenseRepository();
        expenses.CategoryNames[budgetedCategoryId] = "Comida";
        expenses.CategoryNames[nonBudgetedCategoryId] = "Ocio";
        await expenses.AddAsync(CreateExpense(budgetedCategoryId, 1000m, Currency.DOP));
        await expenses.AddAsync(CreateExpense(nonBudgetedCategoryId, 40m, Currency.USD)); // unconvertible, but not budgeted

        var categories = new FakeCategoryRepository();
        categories.SetCategories(new[] { new Category { Id = budgetedCategoryId, Name = "Comida" } });

        var converter = new FakeCurrencyConverter(onConvert: (money, _) =>
            money.Currency == Currency.USD
                ? Result.Failure("ExchangeRateUnavailable", "No rate.")
                : Result.Success(money));

        GetBudgetVsActualQuery query = CreateQuery(budgets, expenses, categories, converter);

        Result result = await query.ExecuteAsync(_september);

        BudgetVsActualResult vsActual = Assert.IsType<BudgetVsActualResult>(result.Value);
        Assert.False(vsActual.HasMissingRates);
        BudgetVsActualLine line = Assert.Single(vsActual.Lines);
        Assert.False(line.IsIndeterminate);
        Assert.Equal(1000m, line.ActualDop);
    }

    [Fact]
    public async Task ExecuteAsync_NoBudgetsForMonth_ReturnsEmptyResultAndSkipsCategoryTotalsQuery()
    {
        var budgets = new FakeBudgetRepository();
        var expenses = new FakeExpenseRepository();
        var categories = new FakeCategoryRepository();
        var converter = new FakeCurrencyConverter();

        GetBudgetVsActualQuery query = CreateQuery(budgets, expenses, categories, converter);

        Result result = await query.ExecuteAsync(_september);

        Assert.True(result.IsSuccess);
        BudgetVsActualResult vsActual = Assert.IsType<BudgetVsActualResult>(result.Value);
        Assert.Empty(vsActual.Lines);
        Assert.Equal(0m, vsActual.TotalBudgetDop);
        Assert.Equal(0m, vsActual.TotalActualDop);
        Assert.False(vsActual.HasMissingRates);
        Assert.Equal(0, expenses.GetCategoryTotalsCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_PercentTie_OrdersDeterministicallyByCategoryNameOrdinal()
    {
        var categoryA = Guid.NewGuid();
        var categoryB = Guid.NewGuid();
        var budgets = new FakeBudgetRepository();
        budgets.Seed(Budget.Create(2026, 9, categoryA, Money.Create(10000m, Currency.DOP)));
        budgets.Seed(Budget.Create(2026, 9, categoryB, Money.Create(10000m, Currency.DOP)));

        var expenses = new FakeExpenseRepository();
        expenses.CategoryNames[categoryA] = "Zapatos";
        expenses.CategoryNames[categoryB] = "Almuerzo";
        await expenses.AddAsync(CreateExpense(categoryA, 5000m, Currency.DOP)); // 50% tie
        await expenses.AddAsync(CreateExpense(categoryB, 5000m, Currency.DOP)); // 50% tie

        var categories = new FakeCategoryRepository();
        categories.SetCategories(new[]
        {
            new Category { Id = categoryA, Name = "Zapatos" },
            new Category { Id = categoryB, Name = "Almuerzo" },
        });
        var converter = new FakeCurrencyConverter();

        GetBudgetVsActualQuery query = CreateQuery(budgets, expenses, categories, converter);

        Result result = await query.ExecuteAsync(_september);

        BudgetVsActualResult vsActual = Assert.IsType<BudgetVsActualResult>(result.Value);
        Assert.Equal(2, vsActual.Lines.Count);
        Assert.Equal("Almuerzo", vsActual.Lines[0].CategoryName); // ordinal tie-break
        Assert.Equal("Zapatos", vsActual.Lines[1].CategoryName);
    }
}
