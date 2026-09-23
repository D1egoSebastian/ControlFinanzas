using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthEndForecast;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Forecasting;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Dashboard;

/// <summary>
/// Unit tests for <see cref="GetMonthEndForecastUseCase"/> (F-014, slice 3,
/// D-6). The domain math itself (day-1 blend, band edges, rounding) is
/// covered by <c>MonthEndForecasterTests</c>; these tests cover the use
/// case's wiring: which range is queried per state, currency
/// normalization/poisoning, the trailing-3-month history average
/// (excluding zero-spend months), and the budget comparison (S-7/S-8).
/// </summary>
public sealed class GetMonthEndForecastUseCaseTests
{
    private static readonly Guid _categoryId = Guid.NewGuid();
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_FutureMonth_ReturnsFutureStateWithoutQueryingBudgets()
    {
        FakeExpenseRepository expenseRepo = new();
        FakeBudgetRepository budgetRepo = new();
        FakeCurrencyConverter converter = new();
        FakeClock clock = new() { Today = new DateOnly(2026, 3, 10) };

        GetMonthEndForecastUseCase useCase = new(expenseRepo, budgetRepo, converter, clock);

        Result result = await useCase.ExecuteAsync(new DateOnly(2026, 4, 1));

        Assert.True(result.IsSuccess);
        MonthEndForecastDto dto = Assert.IsType<MonthEndForecastDto>(result.Value);
        Assert.Equal(ForecastState.FutureMonth, dto.State);
        Assert.Null(dto.ProjectedDop);
        Assert.Null(dto.BudgetTotalDop);
        Assert.False(dto.HasMissingRates);
        Assert.Equal(0, budgetRepo.GetByMonthCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ClosedMonth_ProjectsFullMonthSpendAsActual()
    {
        FakeExpenseRepository expenseRepo = new();
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2026, 2, 5), 400m, Currency.DOP));
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2026, 2, 27), 600m, Currency.DOP));
        FakeBudgetRepository budgetRepo = new();
        FakeCurrencyConverter converter = new();
        FakeClock clock = new() { Today = new DateOnly(2026, 3, 10) };

        GetMonthEndForecastUseCase useCase = new(expenseRepo, budgetRepo, converter, clock);

        Result result = await useCase.ExecuteAsync(new DateOnly(2026, 2, 1));

        MonthEndForecastDto dto = Assert.IsType<MonthEndForecastDto>(result.Value);
        Assert.Equal(ForecastState.ClosedMonth, dto.State);
        Assert.Equal(1000m, dto.SpentToDateDop);
        Assert.Equal(1000m, dto.ProjectedDop);
    }

    [Fact]
    public async Task ExecuteAsync_CurrentMonth_SpentToDateExcludesFutureDatedExpenses()
    {
        FakeExpenseRepository expenseRepo = new();
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2026, 3, 5), 500m, Currency.DOP));
        // Dated later in the same calendar month, but AFTER "today" — must not
        // count as "spent to date" (design D-6 step 3).
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2026, 3, 20), 999m, Currency.DOP));
        FakeBudgetRepository budgetRepo = new();
        FakeCurrencyConverter converter = new();
        FakeClock clock = new() { Today = new DateOnly(2026, 3, 10) };

        GetMonthEndForecastUseCase useCase = new(expenseRepo, budgetRepo, converter, clock);

        Result result = await useCase.ExecuteAsync(new DateOnly(2026, 3, 1));

        MonthEndForecastDto dto = Assert.IsType<MonthEndForecastDto>(result.Value);
        Assert.Equal(500m, dto.SpentToDateDop);
    }

    [Fact]
    public async Task ExecuteAsync_NoHistoryAndElapsedLessThanThree_ReturnsInsufficientData()
    {
        FakeExpenseRepository expenseRepo = new();
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2026, 3, 1), 100m, Currency.DOP));
        FakeBudgetRepository budgetRepo = new();
        FakeCurrencyConverter converter = new();
        FakeClock clock = new() { Today = new DateOnly(2026, 3, 2) };

        GetMonthEndForecastUseCase useCase = new(expenseRepo, budgetRepo, converter, clock);

        Result result = await useCase.ExecuteAsync(new DateOnly(2026, 3, 1));

        MonthEndForecastDto dto = Assert.IsType<MonthEndForecastDto>(result.Value);
        Assert.Equal(ForecastState.InsufficientData, dto.State);
        Assert.Null(dto.ProjectedDop);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroSpendHistoryMonth_IsExcludedFromTrailingAverage()
    {
        FakeExpenseRepository expenseRepo = new();
        // Trailing 3 full months before March 2026: Dec 2025, Jan 2026, Feb 2026.
        // Jan is deliberately left with zero expenses.
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2025, 12, 10), 6200m, Currency.DOP)); // 31 days * 200/day
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2026, 2, 10), 5600m, Currency.DOP));  // 28 days * 200/day
        // Current month spend, small so the blend is dominated by history.
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2026, 3, 5), 500m, Currency.DOP));

        FakeBudgetRepository budgetRepo = new();
        FakeCurrencyConverter converter = new();
        FakeClock clock = new() { Today = new DateOnly(2026, 3, 10) };

        GetMonthEndForecastUseCase useCase = new(expenseRepo, budgetRepo, converter, clock);

        Result result = await useCase.ExecuteAsync(new DateOnly(2026, 3, 1));

        MonthEndForecastDto dto = Assert.IsType<MonthEndForecastDto>(result.Value);

        // If Jan's zero-spend month were wrongly included, the denominator
        // would be 31+31+28=90 days instead of 31+28=59, giving a different
        // historical average and therefore a different projection. Assert
        // against the domain function called with the EXPECTED (correct)
        // history average — this is what the use case must pass it.
        MonthEndForecast expected = MonthEndForecaster.Project(
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 10), 500m, 200m);

        Assert.Equal(expected.ProjectedDop, dto.ProjectedDop);
    }

    [Fact]
    public async Task ExecuteAsync_BudgetPresent_IncludesProjectedVsBudgetComparison()
    {
        FakeExpenseRepository expenseRepo = new();
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2026, 3, 5), 1000m, Currency.DOP));

        FakeBudgetRepository budgetRepo = new();
        budgetRepo.Seed(Budget.Create(2026, 3, _categoryId, Money.Create(5000m, Currency.DOP)));

        FakeCurrencyConverter converter = new();
        FakeClock clock = new() { Today = new DateOnly(2026, 3, 10) };

        GetMonthEndForecastUseCase useCase = new(expenseRepo, budgetRepo, converter, clock);

        Result result = await useCase.ExecuteAsync(new DateOnly(2026, 3, 1));

        MonthEndForecastDto dto = Assert.IsType<MonthEndForecastDto>(result.Value);
        Assert.NotNull(dto.ProjectedDop);
        Assert.Equal(5000m, dto.BudgetTotalDop);

        BudgetEvaluation expectedEvaluation = BudgetEvaluator.Evaluate(5000m, dto.ProjectedDop!.Value);
        Assert.Equal(expectedEvaluation.PercentConsumed, dto.ProjectedPercentOfBudget);
        Assert.Equal(expectedEvaluation.Status, dto.ProjectedBudgetStatus);
    }

    [Fact]
    public async Task ExecuteAsync_NoBudget_OmitsBudgetComparisonFields()
    {
        FakeExpenseRepository expenseRepo = new();
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2026, 3, 5), 1000m, Currency.DOP));
        FakeBudgetRepository budgetRepo = new();
        FakeCurrencyConverter converter = new();
        FakeClock clock = new() { Today = new DateOnly(2026, 3, 10) };

        GetMonthEndForecastUseCase useCase = new(expenseRepo, budgetRepo, converter, clock);

        Result result = await useCase.ExecuteAsync(new DateOnly(2026, 3, 1));

        MonthEndForecastDto dto = Assert.IsType<MonthEndForecastDto>(result.Value);
        Assert.Null(dto.BudgetTotalDop);
        Assert.Null(dto.ProjectedPercentOfBudget);
        Assert.Null(dto.ProjectedBudgetStatus);
        Assert.NotNull(dto.ProjectedDop);
    }

    [Fact]
    public async Task ExecuteAsync_BudgetConversionFails_PoisonsBudgetTotalAndSetsHasMissingRatesWithoutThrowing()
    {
        FakeExpenseRepository expenseRepo = new();
        await expenseRepo.AddAsync(CreateExpense(new DateOnly(2026, 3, 5), 1000m, Currency.DOP));

        FakeBudgetRepository budgetRepo = new();
        budgetRepo.Seed(Budget.Create(2026, 3, _categoryId, Money.Create(100m, Currency.USD)));

        FakeCurrencyConverter converter = new(onConvert: (money, _) =>
            money.Currency == Currency.USD
                ? Result.Failure("ExchangeRateUnavailable", "Provider unavailable.")
                : Result.Success(money));
        FakeClock clock = new() { Today = new DateOnly(2026, 3, 10) };

        GetMonthEndForecastUseCase useCase = new(expenseRepo, budgetRepo, converter, clock);

        Result result = await useCase.ExecuteAsync(new DateOnly(2026, 3, 1));

        Assert.True(result.IsSuccess);
        MonthEndForecastDto dto = Assert.IsType<MonthEndForecastDto>(result.Value);
        Assert.Null(dto.BudgetTotalDop);
        Assert.Null(dto.ProjectedPercentOfBudget);
        Assert.Null(dto.ProjectedBudgetStatus);
        Assert.True(dto.HasMissingRates);
        // The unrelated DOP spend must not be poisoned by the budget's failed conversion.
        Assert.Equal(1000m, dto.SpentToDateDop);
    }

    private static Expense CreateExpense(DateOnly date, decimal amount, Currency currency)
        => Expense.Create(
            _categoryId,
            date,
            Money.Create(amount, currency),
            "Test expense",
            null,
            _fixedTimestamp);
}
