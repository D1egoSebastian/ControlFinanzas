using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthlyEvolution;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Dashboard;

/// <summary>
/// Unit tests for <see cref="GetMonthlyEvolutionQuery"/> (F-008, slice 2).
/// Covers R-5 (6-month trailing window with zero-fill) and R-7/S-6 (FX
/// failure excludes the bucket without throwing).
/// </summary>
public sealed class GetMonthlyEvolutionQueryTests
{
    private static readonly Guid _categoryId = Guid.NewGuid();
    private static readonly DateOnly _march = new(2026, 3, 15);

    [Fact]
    public async Task ExecuteAsync_OnlyTwoMonthsHaveData_ReturnsSixZeroFilledPointsInOrder()
    {
        FakeExpenseRepository expenseRepo = new();
        FakeIncomeRepository incomeRepo = new();

        // Window for anchor=March 2026, 6 months: Oct 2025 .. Mar 2026.
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2025, 10, 5), 100m, Currency.DOP));
        await incomeRepo.AddAsync(CreateIncome(new DateOnly(2026, 1, 12), 50m, Currency.DOP));

        GetMonthlyEvolutionQuery query = new(expenseRepo, incomeRepo, new FakeCurrencyConverter());

        Result result = await query.ExecuteAsync(_march);

        MonthlyEvolutionResult evolution = Assert.IsType<MonthlyEvolutionResult>(result.Value);
        Assert.Equal(6, evolution.Points.Count);
        Assert.False(evolution.HasMissingRates);

        (int Year, int Month)[] expectedOrder =
        [
            (2025, 10), (2025, 11), (2025, 12), (2026, 1), (2026, 2), (2026, 3),
        ];
        for (int i = 0; i < expectedOrder.Length; i++)
        {
            Assert.Equal(expectedOrder[i].Year, evolution.Points[i].Year);
            Assert.Equal(expectedOrder[i].Month, evolution.Points[i].Month);
        }

        EvolutionPoint october = evolution.Points[0];
        Assert.Equal(100m, october.ExpensesDop);
        Assert.Equal(0m, october.IncomesDop);

        EvolutionPoint january = evolution.Points[3];
        Assert.Equal(0m, january.ExpensesDop);
        Assert.Equal(50m, january.IncomesDop);

        // Months with no rows are zero-filled, not omitted.
        EvolutionPoint november = evolution.Points[1];
        Assert.Equal(0m, november.ExpensesDop);
        Assert.Equal(0m, november.IncomesDop);
    }

    [Fact]
    public async Task ExecuteAsync_NoDataAtAll_ReturnsSixZeroPointsWithoutThrowing()
    {
        FakeExpenseRepository expenseRepo = new();
        FakeIncomeRepository incomeRepo = new();

        GetMonthlyEvolutionQuery query = new(expenseRepo, incomeRepo, new FakeCurrencyConverter());

        Result result = await query.ExecuteAsync(_march);

        MonthlyEvolutionResult evolution = Assert.IsType<MonthlyEvolutionResult>(result.Value);
        Assert.Equal(6, evolution.Points.Count);
        Assert.All(evolution.Points, p => Assert.Equal(0m, p.ExpensesDop));
        Assert.All(evolution.Points, p => Assert.Equal(0m, p.IncomesDop));
    }

    [Fact]
    public async Task ExecuteAsync_FxConversionFails_ExcludesBucketAndSetsHasMissingRatesWithoutThrowing()
    {
        FakeExpenseRepository expenseRepo = new();
        FakeIncomeRepository incomeRepo = new();
        await expenseRepo.AddAsync(CreateExpense(_march, 1000m, Currency.DOP));
        await expenseRepo.AddAsync(CreateExpense(_march, 100m, Currency.USD));

        FakeCurrencyConverter converter = new(onConvert: (_, _) =>
            Result.Failure("ExchangeRateUnavailable", "Provider unavailable."));
        GetMonthlyEvolutionQuery query = new(expenseRepo, incomeRepo, converter);

        Result result = await query.ExecuteAsync(_march);

        Assert.True(result.IsSuccess);
        MonthlyEvolutionResult evolution = Assert.IsType<MonthlyEvolutionResult>(result.Value);
        Assert.True(evolution.HasMissingRates);
        EvolutionPoint marchPoint = evolution.Points[^1];
        Assert.Equal(1000m, marchPoint.ExpensesDop); // USD bucket excluded
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
