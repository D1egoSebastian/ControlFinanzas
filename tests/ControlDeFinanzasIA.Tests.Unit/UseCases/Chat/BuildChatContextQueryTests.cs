using System.Reflection;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Chat.BuildChatContext;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebts;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebtSchedule;
using ControlDeFinanzasIA.Application.UseCases.Goals.GetGoals;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UseCases.Chat;

/// <summary>
/// Tests for <see cref="BuildChatContextQuery"/> (F-015, Slice S1, R-2,
/// D-1, D-2, D-3, S-1, S-9, S-10). Covers exact per-section caps and
/// folding, per-section degradation on a throwing dependency, caller
/// cancellation, missing exchange rates, and the privacy shape of every
/// snapshot record.
/// </summary>
public sealed class BuildChatContextQueryTests
{
    private static readonly DateOnly _today = new(2026, 9, 20);
    private static readonly DateOnly _currentMonthAnchor = new(2026, 9, 1);
    private static readonly DateOnly _priorMonthAnchor = new(2026, 8, 1);

    private sealed record Sut(
        BuildChatContextQuery Query,
        FakeExpenseRepository ExpenseRepo,
        FakeIncomeRepository IncomeRepo,
        FakeBudgetRepository BudgetRepo,
        FakeCategoryRepository CategoryRepo,
        FakeGoalRepository GoalRepo,
        FakeGoalContributionRepository GoalContributionRepo,
        FakeDebtRepository DebtRepo,
        FakeDebtPaymentRepository DebtPaymentRepo,
        FakeRecurringRuleRepository RecurringRuleRepo,
        FakeCurrencyConverter Converter,
        FakeClock Clock,
        RecordingLogger<BuildChatContextQuery> Logger);

    private static Sut CreateSut(
        FakeCurrencyConverter? converter = null,
        FakeClock? clock = null,
        GetMonthSummaryQuery? monthSummaryOverride = null,
        GetCategoryBreakdownQuery? categoryBreakdownOverride = null,
        GetBudgetVsActualQuery? budgetVsActualOverride = null,
        GetGoalsQuery? goalsOverride = null,
        GetDebtsQuery? debtsOverride = null,
        GetUpcomingPaymentsQuery? upcomingPaymentsOverride = null)
    {
        var expenseRepo = new FakeExpenseRepository();
        var incomeRepo = new FakeIncomeRepository();
        var budgetRepo = new FakeBudgetRepository();
        var categoryRepo = new FakeCategoryRepository();
        var goalRepo = new FakeGoalRepository();
        var goalContributionRepo = new FakeGoalContributionRepository();
        var debtRepo = new FakeDebtRepository();
        var debtPaymentRepo = new FakeDebtPaymentRepository();
        var recurringRuleRepo = new FakeRecurringRuleRepository();
        converter ??= new FakeCurrencyConverter();
        clock ??= new FakeClock { Today = _today };

        GetMonthSummaryQuery monthSummary = monthSummaryOverride
            ?? new GetMonthSummaryQuery(expenseRepo, incomeRepo, converter);
        GetCategoryBreakdownQuery categoryBreakdown = categoryBreakdownOverride
            ?? new GetCategoryBreakdownQuery(expenseRepo, converter);
        GetBudgetVsActualQuery budgetVsActual = budgetVsActualOverride
            ?? new GetBudgetVsActualQuery(budgetRepo, expenseRepo, categoryRepo, converter);
        GetGoalsQuery goals = goalsOverride
            ?? new GetGoalsQuery(goalRepo, goalContributionRepo, clock);
        GetDebtsQuery debts = debtsOverride
            ?? new GetDebtsQuery(debtRepo);
        var debtSchedule = new GetDebtScheduleQuery(debtRepo, debtPaymentRepo);
        GetUpcomingPaymentsQuery upcomingPayments = upcomingPaymentsOverride
            ?? new GetUpcomingPaymentsQuery(debtPaymentRepo, debtRepo, recurringRuleRepo, clock);

        var logger = new RecordingLogger<BuildChatContextQuery>();

        var query = new BuildChatContextQuery(
            monthSummary, categoryBreakdown, budgetVsActual, goals, debts, debtSchedule, upcomingPayments,
            converter, clock, logger);

        return new Sut(
            query, expenseRepo, incomeRepo, budgetRepo, categoryRepo, goalRepo, goalContributionRepo,
            debtRepo, debtPaymentRepo, recurringRuleRepo, converter, clock, logger);
    }

