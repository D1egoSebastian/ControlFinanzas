using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Dashboard;

/// <summary>
/// Unit tests for <see cref="GetCategoryBreakdownQuery"/> (F-008, slice 2).
/// Covers R-4/S-4 (top-5 + "Otras"), ties in the ranking, the &lt;5
/// categories case (no "Otras"), reconciliation of slices vs. total, and
/// R-7/S-6 (FX failure excludes the bucket without throwing).
/// </summary>
public sealed class GetCategoryBreakdownQueryTests
{
    private static readonly DateOnly _march = new(2026, 3, 15);

    [Fact]
    public async Task ExecuteAsync_SevenCategories_ReturnsTop5PlusOtras()
    {
        FakeExpenseRepository repo = new();
        Guid[] categoryIds = await AddExpensesAsync(repo,
            ("CategoryA", 700m), ("CategoryB", 600m), ("CategoryC", 500m),
            ("CategoryD", 400m), ("CategoryE", 300m), ("CategoryF", 200m), ("CategoryG", 100m));

        GetCategoryBreakdownQuery query = new(repo, new FakeCurrencyConverter());

        Result result = await query.ExecuteAsync(_march);

        CategoryBreakdownResult breakdown = Assert.IsType<CategoryBreakdownResult>(result.Value);
        Assert.Equal(6, breakdown.Slices.Count); // top 5 + Otras
        Assert.Equal(2800m, breakdown.TotalDop);
        Assert.False(breakdown.HasMissingRates);

        CategorySlice otras = breakdown.Slices[^1];
        Assert.Null(otras.CategoryId);
        Assert.Equal("Otras", otras.Label);
        Assert.Equal(300m, otras.AmountDop); // 200 + 100
        _ = categoryIds;
    }

    [Fact]
    public async Task ExecuteAsync_FewerThanTopCountCategories_ReturnsNoOtrasSlice()
    {
        FakeExpenseRepository repo = new();
        await AddExpensesAsync(repo, ("CategoryA", 300m), ("CategoryB", 200m), ("CategoryC", 100m));

        GetCategoryBreakdownQuery query = new(repo, new FakeCurrencyConverter());

        Result result = await query.ExecuteAsync(_march);

        CategoryBreakdownResult breakdown = Assert.IsType<CategoryBreakdownResult>(result.Value);
        Assert.Equal(3, breakdown.Slices.Count);
        Assert.DoesNotContain(breakdown.Slices, s => s.CategoryId is null);
        Assert.Equal(600m, breakdown.TotalDop);
    }

    [Fact]
    public async Task ExecuteAsync_SlicesSum_ReconcilesWithTotalDop()
    {
        FakeExpenseRepository repo = new();
        await AddExpensesAsync(repo,
            ("CategoryA", 700m), ("CategoryB", 600m), ("CategoryC", 500m),
            ("CategoryD", 400m), ("CategoryE", 300m), ("CategoryF", 200m), ("CategoryG", 100m));

        GetCategoryBreakdownQuery query = new(repo, new FakeCurrencyConverter());

        Result result = await query.ExecuteAsync(_march);

        CategoryBreakdownResult breakdown = Assert.IsType<CategoryBreakdownResult>(result.Value);
        decimal slicesSum = breakdown.Slices.Sum(s => s.AmountDop);
        Assert.Equal(breakdown.TotalDop, slicesSum);
    }

    [Fact]
    public async Task ExecuteAsync_TiedAmountsAtRankingBoundary_IncludesBothDeterministically()
    {
        FakeExpenseRepository repo = new();
        // Six categories, two tied at 500 straddle the top-5 boundary.
        await AddExpensesAsync(repo,
            ("CategoryA", 900m), ("CategoryB", 800m), ("CategoryC", 700m),
            ("CategoryD", 500m), ("CategoryE", 500m), ("CategoryF", 100m));

        GetCategoryBreakdownQuery query = new(repo, new FakeCurrencyConverter());

        Result result = await query.ExecuteAsync(_march);

        CategoryBreakdownResult breakdown = Assert.IsType<CategoryBreakdownResult>(result.Value);
        Assert.Equal(6, breakdown.Slices.Count); // top 5 + Otras
        Assert.Equal(3500m, breakdown.TotalDop);
        decimal slicesSum = breakdown.Slices.Sum(s => s.AmountDop);
        Assert.Equal(breakdown.TotalDop, slicesSum);
    }

    [Fact]
    public async Task ExecuteAsync_FxConversionFails_ExcludesBucketAndSetsHasMissingRatesWithoutThrowing()
    {
        var categoryId = Guid.NewGuid();
        FakeExpenseRepository repo = new();
        repo.CategoryNames[categoryId] = "CategoryA";
        await repo.AddAsync(CreateExpense(categoryId, 500m, Currency.DOP));
        await repo.AddAsync(CreateExpense(categoryId, 100m, Currency.USD));

        FakeCurrencyConverter converter = new(onConvert: (_, _) =>
            Result.Failure("ExchangeRateUnavailable", "Provider unavailable."));
        GetCategoryBreakdownQuery query = new(repo, converter);

        Result result = await query.ExecuteAsync(_march);

        Assert.True(result.IsSuccess);
        CategoryBreakdownResult breakdown = Assert.IsType<CategoryBreakdownResult>(result.Value);
        Assert.True(breakdown.HasMissingRates);
        Assert.Equal(500m, breakdown.TotalDop); // USD bucket excluded
    }

    [Fact]
    public async Task ExecuteAsync_MonthWithNoExpenses_ReturnsEmptySlicesWithoutThrowing()
    {
        FakeExpenseRepository repo = new();
        GetCategoryBreakdownQuery query = new(repo, new FakeCurrencyConverter());

        Result result = await query.ExecuteAsync(_march);

        CategoryBreakdownResult breakdown = Assert.IsType<CategoryBreakdownResult>(result.Value);
        Assert.Empty(breakdown.Slices);
        Assert.Equal(0m, breakdown.TotalDop);
        Assert.False(breakdown.HasMissingRates);
    }

    private static async Task<Guid[]> AddExpensesAsync(
        FakeExpenseRepository repo, params (string Name, decimal Amount)[] categories)
    {
        var ids = new Guid[categories.Length];
        for (int i = 0; i < categories.Length; i++)
        {
            var categoryId = Guid.NewGuid();
            ids[i] = categoryId;
            repo.CategoryNames[categoryId] = categories[i].Name;
            await repo.AddAsync(CreateExpense(categoryId, categories[i].Amount, Currency.DOP));
        }

        return ids;
    }

    private static Expense CreateExpense(Guid categoryId, decimal amount, Currency currency)
        => Expense.Create(
            categoryId,
            _march,
            Money.Create(amount, currency),
            "Test expense",
            null,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
}
