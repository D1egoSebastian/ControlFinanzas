using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Exports.ExportMonthSummaryReport;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Exports;

/// <summary>
/// Tests for <see cref="ExportMonthSummaryReportUseCase"/> (F-018, S1b): DTO construction from
/// the existing queries, DOP normalization, paging, and the buffered render-then-copy contract.
/// </summary>
public sealed class ExportMonthSummaryReportUseCaseTests
{
    private static readonly DateOnly _anchor = new(2026, 3, 1);
    private static readonly DateOnly _today = new(2026, 3, 20);
    private static readonly DateTime _created = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_SummaryMatchesGetMonthSummaryQuery()
    {
        using var f = new Fixture();
        Guid food = f.AddCategory("Food");
        f.AddExpense(food, new DateOnly(2026, 3, 5), 100m);
        f.AddExpense(food, new DateOnly(2026, 2, 5), 40m);
        f.AddIncome(null, new DateOnly(2026, 3, 6), 500m);
        f.AddIncome(null, new DateOnly(2026, 2, 6), 250m);

        Result result = await f.RunAsync();

        Assert.True(result.IsSuccess);
        MonthSummaryReport report = f.Writer.CapturedMonthSummary!;
        var expected = (MonthSummaryResult)(await f.SummaryQuery().ExecuteAsync(_anchor)).Value!;
        Assert.Equal(expected, report.Summary);
        Assert.Equal(100m, report.Summary.ExpensesDop);
        Assert.Equal(40m, report.Summary.PreviousExpensesDop);
        Assert.Equal(250m, report.Summary.PreviousIncomesDop);
        Assert.Equal(_anchor, report.MonthAnchor);
    }

    [Fact]
    public async Task ExecuteAsync_MoreThanFiveCategories_StayUnfoldedWithoutOtras()
    {
        using var f = new Fixture();
        for (int i = 1; i <= 7; i++)
        {
            Guid id = f.AddCategory($"Cat{i}");
            f.AddExpense(id, new DateOnly(2026, 3, 2), i * 10m);
        }

        await f.RunAsync();

        MonthSummaryReport report = f.Writer.CapturedMonthSummary!;
        Assert.Equal(7, report.Categories.Count);
        Assert.DoesNotContain(report.Categories, c => c.Label == "Otras");
        Assert.Equal(report.CategoriesTotalDop, report.Categories.Sum(c => c.AmountDop));
        Assert.Equal(280m, report.CategoriesTotalDop);
    }

    [Fact]
    public async Task ExecuteAsync_UsdLineAtRate60_ConvertsToDop_DopLineEqualsAmount()
    {
        using var f = new Fixture(Fixture.Rate60);
        Guid food = f.AddCategory("Food");
        f.AddExpense(food, new DateOnly(2026, 3, 5), 100m, Currency.USD);
        f.AddExpense(food, new DateOnly(2026, 3, 6), 75.25m);

        await f.RunAsync();

        IReadOnlyList<ReportTransactionLine> lines = f.Writer.CapturedMonthSummary!.Transactions;
        ReportTransactionLine usd = lines.Single(l => l.Currency == Currency.USD);
        ReportTransactionLine dop = lines.Single(l => l.Currency == Currency.DOP);
        Assert.Equal(6000.00m, usd.AmountDop);
        Assert.Equal(100m, usd.Amount);
        Assert.Equal(75.25m, dop.AmountDop);
        Assert.False(f.Writer.CapturedMonthSummary!.HasMissingRates);
    }

    [Fact]
    public async Task ExecuteAsync_LineWithoutRate_KeepsAmountAndFlagsMissingRates()
    {
        using var f = new Fixture(Fixture.NoRate);
        Guid food = f.AddCategory("Food");
        f.AddExpense(food, new DateOnly(2026, 3, 5), 30m, Currency.USD);
        f.AddExpense(food, new DateOnly(2026, 3, 6), 50m);

        Result result = await f.RunAsync();

        Assert.True(result.IsSuccess);
        MonthSummaryReport report = f.Writer.CapturedMonthSummary!;
        ReportTransactionLine missing = report.Transactions.Single(l => l.Currency == Currency.USD);
        Assert.Equal(30m, missing.Amount);
        Assert.Null(missing.AmountDop);
        Assert.True(report.HasMissingRates);
        Assert.Equal(50m, report.Summary.ExpensesDop);
        Assert.Equal(50m, report.CategoriesTotalDop);
        Assert.True(((ExportReportResult)result.Value!).HasMissingRates);
    }