    private static async Task<Guid> SeedCategoryExpenseAsync(
        FakeExpenseRepository repo, string name, decimal amount, DateOnly date)
    {
        var categoryId = Guid.NewGuid();
        repo.CategoryNames[categoryId] = name;
        await repo.AddAsync(Expense.Create(
            categoryId, date, Money.Create(amount, Currency.DOP), name, null, DateTime.UtcNow));
        return categoryId;
    }

    private static ChatContextSnapshot ExecuteSuccessfully(Result result)
    {
        Assert.True(result.IsSuccess);
        return Assert.IsType<ChatContextSnapshot>(result.Value);
    }

    // ------------------------------------------------------------------
    // S-1: a category beyond the Dashboard's top-5 default is still exact
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_CategoryRankedSixthByAmount_KeepsExactAmountBeyondDashboardTop5()
    {
        Sut sut = CreateSut();
        await SeedCategoryExpenseAsync(sut.ExpenseRepo, "Alpha", 500m, _currentMonthAnchor);
        await SeedCategoryExpenseAsync(sut.ExpenseRepo, "Beta", 400m, _currentMonthAnchor);
        await SeedCategoryExpenseAsync(sut.ExpenseRepo, "Gamma", 300m, _currentMonthAnchor);
        await SeedCategoryExpenseAsync(sut.ExpenseRepo, "Delta", 200m, _currentMonthAnchor);
        await SeedCategoryExpenseAsync(sut.ExpenseRepo, "Epsilon", 100m, _currentMonthAnchor);
        await SeedCategoryExpenseAsync(sut.ExpenseRepo, "Comida", 50m, _currentMonthAnchor);

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.NotNull(snapshot.CurrentCategories);
        Assert.Equal(6, snapshot.CurrentCategories!.Lines.Count);
        Assert.Equal(0, snapshot.CurrentCategories.OverflowCount);
        ChatCategoryLine comida = Assert.Single(snapshot.CurrentCategories.Lines, l => l.Name == "Comida");
        Assert.Equal(50m, comida.AmountDop);
    }

    [Fact]
    public async Task ExecuteAsync_31CurrentMonthCategories_Keeps30NamedLinesPlusExactOverflowLine()
    {
        Sut sut = CreateSut();

        // 31 categories, descending amount: rank 30 is "Comida" (still named),
        // rank 31 is "Otro" (folds into the overflow line).
        for (int i = 0; i < 30; i++)
        {
            decimal amount = 3100m - (i * 100m);
            string name = i == 29 ? "Comida" : $"Cat{i:D2}";
            await SeedCategoryExpenseAsync(sut.ExpenseRepo, name, amount, _currentMonthAnchor);
        }

        await SeedCategoryExpenseAsync(sut.ExpenseRepo, "Otro", 100m, _currentMonthAnchor);

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.NotNull(snapshot.CurrentCategories);
        Assert.Equal(30, snapshot.CurrentCategories!.Lines.Count);
        Assert.Equal(1, snapshot.CurrentCategories.OverflowCount);
        Assert.Equal(100m, snapshot.CurrentCategories.OverflowAmountDop);
        Assert.Equal(49_600m, snapshot.CurrentCategories.TotalDop);

        ChatCategoryLine comida = Assert.Single(snapshot.CurrentCategories.Lines, l => l.Name == "Comida");
        Assert.Equal(200m, comida.AmountDop);
        Assert.DoesNotContain(snapshot.CurrentCategories.Lines, l => l.Name == "Otro");
    }

