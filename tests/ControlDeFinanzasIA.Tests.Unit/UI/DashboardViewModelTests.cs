using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthlyEvolution;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
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

    [Fact]
    public async Task NavigatingMonths_DoesNotReloadOrChangeUpcomingPayments()
    {
        FakeExpenseRepository expenseRepo = new();
        FakeIncomeRepository incomeRepo = new();

        FakeDebtRepository debtRepo = new();
        var debt = Debt.Create(
            "Préstamo carro",
            Money.Create(1000m, Currency.DOP),
            Currency.DOP,
            totalInstallments: 1,
            installmentAmount: Money.Create(1000m, Currency.DOP),
            frequency: nameof(DebtFrequency.Monthly),
            startDate: _march.AddDays(5));
        debtRepo.Seed(debt);
        FakeDebtPaymentRepository debtPaymentRepo = new();
        debtPaymentRepo.SeedRange(new DebtScheduleGenerator().Generate(debt));

        DashboardViewModel viewModel = CreateViewModel(
            expenseRepo, incomeRepo, _march, debtPaymentRepo: debtPaymentRepo, debtRepo: debtRepo);

        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.Single(viewModel.UpcomingPayments);
        UpcomingPaymentItem original = viewModel.UpcomingPayments[0];

        await viewModel.PreviousMonthCommand.ExecuteAsync(null);
        await viewModel.NextMonthCommand.ExecuteAsync(null);
        await viewModel.NextMonthCommand.ExecuteAsync(null);

        Assert.Single(viewModel.UpcomingPayments);
        Assert.Equal(original, viewModel.UpcomingPayments[0]);
    }

    [Fact]
    public async Task RefreshCommand_PopulatesBudgetProgress_FromBudgetVsActualQuery()
    {
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.CategoryNames[_categoryId] = "Comida";
        await expenseRepo.AddAsync(CreateExpense(_march, 3000m));

        FakeIncomeRepository incomeRepo = new();

        FakeBudgetRepository budgetRepo = new();
        budgetRepo.Seed(Budget.Create(2026, 3, _categoryId, Money.Create(10000m, Currency.DOP)));

        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories(new[] { new Category { Id = _categoryId, Name = "Comida" } });

        DashboardViewModel viewModel = CreateViewModel(
            expenseRepo, incomeRepo, _march, budgetRepo: budgetRepo, categoryRepo: categoryRepo);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        BudgetProgressItem item = Assert.Single(viewModel.BudgetProgress);
        Assert.Equal("Comida", item.CategoryName);
        Assert.Equal(BudgetStatus.Ok, item.Status);
        Assert.False(item.IsIndeterminate);
        Assert.Equal("30.0%", item.PercentDisplay);
        Assert.Equal(30d, item.ProgressValue);
    }

    [Fact]
    public async Task RefreshCommand_OverBudgetCategory_ClampsProgressValueButKeepsTruePercentDisplay()
    {
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.CategoryNames[_categoryId] = "Comida";
        await expenseRepo.AddAsync(CreateExpense(_march, 20000m));

        FakeIncomeRepository incomeRepo = new();

        FakeBudgetRepository budgetRepo = new();
        budgetRepo.Seed(Budget.Create(2026, 3, _categoryId, Money.Create(15000m, Currency.DOP)));

        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories(new[] { new Category { Id = _categoryId, Name = "Comida" } });

        DashboardViewModel viewModel = CreateViewModel(
            expenseRepo, incomeRepo, _march, budgetRepo: budgetRepo, categoryRepo: categoryRepo);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        BudgetProgressItem item = Assert.Single(viewModel.BudgetProgress);
        Assert.Equal(BudgetStatus.Exceeded, item.Status);
        Assert.True(item.IsExceeded);
        Assert.Equal("133.3%", item.PercentDisplay); // true, unclamped percent
        Assert.Equal(100d, item.ProgressValue);      // clamped, the bar can't overflow its track
    }

    [Fact]
    public async Task RefreshCommand_NearThresholdCategory_MapsToIsNear()
    {
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.CategoryNames[_categoryId] = "Comida";
        await expenseRepo.AddAsync(CreateExpense(_march, 8000m));

        FakeIncomeRepository incomeRepo = new();

        FakeBudgetRepository budgetRepo = new();
        budgetRepo.Seed(Budget.Create(2026, 3, _categoryId, Money.Create(10000m, Currency.DOP)));

        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories(new[] { new Category { Id = _categoryId, Name = "Comida" } });

        DashboardViewModel viewModel = CreateViewModel(
            expenseRepo, incomeRepo, _march, budgetRepo: budgetRepo, categoryRepo: categoryRepo);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        BudgetProgressItem item = Assert.Single(viewModel.BudgetProgress);
        Assert.Equal(BudgetStatus.Near, item.Status);
        Assert.True(item.IsNear);
        Assert.False(item.IsExceeded);
    }

    [Fact]
    public async Task RefreshCommand_BudgetConversionFails_RendersIndeterminateAndSetsHasMissingRates()
    {
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.CategoryNames[_categoryId] = "Comida";
        await expenseRepo.AddAsync(CreateExpense(_march, 2000m)); // DOP, converts fine

        FakeIncomeRepository incomeRepo = new();

        FakeBudgetRepository budgetRepo = new();
        budgetRepo.Seed(Budget.Create(2026, 3, _categoryId, Money.Create(10000m, Currency.USD)));

        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories(new[] { new Category { Id = _categoryId, Name = "Comida" } });

        // Only USD conversions fail; the DOP expense side still converts.
        FakeCurrencyConverter converter = new(onConvert: (money, target) =>
            money.Currency == Currency.USD
                ? Result.Failure("ExchangeRateUnavailable", "No rate.")
                : Result.Success(Money.Create(money.Amount, target)));

        DashboardViewModel viewModel = CreateViewModel(
            expenseRepo, incomeRepo, _march, converter, budgetRepo: budgetRepo, categoryRepo: categoryRepo);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        BudgetProgressItem item = Assert.Single(viewModel.BudgetProgress);
        Assert.True(item.IsIndeterminate);
        Assert.Null(item.Status);
        Assert.False(item.IsNear);
        Assert.False(item.IsExceeded);
        Assert.Equal(0d, item.ProgressValue);
        Assert.Equal(Strings.DashboardBudgetIndeterminateLabel, item.PercentDisplay);
        Assert.True(viewModel.HasMissingRates);
    }

    [Fact]
    public async Task PreviousMonthCommand_RecalculatesBudgetProgressForNavigatedMonth()
    {
        FakeExpenseRepository expenseRepo = new();
        expenseRepo.CategoryNames[_categoryId] = "Comida";
        await expenseRepo.AddAsync(CreateExpense(_march, 3000m));
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2026, 2, 10), 1000m));

        FakeIncomeRepository incomeRepo = new();

        FakeBudgetRepository budgetRepo = new();
        budgetRepo.Seed(Budget.Create(2026, 3, _categoryId, Money.Create(10000m, Currency.DOP)));
        budgetRepo.Seed(Budget.Create(2026, 2, _categoryId, Money.Create(5000m, Currency.DOP)));

        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories(new[] { new Category { Id = _categoryId, Name = "Comida" } });

        DashboardViewModel viewModel = CreateViewModel(
            expenseRepo, incomeRepo, _march, budgetRepo: budgetRepo, categoryRepo: categoryRepo);

        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(30d, Assert.Single(viewModel.BudgetProgress).ProgressValue); // 3000/10000

        await viewModel.PreviousMonthCommand.ExecuteAsync(null);

        Assert.Equal(_februaryAnchor, viewModel.SelectedMonth);
        BudgetProgressItem febItem = Assert.Single(viewModel.BudgetProgress);
        Assert.Equal(20d, febItem.ProgressValue); // 1000/5000
    }

    private static DashboardViewModel CreateViewModel(
        FakeExpenseRepository expenseRepo,
        FakeIncomeRepository incomeRepo,
        DateOnly today,
        FakeCurrencyConverter? converter = null,
        FakeDebtPaymentRepository? debtPaymentRepo = null,
        FakeDebtRepository? debtRepo = null,
        FakeRecurringRuleRepository? recurringRuleRepo = null,
        FakeBudgetRepository? budgetRepo = null,
        FakeCategoryRepository? categoryRepo = null)
    {
        converter ??= new FakeCurrencyConverter();
        FakeClock clock = new() { Today = today };

        GetMonthSummaryQuery summaryQuery = new(expenseRepo, incomeRepo, converter);
        GetCategoryBreakdownQuery breakdownQuery = new(expenseRepo, converter);
        GetMonthlyEvolutionQuery evolutionQuery = new(expenseRepo, incomeRepo, converter);
        GetUpcomingPaymentsQuery upcomingPaymentsQuery = new(
            debtPaymentRepo ?? new FakeDebtPaymentRepository(),
            debtRepo ?? new FakeDebtRepository(),
            recurringRuleRepo ?? new FakeRecurringRuleRepository(),
            clock);
        GetBudgetVsActualQuery budgetVsActualQuery = new(
            budgetRepo ?? new FakeBudgetRepository(),
            expenseRepo,
            categoryRepo ?? new FakeCategoryRepository(),
            converter);

        return new DashboardViewModel(
            summaryQuery,
            breakdownQuery,
            evolutionQuery,
            upcomingPaymentsQuery,
            budgetVsActualQuery,
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