    [Fact]
    public async Task ExecuteAsync_IncludesExpensesAndIncomes_OrderedByDateThenExpenseFirst()
    {
        using var f = new Fixture();
        Guid food = f.AddCategory("Food");
        f.AddIncome(null, new DateOnly(2026, 3, 10), 900m, description: "salary");
        f.AddExpense(food, new DateOnly(2026, 3, 10), 10m, description: "lunch");
        f.AddExpense(food, new DateOnly(2026, 3, 2), 5m, description: "coffee");
        f.AddIncome(null, new DateOnly(2026, 3, 1), 20m, description: "gift");

        await f.RunAsync();

        IReadOnlyList<ReportTransactionLine> lines = f.Writer.CapturedMonthSummary!.Transactions;
        Assert.Equal(
            new[] { "gift", "coffee", "lunch", "salary" },
            lines.Select(l => l.Description).ToArray());
        Assert.Equal(ReportTransactionKind.Expense, lines[2].Kind);
        Assert.Equal(ReportTransactionKind.Income, lines[3].Kind);
        Assert.Equal(string.Empty, lines[0].CategoryName);
        Assert.Equal("Food", lines[1].CategoryName);
    }

    [Fact]
    public async Task ExecuteAsync_IndeterminateBudgetLines_PassThroughWithNullStatus()
    {
        using var f = new Fixture(Fixture.NoRate);
        Guid food = f.AddCategory("Food");
        f.Budgets.Seed(Budget.Create(2026, 3, food, Money.Create(100m, Currency.USD)));

        await f.RunAsync();

        BudgetVsActualResult budgets = f.Writer.CapturedMonthSummary!.Budgets;
        BudgetVsActualLine line = Assert.Single(budgets.Lines);
        Assert.Null(line.Status);
        Assert.True(line.IsIndeterminate);
        Assert.True(f.Writer.CapturedMonthSummary!.HasMissingRates);
    }