    [Fact]
    public async Task ExecuteAsync_12PriorMonthCategories_CapsAt10WithExactOverflow()
    {
        Sut sut = CreateSut();
        for (int i = 0; i < 12; i++)
        {
            await SeedCategoryExpenseAsync(sut.ExpenseRepo, $"Prior{i:D2}", 1000m - (i * 10m), _priorMonthAnchor);
        }

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.NotNull(snapshot.PriorCategories);
        Assert.Equal(10, snapshot.PriorCategories!.Lines.Count);
        Assert.Equal(2, snapshot.PriorCategories.OverflowCount);
    }

    // ------------------------------------------------------------------
    // Budgets: cap at 15, in the query's own order
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_16Budgets_CapsAt15InQueryOrder()
    {
        Sut sut = CreateSut();
        for (int i = 1; i <= 16; i++)
        {
            var categoryId = Guid.NewGuid();
            string name = $"Cat{i:D2}";
            sut.CategoryRepo.Categories = sut.CategoryRepo.Categories
                .Append(new Category { Id = categoryId, Name = name })
                .ToList();
            sut.BudgetRepo.Seed(Budget.Create(2026, 9, categoryId, Money.Create(1_000m, Currency.DOP)));
        }

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.NotNull(snapshot.Budgets);
        Assert.Equal(15, snapshot.Budgets!.Count);

        // All lines are 0% consumed (no expenses seeded), so the query's
        // tie-break by CategoryName ordinal determines the order: Cat01..Cat15.
        Assert.Equal("Cat01", snapshot.Budgets[0].CategoryName);
        Assert.Equal("Cat15", snapshot.Budgets[14].CategoryName);
        Assert.DoesNotContain(snapshot.Budgets, b => b.CategoryName == "Cat16");
    }

    // ------------------------------------------------------------------
    // Goals: Active only, capped at 10
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_12ActiveGoalsPlusCompleted_KeepsOnlyActiveCappedAt10()
    {
        Sut sut = CreateSut();
        for (int i = 0; i < 12; i++)
        {
            var goal = Goal.Create($"Meta{i:D2}", Money.Create(10_000m, Currency.DOP), new DateOnly(2027, 1 + (i % 12), 1));
            sut.GoalRepo.Seed(goal);
        }

        var completedGoal = Goal.Create("Completada", Money.Create(5_000m, Currency.DOP), new DateOnly(2026, 1, 1));
        var fullContribution = GoalContribution.Create(completedGoal.Id, Money.Create(5_000m, Currency.DOP), new DateOnly(2026, 1, 1));
        sut.GoalContributionRepo.Seed(fullContribution);
        completedGoal.TryAutoComplete(new[] { fullContribution });
        sut.GoalRepo.Seed(completedGoal);

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.NotNull(snapshot.Goals);
        Assert.Equal(10, snapshot.Goals!.Count);
        Assert.DoesNotContain(snapshot.Goals, g => g.Name == "Completada");
    }

    // ------------------------------------------------------------------
    // Debts: Active only, capped at 10, pending balance + next due date
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_12ActiveDebtsPlusClosed_KeepsOnlyActiveCappedAt10OrderedByDescription()
    {
        Sut sut = CreateSut();
        var closedDebt = Debt.Create(
            "ZZZClosed", Money.Create(1_000m, Currency.DOP), Currency.DOP, 1, Money.Create(1_000m, Currency.DOP),
            nameof(DebtFrequency.Monthly), new DateOnly(2026, 1, 1));
        sut.DebtRepo.Seed(closedDebt);

        for (int i = 1; i <= 12; i++)
        {
            var debt = Debt.Create(
                $"Debt{i:D2}", Money.Create(10_000m, Currency.DOP), Currency.DOP, 1, Money.Create(1_000m, Currency.DOP),
                nameof(DebtFrequency.Monthly), new DateOnly(2026, 1, 1));
            sut.DebtRepo.Seed(debt);
        }

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.NotNull(snapshot.Debts);
        Assert.Equal(10, snapshot.Debts!.Count);
        Assert.Equal("Debt01", snapshot.Debts[0].Name);
        Assert.Equal("Debt10", snapshot.Debts[9].Name);
        Assert.DoesNotContain(snapshot.Debts, d => d.Name is "Debt11" or "Debt12" or "ZZZClosed");
    }

