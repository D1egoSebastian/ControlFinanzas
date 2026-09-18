using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthlyEvolution;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for <see cref="DashboardViewModel"/> (F-008, slice 2).
/// Covers R-1/S-2 (month navigation recalculates the summary and both
/// charts) and that a single load populates all three period-dependent
/// widgets from one coherent <c>Task.WhenAll</c> snapshot.
/// </summary>
public sealed class DashboardViewModelTests
{
    private static readonly Guid _categoryId = Guid.NewGuid();
    private static readonly DateOnly _march = new(2026, 3, 15);
    private static readonly DateOnly _marchAnchor = new(2026, 3, 1);
    private static readonly DateOnly _februaryAnchor = new(2026, 2, 1);

    [Fact]
    public async Task RefreshCommand_PopulatesSummaryCategoryBreakdownAndEvolutionTogether()
    {
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.CategoryNames[_categoryId] = "Comida";
        await expenseRepo.AddAsync(CreateExpense(_march, 300m));

        FakeIncomeRepository incomeRepo = new();
        await incomeRepo.AddAsync(CreateIncome(_march, 500m));

        DashboardViewModel viewModel = CreateViewModel(expenseRepo, incomeRepo, _march);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(_marchAnchor, viewModel.SelectedMonth);
        Assert.False(string.IsNullOrEmpty(viewModel.TotalExpensesDisplay));
        Assert.False(string.IsNullOrEmpty(viewModel.TotalIncomesDisplay));
        Assert.Single(viewModel.CategorySlices);
        Assert.Equal(300m, viewModel.CategorySlices[0].AmountDop);
        Assert.Equal(6, viewModel.EvolutionPoints.Count);
        Assert.Equal(2026, viewModel.EvolutionPoints[^1].Year);
        Assert.Equal(3, viewModel.EvolutionPoints[^1].Month);
    }

    [Fact]
    public async Task PreviousMonthCommand_RecalculatesSummaryAndBothCharts()
    {
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.CategoryNames[_categoryId] = "Comida";
        await expenseRepo.AddAsync(CreateExpense(_march, 300m));
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2026, 2, 10), 100m));

        FakeIncomeRepository incomeRepo = new();

        DashboardViewModel viewModel = CreateViewModel(expenseRepo, incomeRepo, _march);
        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(300m, viewModel.CategorySlices[0].AmountDop);

        await viewModel.PreviousMonthCommand.ExecuteAsync(null);

        Assert.Equal(_februaryAnchor, viewModel.SelectedMonth);
        Assert.Single(viewModel.CategorySlices);
        Assert.Equal(100m, viewModel.CategorySlices[0].AmountDop);
        Assert.Equal(6, viewModel.EvolutionPoints.Count);
        Assert.Equal(2, viewModel.EvolutionPoints[^1].Month);
    }

    [Fact]
    public async Task NextMonthCommand_RecalculatesSummaryAndBothCharts()
    {
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.CategoryNames[_categoryId] = "Comida";
        await expenseRepo.AddAsync(CreateExpense(_march, 300m));
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2026, 4, 10), 400m));

        FakeIncomeRepository incomeRepo = new();

        DashboardViewModel viewModel = CreateViewModel(expenseRepo, incomeRepo, _march);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        await viewModel.NextMonthCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 4, 1), viewModel.SelectedMonth);
        Assert.Single(viewModel.CategorySlices);
        Assert.Equal(400m, viewModel.CategorySlices[0].AmountDop);
        Assert.Equal(4, viewModel.EvolutionPoints[^1].Month);
    }

    [Fact]
    public async Task RefreshCommand_FxConversionFails_SetsHasMissingRatesAndDoesNotThrow()
    {
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.CategoryNames[_categoryId] = "Comida";
        await expenseRepo.AddAsync(CreateExpense(_march, 100m, Currency.USD));

        FakeIncomeRepository incomeRepo = new();
        FakeCurrencyConverter converter = new(onConvert: (_, _) =>
            Result.Failure("ExchangeRateUnavailable", "Provider unavailable."));

        DashboardViewModel viewModel = CreateViewModel(expenseRepo, incomeRepo, _march, converter);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasMissingRates);
        Assert.Empty(viewModel.LoadError);
    }

    private static DashboardViewModel CreateViewModel(
        FakeExpenseRepository expenseRepo,
        FakeIncomeRepository incomeRepo,
        DateOnly today,
        FakeCurrencyConverter? converter = null)
    {
        converter ??= new FakeCurrencyConverter();
        FakeClock clock = new() { Today = today };

        GetMonthSummaryQuery summaryQuery = new(expenseRepo, incomeRepo, converter);
        GetCategoryBreakdownQuery breakdownQuery = new(expenseRepo, converter);
        GetMonthlyEvolutionQuery evolutionQuery = new(expenseRepo, incomeRepo, converter);

        return new DashboardViewModel(
            summaryQuery,
            breakdownQuery,
            evolutionQuery,
            clock,
            NullLogger<DashboardViewModel>.Instance);
    }

    private static Expense CreateExpense(DateOnly date, decimal amount, Currency currency = Currency.DOP)
        => Expense.Create(
            _categoryId,
            date,
            Money.Create(amount, currency),
            "Test expense",
            null,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    private static Income CreateIncome(DateOnly date, decimal amount, Currency currency = Currency.DOP)
        => Income.Create(
            null,
            date,
            Money.Create(amount, currency),
            "Test income",
            null,
            null,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
}