    [Fact]
    public async Task ExecuteAsync_450RowsAcrossThreePages_AreAllIncluded()
    {
        using var f = new Fixture();
        Guid food = f.AddCategory("Food");
        for (int i = 0; i < 450; i++)
        {
            f.AddExpense(food, new DateOnly(2026, 3, 1 + (i % 28)), 1m, description: $"e{i}");
        }

        await f.RunAsync();

        Assert.Equal(450, f.Writer.CapturedMonthSummary!.Transactions.Count);
        Assert.Equal(new[] { 1, 2, 3 }, f.Expenses.RequestedPages.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_PassesLabelsToWriterByReference()
    {
        using var f = new Fixture();

        await f.RunAsync();

        Assert.Same(f.Labels, f.Writer.CapturedLabels);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyMonth_ProducesValidEmptyReport()
    {
        using var f = new Fixture();

        Result result = await f.RunAsync();

        Assert.True(result.IsSuccess);
        MonthSummaryReport report = f.Writer.CapturedMonthSummary!;
        Assert.Equal(0m, report.Summary.ExpensesDop);
        Assert.Equal(0m, report.Summary.IncomesDop);
        Assert.Empty(report.Categories);
        Assert.Empty(report.Budgets.Lines);
        Assert.Empty(report.Transactions);
        Assert.False(report.HasMissingRates);
        var payload = (ExportReportResult)result.Value!;
        Assert.Equal(0, payload.RowCount);
        Assert.Equal(ReportFormat.Pdf, payload.Format);
        Assert.Equal("recorded.bin", payload.TargetName);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedOnIsClockToday()
    {
        using var f = new Fixture();

        await f.RunAsync();

        Assert.Equal(_today, f.Writer.CapturedMonthSummary!.GeneratedOn);
    }

    [Fact]
    public async Task ExecuteAsync_Success_CopiesRenderedBytesToTarget()
    {
        using var f = new Fixture();
        f.Writer.Payload = new byte[] { 9, 8, 7, 6 };

        await f.RunAsync();

        Assert.Equal(1, f.Target.OpenCount);
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, f.Target.WrittenBytes);
    }

    [Fact]
    public async Task ExecuteAsync_WriterReturnsFailure_ReportRenderFailedAndTargetNeverOpened()
    {
        using var f = new Fixture();
        f.Writer.FailWith = new Error("Boom", "render broke");

        Result result = await f.RunAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("ReportRenderFailed", result.Error!.Code);
        Assert.Equal(0, f.Target.OpenCount);
        Assert.Empty(f.Target.WrittenBytes);
    }

    [Fact]
    public async Task ExecuteAsync_WriterThrows_ReportRenderFailedAndTargetNeverOpened()
    {
        using var f = new Fixture();
        f.Writer.ThrowWith = new InvalidOperationException("lib crash");

        Result result = await f.RunAsync();

        Assert.Equal("ReportRenderFailed", result.Error!.Code);
        Assert.Equal(0, f.Target.OpenCount);
        Assert.Empty(f.Target.WrittenBytes);
    }

    [Fact]
    public async Task ExecuteAsync_IOExceptionOnOpen_ReportSaveFailed()
    {
        using var f = new Fixture(target: SaveTargets.ThrowingOnOpen(new IOException("locked")));

        Result result = await f.RunAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("ReportSaveFailed", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_UnauthorizedOnOpen_ReportSaveFailed()
    {
        using var f = new Fixture(target: SaveTargets.ThrowingOnOpen(new UnauthorizedAccessException()));

        Result result = await f.RunAsync();

        Assert.Equal("ReportSaveFailed", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_NoWriterForFormat_ReportWriterUnavailableAndNoRepositoryCall()
    {
        using var f = new Fixture();

        Result result = await f.RunAsync(format: ReportFormat.Xlsx);

        Assert.False(result.IsSuccess);
        Assert.Equal("ReportWriterUnavailable", result.Error!.Code);
        Assert.Equal(0, f.Expenses.GetByFilterCallCount);
        Assert.Equal(0, f.Incomes.GetByFilterCallCount);
        Assert.Equal(0, f.Expenses.GetCategoryTotalsCallCount);
        Assert.Equal(0, f.Target.OpenCount);
    }

    [Fact]
    public async Task ExecuteAsync_RepositoryThrows_ReturnsFailureInsteadOfThrowing()
    {
        using var f = new Fixture();
        f.Expenses.SetThrowOnGetFilter(new InvalidOperationException("db down"));

        Result result = await f.RunAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("ReportDataFailed", result.Error!.Code);
        Assert.Equal(0, f.Target.OpenCount);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_ThrowsOperationCanceledInsteadOfFailure()
    {
        using var f = new Fixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        f.Writer.ThrowWith = new OperationCanceledException(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.RunAsync(ct: cts.Token));
        Assert.Equal(0, f.Target.OpenCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingLongerTarget_IsTruncatedToNewContent()
    {
        using var f = new Fixture(target: SaveTargets.Recording(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }));
        f.Writer.Payload = new byte[] { 5, 6, 7 };

        await f.RunAsync();

        Assert.Equal(new byte[] { 5, 6, 7 }, f.Target.WrittenBytes);
    }

    private sealed class Fixture : IDisposable
    {
        public static readonly Func<Money, Currency, Result> Rate60 =
            (m, t) => Result.Success(Money.Create(m.Amount * 60m, t));

        public static readonly Func<Money, Currency, Result> NoRate =
            (_, _) => Result.Failure("NoRate", "No rate available.");

        private readonly FakeCurrencyConverter _converter;
        private readonly FakeCategoryRepository _categories = new();
        private readonly List<Category> _categoryList = new();

        public Fixture(
            Func<Money, Currency, Result>? onConvert = null,
            SaveTargets.RecordingSaveTarget? target = null)
        {
            _converter = new FakeCurrencyConverter(onConvert);
            Expenses.PageAddedItems = true;
            Incomes.PageAddedItems = true;
            Target = target ?? SaveTargets.Recording();
        }

        public FakeExpenseRepository Expenses { get; } = new();

        public FakeIncomeRepository Incomes { get; } = new();

        public FakeBudgetRepository Budgets { get; } = new();

        public FakeReportWriter Writer { get; } = new(ReportFormat.Pdf);

        public SaveTargets.RecordingSaveTarget Target { get; }

        public ReportLabels Labels { get; } = new()
        {
            Culture = CultureInfo.InvariantCulture,
            ReportMonthSummaryTitle = "Month",
            ReportExpenseListTitle = "List",
            ReportSummarySection = "Summary",
            ReportExpensesLabel = "Expenses",
            ReportIncomesLabel = "Incomes",
            ReportNetBalanceLabel = "Net",
            ReportDeltaLabel = "Delta",
            ReportCategoriesSection = "Categories",
            ReportBudgetsSection = "Budgets",
            ReportCategoryColumn = "Category",
            ReportAmountDopColumn = "DOP",
            ReportShareColumn = "Share",
            ReportBudgetColumn = "Budget",
            ReportActualColumn = "Actual",
            ReportPercentColumn = "%",
            ReportStatusColumn = "Status",
            ReportDateColumn = "Date",
            ReportDescriptionColumn = "Description",
            ReportAmountColumn = "Amount",
            ReportCurrencyColumn = "Currency",
            ReportTotalLabel = "Total",
            ReportStatusOk = "Ok",
            ReportStatusNear = "Near",
            ReportStatusExceeded = "Exceeded",
            ReportStatusIndeterminate = "n/a",
            ReportNoData = "No data",
            ReportFiltersLabel = "Filters",
            ReportFilterAll = "All",
            ReportFilterPeriodFormat = "{0}-{1}",
            ReportFilterCategoryLabel = "Category",
            ReportFilterTagsFormat = "{0} tags",
            ReportCountLabel = "Count",
            ReportGeneratedOnFormat = "Generated {0}",
            ReportMissingRatesNote = "Missing rates",
            ReportPageLabel = "Page",
        };

        public Guid AddCategory(string name)
        {
            var category = new Category { Id = Guid.NewGuid(), Name = name, CreatedAt = _created, UpdatedAt = _created };
            _categoryList.Add(category);
            _categories.SetCategories(_categoryList);
            Expenses.CategoryNames[category.Id] = name;
            return category.Id;
        }

        public void AddExpense(
            Guid categoryId,
            DateOnly date,
            decimal amount,
            Currency currency = Currency.DOP,
            string description = "expense")
            => Expenses
                .AddAsync(Expense.Create(categoryId, date, Money.Create(amount, currency), description, null, _created))
                .GetAwaiter().GetResult();

        public void AddIncome(
            Guid? categoryId,
            DateOnly date,
            decimal amount,
            Currency currency = Currency.DOP,
            string description = "income")
            => Incomes
                .AddAsync(Income.Create(categoryId, date, Money.Create(amount, currency), description, null, null, _created))
                .GetAwaiter().GetResult();

        public GetMonthSummaryQuery SummaryQuery() => new(Expenses, Incomes, _converter);

        public Task<Result> RunAsync(ReportFormat format = ReportFormat.Pdf, CancellationToken ct = default)
        {
            var useCase = new ExportMonthSummaryReportUseCase(
                SummaryQuery(),
                new GetCategoryBreakdownQuery(Expenses, _converter),
                new GetBudgetVsActualQuery(Budgets, Expenses, _categories, _converter),
                Expenses,
                Incomes,
                _categories,
                _converter,
                new IReportWriter[] { Writer },
                new FakeClock { Today = _today },
                NullLogger<ExportMonthSummaryReportUseCase>.Instance);

            return useCase.ExecuteAsync(
                new ExportMonthSummaryReportCommand(_anchor, format, Labels),
                Target.Target,
                ct);
        }

        public void Dispose() => Target.Dispose();
    }
}