    [Fact]
    public async Task ExecuteAsync_DebtWithPartialPayments_ComputesPendingBalanceAndNextDueDateFromDomain()
    {
        Sut sut = CreateSut();
        var debt = Debt.Create(
            "Préstamo carro", Money.Create(120_000m, Currency.DOP), Currency.DOP, 12, Money.Create(10_000m, Currency.DOP),
            nameof(DebtFrequency.Monthly), new DateOnly(2026, 1, 1));
        sut.DebtRepo.Seed(debt);

        IReadOnlyList<DebtPayment> schedule = new DebtScheduleGenerator().Generate(debt);
        schedule[0].MarkAsPaid(new DateOnly(2026, 1, 1));
        schedule[1].MarkAsPaid(new DateOnly(2026, 2, 1));
        sut.DebtPaymentRepo.SeedRange(schedule);

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.NotNull(snapshot.Debts);
        ChatDebtLine line = Assert.Single(snapshot.Debts!);
        Assert.Equal(100_000m, line.PendingBalance.AmountDop);
        Assert.Equal(10_000m, line.Installment.AmountDop);
        Assert.Equal(10, line.PendingInstallments);
        Assert.Equal(new DateOnly(2026, 3, 1), line.NextDueDate);
    }

    // ------------------------------------------------------------------
    // Upcoming payments: the query's own maxItems cap flows through
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_12UpcomingDebtPayments_CapsAt10()
    {
        Sut sut = CreateSut();

        // 12 single-installment debts, each due on a distinct day inside the
        // 30-day horizon: GetDebtScheduleGenerator (Domain) is the only
        // public path to a valid DebtPayment (its constructor is internal).
        for (int i = 0; i < 12; i++)
        {
            var debt = Debt.Create(
                $"Tarjeta{i:D2}", Money.Create(1_000m, Currency.DOP), Currency.DOP, 1, Money.Create(1_000m, Currency.DOP),
                nameof(DebtFrequency.Monthly), _today.AddDays(i + 1));
            sut.DebtRepo.Seed(debt);
            sut.DebtPaymentRepo.SeedRange(new DebtScheduleGenerator().Generate(debt));
        }

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.NotNull(snapshot.UpcomingPayments);
        Assert.Equal(10, snapshot.UpcomingPayments!.Count);
    }

    // ------------------------------------------------------------------
    // Per-section degradation: a throwing dependency leaves only that
    // section null, the rest of the snapshot stays intact.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_MonthSummaryThrows_LeavesOnlySummarySectionsNull()
    {
        var throwingExpenseRepo = new FakeExpenseRepository();
        throwingExpenseRepo.SetThrowOnGetPeriodTotals(new InvalidOperationException("boom"));
        var converter = new FakeCurrencyConverter();
        var monthSummary = new GetMonthSummaryQuery(throwingExpenseRepo, new FakeIncomeRepository(), converter);
        Sut sut = CreateSut(converter: converter, monthSummaryOverride: monthSummary);

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.Null(snapshot.CurrentMonth);
        Assert.Null(snapshot.PriorMonth);
        Assert.NotNull(snapshot.CurrentCategories);
        Assert.NotNull(snapshot.PriorCategories);
        Assert.NotNull(snapshot.Budgets);
        Assert.NotNull(snapshot.Goals);
        Assert.NotNull(snapshot.Debts);
        Assert.NotNull(snapshot.UpcomingPayments);
        Assert.True(sut.Logger.HasWarning);
    }

