using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Dashboard;

/// <summary>
/// Unit tests for <see cref="GetMonthSummaryQuery"/> (F-008, slice 2).
/// Covers R-2 (normalization to DOP), the MoM delta (positive, negative,
/// previous == 0 → null) and R-7/S-6 (FX failure degrades without throwing).
/// </summary>
public sealed class GetMonthSummaryQueryTests
{
    private static readonly Guid _categoryId = Guid.NewGuid();
    private static readonly DateOnly _march = new(2026, 3, 15);
    private static readonly DateOnly _february = new(2026, 2, 10);

    [Fact]
    public async Task ExecuteAsync_MixedCurrencies_NormalizesEverythingToDop()
    {
        FakeExpenseRepository expenseRepo = new();
        await AddExpensesAsync(expenseRepo,
            CreateExpense(_march, 1000m, Currency.DOP),
            CreateExpense(_march, 100m, Currency.USD));

        FakeIncomeRepository incomeRepo = new();
        await AddIncomesAsync(incomeRepo, CreateIncome(_march, 500m, Currency.DOP));

        FakeCurrencyConverter converter = new(onConvert: (money, target) =>
            Result.Success(Money.Create(money.Amount * 60m, target)));

        GetMonthSummaryQuery query = new(expenseRepo, incomeRepo, converter);

        Result result = await query.ExecuteAsync(_march);

        Assert.True(result.IsSuccess);
        MonthSummaryResult summary = Assert.IsType<MonthSummaryResult>(result.Value);
        Assert.Equal(7000m, summary.ExpensesDop);
        Assert.Equal(500m, summary.IncomesDop);
        Assert.Equal(-6500m, summary.NetBalanceDop);
        Assert.False(summary.HasMissingRates);
    }

    [Fact]
    public async Task ExecuteAsync_ExpensesIncreasedVsPreviousMonth_ReturnsPositiveDelta()
    {
        FakeExpenseRepository expenseRepo = new();
        await AddExpensesAsync(expenseRepo,
            CreateExpense(_february, 1000m, Currency.DOP),
            CreateExpense(_march, 1500m, Currency.DOP));

        FakeIncomeRepository incomeRepo = new();
        FakeCurrencyConverter converter = new();

        GetMonthSummaryQuery query = new(expenseRepo, incomeRepo, converter);

        Result result = await query.ExecuteAsync(_march);

        MonthSummaryResult summary = Assert.IsType<MonthSummaryResult>(result.Value);
        Assert.Equal(50m, summary.ExpensesDeltaPercent);
    }

    [Fact]
    public async Task ExecuteAsync_ExpensesDecreasedVsPreviousMonth_ReturnsNegativeDelta()
    {
        FakeExpenseRepository expenseRepo = new();
        await AddExpensesAsync(expenseRepo,
            CreateExpense(_february, 2000m, Currency.DOP),
            CreateExpense(_march, 1000m, Currency.DOP));

        FakeIncomeRepository incomeRepo = new();
        FakeCurrencyConverter converter = new();

        GetMonthSummaryQuery query = new(expenseRepo, incomeRepo, converter);

        Result result = await query.ExecuteAsync(_march);

        MonthSummaryResult summary = Assert.IsType<MonthSummaryResult>(result.Value);
        Assert.Equal(-50m, summary.ExpensesDeltaPercent);
    }

    [Fact]
    public async Task ExecuteAsync_PreviousMonthHadNoExpenses_ReturnsNullDelta()
    {
        FakeExpenseRepository expenseRepo = new();
        await AddExpensesAsync(expenseRepo, CreateExpense(_march, 1000m, Currency.DOP));

        FakeIncomeRepository incomeRepo = new();
        FakeCurrencyConverter converter = new();

        GetMonthSummaryQuery query = new(expenseRepo, incomeRepo, converter);

        Result result = await query.ExecuteAsync(_march);

        MonthSummaryResult summary = Assert.IsType<MonthSummaryResult>(result.Value);
        Assert.Null(summary.ExpensesDeltaPercent);
    }

    [Fact]
    public async Task ExecuteAsync_FxConversionFails_ExcludesBucketAndSetsHasMissingRatesWithoutThrowing()
    {
        FakeExpenseRepository expenseRepo = new();
        await AddExpensesAsync(expenseRepo,
            CreateExpense(_march, 1000m, Currency.DOP),
            CreateExpense(_march, 100m, Currency.USD));

        FakeIncomeRepository incomeRepo = new();
        FakeCurrencyConverter converter = new(onConvert: (_, _) =>
            Result.Failure("ExchangeRateUnavailable", "Provider unavailable."));

        GetMonthSummaryQuery query = new(expenseRepo, incomeRepo, converter);

        Result result = await query.ExecuteAsync(_march);

        Assert.True(result.IsSuccess);
        MonthSummaryResult summary = Assert.IsType<MonthSummaryResult>(result.Value);
        Assert.Equal(1000m, summary.ExpensesDop); // USD bucket excluded, DOP bucket kept
        Assert.True(summary.HasMissingRates);
    }

    [Fact]
    public async Task ExecuteAsync_MonthWithNoTransactions_ReturnsZeroSummaryWithoutThrowing()
    {
        FakeExpenseRepository expenseRepo = new();
        FakeIncomeRepository incomeRepo = new();
        FakeCurrencyConverter converter = new();

        GetMonthSummaryQuery query = new(expenseRepo, incomeRepo, converter);

        Result result = await query.ExecuteAsync(_march);

        MonthSummaryResult summary = Assert.IsType<MonthSummaryResult>(result.Value);
        Assert.Equal(0m, summary.ExpensesDop);
        Assert.Equal(0m, summary.IncomesDop);
        Assert.Equal(0m, summary.NetBalanceDop);
        Assert.Null(summary.ExpensesDeltaPercent);
        Assert.Null(summary.IncomesDeltaPercent);
        Assert.False(summary.HasMissingRates);
    }

    private static async Task AddExpensesAsync(FakeExpenseRepository repo, params Expense[] expenses)
    {
        foreach (Expense expense in expenses)
        {
            await repo.AddAsync(expense);
        }
    }

    private static async Task AddIncomesAsync(FakeIncomeRepository repo, params Income[] incomes)
    {
        foreach (Income income in incomes)
        {
            await repo.AddAsync(income);
        }
    }

    private static Expense CreateExpense(DateOnly date, decimal amount, Currency currency)
        => Expense.Create(
            _categoryId,
            date,
            Money.Create(amount, currency),
            "Test expense",
            null,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    private static Income CreateIncome(DateOnly date, decimal amount, Currency currency)
        => Income.Create(
            null,
            date,
            Money.Create(amount, currency),
            "Test income",
            null,
            null,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
}