    [Fact]
    public async Task ExecuteAsync_CategoryBreakdownThrows_LeavesOnlyCategorySectionsNull()
    {
        var throwingExpenseRepo = new FakeExpenseRepository();
        throwingExpenseRepo.SetThrowOnGetCategoryTotals(new InvalidOperationException("boom"));
        var converter = new FakeCurrencyConverter();
        var categoryBreakdown = new GetCategoryBreakdownQuery(throwingExpenseRepo, converter);
        Sut sut = CreateSut(converter: converter, categoryBreakdownOverride: categoryBreakdown);

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.Null(snapshot.CurrentCategories);
        Assert.Null(snapshot.PriorCategories);
        Assert.NotNull(snapshot.CurrentMonth);
        Assert.NotNull(snapshot.Budgets);
        Assert.NotNull(snapshot.Goals);
        Assert.NotNull(snapshot.Debts);
        Assert.NotNull(snapshot.UpcomingPayments);
        Assert.True(sut.Logger.HasWarning);
    }

    [Fact]
    public async Task ExecuteAsync_BudgetVsActualThrows_LeavesOnlyBudgetsSectionNull()
    {
        var throwingBudgetRepo = new FakeBudgetRepository();
        throwingBudgetRepo.SetThrowOnGetByMonth(new InvalidOperationException("boom"));
        var converter = new FakeCurrencyConverter();
        var budgetVsActual = new GetBudgetVsActualQuery(
            throwingBudgetRepo, new FakeExpenseRepository(), new FakeCategoryRepository(), converter);
        Sut sut = CreateSut(converter: converter, budgetVsActualOverride: budgetVsActual);

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.Null(snapshot.Budgets);
        Assert.NotNull(snapshot.CurrentMonth);
        Assert.NotNull(snapshot.CurrentCategories);
        Assert.NotNull(snapshot.Goals);
        Assert.NotNull(snapshot.Debts);
        Assert.NotNull(snapshot.UpcomingPayments);
        Assert.True(sut.Logger.HasWarning);
    }

    [Fact]
    public async Task ExecuteAsync_GoalsThrows_LeavesOnlyGoalsSectionNull()
    {
        var throwingGoalRepo = new FakeGoalRepository();
        throwingGoalRepo.SetThrowOnGetAll(new InvalidOperationException("boom"));
        var clock = new FakeClock { Today = _today };
        var goals = new GetGoalsQuery(throwingGoalRepo, new FakeGoalContributionRepository(), clock);
        Sut sut = CreateSut(clock: clock, goalsOverride: goals);

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.Null(snapshot.Goals);
        Assert.NotNull(snapshot.CurrentMonth);
        Assert.NotNull(snapshot.CurrentCategories);
        Assert.NotNull(snapshot.Budgets);
        Assert.NotNull(snapshot.Debts);
        Assert.NotNull(snapshot.UpcomingPayments);
        Assert.True(sut.Logger.HasWarning);
    }

    [Fact]
    public async Task ExecuteAsync_DebtsThrows_LeavesOnlyDebtsSectionNull()
    {
        var throwingDebtRepo = new FakeDebtRepository();
        throwingDebtRepo.SetThrowOnGetAll(new InvalidOperationException("boom"));
        var debts = new GetDebtsQuery(throwingDebtRepo);
        Sut sut = CreateSut(debtsOverride: debts);

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.Null(snapshot.Debts);
        Assert.NotNull(snapshot.CurrentMonth);
        Assert.NotNull(snapshot.CurrentCategories);
        Assert.NotNull(snapshot.Budgets);
        Assert.NotNull(snapshot.Goals);
        Assert.NotNull(snapshot.UpcomingPayments);
        Assert.True(sut.Logger.HasWarning);
    }

    [Fact]
    public async Task ExecuteAsync_UpcomingPaymentsThrows_LeavesOnlyPaymentsSectionNull()
    {
        var throwingRuleRepo = new FakeRecurringRuleRepository();
        throwingRuleRepo.SetThrowOnGetAll(new InvalidOperationException("boom"));
        var clock = new FakeClock { Today = _today };
        var upcomingPayments = new GetUpcomingPaymentsQuery(
            new FakeDebtPaymentRepository(), new FakeDebtRepository(), throwingRuleRepo, clock);
        Sut sut = CreateSut(clock: clock, upcomingPaymentsOverride: upcomingPayments);

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.Null(snapshot.UpcomingPayments);
        Assert.NotNull(snapshot.CurrentMonth);
        Assert.NotNull(snapshot.CurrentCategories);
        Assert.NotNull(snapshot.Budgets);
        Assert.NotNull(snapshot.Goals);
        Assert.NotNull(snapshot.Debts);
        Assert.True(sut.Logger.HasWarning);
    }

    // ------------------------------------------------------------------
    // Caller cancellation must rethrow unchanged, never degrade to null
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_CallerCancelled_RethrowsOperationCanceledExceptionUnchanged()
    {
        var throwingExpenseRepo = new FakeExpenseRepository();
        throwingExpenseRepo.SetThrowOnGetPeriodTotals(new OperationCanceledException());
        var converter = new FakeCurrencyConverter();
        var monthSummary = new GetMonthSummaryQuery(throwingExpenseRepo, new FakeIncomeRepository(), converter);
        Sut sut = CreateSut(converter: converter, monthSummaryOverride: monthSummary);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.Query.ExecuteAsync(cts.Token));
    }

    // ------------------------------------------------------------------
    // S-9: a missing exchange rate omits the figure and flags the snapshot
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_GoalInUsdWithNoRate_OmitsAmountAndFlagsHasMissingRatesWithoutPoisoningOtherSections()
    {
        var converter = new FakeCurrencyConverter((money, _) => money.Currency == Currency.USD
            ? Result.Failure("ExchangeRateUnavailable", "No rate for USD.")
            : Result.Success(Money.Create(money.Amount, Currency.DOP)));
        Sut sut = CreateSut(converter: converter);

        var usdGoal = Goal.Create("Viaje", Money.Create(5_000m, Currency.USD), new DateOnly(2027, 6, 30));
        sut.GoalRepo.Seed(usdGoal);
        await SeedCategoryExpenseAsync(sut.ExpenseRepo, "Comida", 1_000m, _currentMonthAnchor);

        Result result = await sut.Query.ExecuteAsync();

        ChatContextSnapshot snapshot = ExecuteSuccessfully(result);
        Assert.True(snapshot.HasMissingRates);
        ChatGoalLine goalLine = Assert.Single(snapshot.Goals!);
        Assert.Null(goalLine.Target.AmountDop);
        Assert.Equal(5_000m, goalLine.Target.OriginalAmount);
        Assert.Equal(Currency.USD, goalLine.Target.OriginalCurrency);

        // The unrelated DOP category section is untouched: no partial sum poisoned.
        Assert.NotNull(snapshot.CurrentCategories);
        Assert.Equal(1_000m, snapshot.CurrentCategories!.TotalDop);
    }

    // ------------------------------------------------------------------
    // S-10: no snapshot record exposes transaction/counterparty detail
    // ------------------------------------------------------------------

    [Fact]
    public void ChatContextSnapshotTypes_ExposeNoTransactionLevelOrCounterpartyDetail()
    {
        Type[] types =
        [
            typeof(ChatAmount),
            typeof(ChatMonthTotals),
            typeof(ChatCategoryLine),
            typeof(ChatCategoryList),
            typeof(ChatBudgetLine),
            typeof(ChatGoalLine),
            typeof(ChatDebtLine),
            typeof(ChatPaymentLine),
            typeof(ChatContextSnapshot),
        ];

        string[] forbiddenPrefixes = ["Note", "Tags", "PaymentMethod", "Counterparty"];

        foreach (Type type in types)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                bool forbidden = forbiddenPrefixes.Any(
                    prefix => property.Name.StartsWith(prefix, StringComparison.Ordinal));
                Assert.False(forbidden, $"{type.Name}.{property.Name} exposes transaction-level or counterparty detail.");
            }
        }
    }
}
