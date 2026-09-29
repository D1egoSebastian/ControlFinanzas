using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;
using ControlDeFinanzasIA.Application.UseCases.Exports.ExportExpenseListReport;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Exports;

/// <summary>
/// Tests for <see cref="ExportExpenseListReportUseCase"/> (F-018, S5): filter pass-through, paging
/// across all pages, DOP normalization and the buffered render-then-copy contract.
/// </summary>
public sealed class ExportExpenseListReportUseCaseTests
{
    private static readonly DateOnly _today = new(2026, 3, 20);
    private static readonly DateTime _created = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_450MatchingRows_AreReadAsThreePagesOf200And200And50()
    {
        using var f = new Fixture();
        Guid food = f.AddCategory("Food");
        Guid other = f.AddCategory("Other");
        for (int i = 0; i < 450; i++)
        {
            f.AddExpense(food, new DateOnly(2026, 3, 1 + (i % 28)), 1m, description: $"e{i}");
        }

        f.AddExpense(other, new DateOnly(2026, 3, 5), 9m, description: "other-category");
        f.AddExpense(food, new DateOnly(2025, 12, 5), 9m, description: "out-of-range");

        Result result = await f.RunAsync(new ExpenseFilter
        {
            CategoryId = food,
            DateFrom = new DateOnly(2026, 3, 1),
            DateTo = new DateOnly(2026, 3, 31),
        });

        Assert.True(result.IsSuccess);
        ExpenseListReport report = f.Writer.CapturedExpenseList!;
        Assert.Equal(450, report.Lines.Count);
        Assert.DoesNotContain(report.Lines, l => l.Description is "other-category" or "out-of-range");
        Assert.Equal(new[] { 1, 2, 3 }, f.Expenses.RequestedPages.ToArray());
        Assert.Equal(450m, report.TotalDop);
        Assert.Equal(450, ((ExportReportResult)result.Value!).RowCount);
    }

    [Fact]
    public async Task ExecuteAsync_IgnoresIncomingPage_AndOverwritesPageSizeTo200()
    {
        using var f = new Fixture();
        Guid food = f.AddCategory("Food");
        f.AddExpense(food, new DateOnly(2026, 3, 5), 5m);

        await f.RunAsync(new ExpenseFilter { Page = 7, PageSize = 20, CategoryId = food });

        Assert.Equal(new[] { 1 }, f.Expenses.RequestedPages.ToArray());
        Assert.Equal(200, f.Expenses.LastExpenseFilter!.PageSize);
        Assert.Equal(food, f.Expenses.LastExpenseFilter!.CategoryId);
    }

    [Fact]
    public async Task ExecuteAsync_PassesTagFilterThrough()
    {
        using var f = new Fixture();
        var tag = Guid.NewGuid();

        await f.RunAsync(new ExpenseFilter { TagIds = new[] { tag } });

        Assert.Equal(new[] { tag }, f.Expenses.LastExpenseFilter!.TagIds);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroMatches_ProducesValidEmptyReport()
    {
        using var f = new Fixture();

        Result result = await f.RunAsync(new ExpenseFilter());

        Assert.True(result.IsSuccess);
        ExpenseListReport report = f.Writer.CapturedExpenseList!;
        Assert.Empty(report.Lines);
        Assert.Equal(0m, report.TotalDop);
        Assert.False(report.HasMissingRates);
        Assert.Equal(0, ((ExportReportResult)result.Value!).RowCount);
        Assert.Equal(1, f.Target.OpenCount);
    }

    [Fact]
    public async Task ExecuteAsync_DateFromAfterDateTo_InvalidFilterAndNoRepositoryCall()
    {
        using var f = new Fixture();

        Result result = await f.RunAsync(new ExpenseFilter
        {
            DateFrom = new DateOnly(2026, 3, 10),
            DateTo = new DateOnly(2026, 3, 1),
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("InvalidFilter", result.Error!.Code);
        Assert.Equal(0, f.Expenses.GetByFilterCallCount);
        Assert.Equal(0, f.Target.OpenCount);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyGuidInTagIds_InvalidFilterAndNoRepositoryCall()
    {
        using var f = new Fixture();

        Result result = await f.RunAsync(new ExpenseFilter { TagIds = new[] { Guid.Empty } });

        Assert.Equal("InvalidFilter", result.Error!.Code);
        Assert.Equal(0, f.Expenses.GetByFilterCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_FilterSummary_CarriesRangeCategoryNameAndTagCount()
    {
        using var f = new Fixture();
        Guid food = f.AddCategory("Food");

        await f.RunAsync(new ExpenseFilter
        {
            CategoryId = food,
            DateFrom = new DateOnly(2026, 3, 1),
            DateTo = new DateOnly(2026, 3, 31),
            TagIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
        });

        ReportFilterSummary summary = f.Writer.CapturedExpenseList!.Filter;
        Assert.Equal(new DateOnly(2026, 3, 1), summary.DateFrom);
        Assert.Equal(new DateOnly(2026, 3, 31), summary.DateTo);
        Assert.Equal("Food", summary.CategoryName);
        Assert.Equal(2, summary.TagCount);
    }

    [Fact]
    public async Task ExecuteAsync_NoFilter_SummaryHasNoCategoryNoDatesNoTags()
    {
        using var f = new Fixture();

        await f.RunAsync(new ExpenseFilter());

        ReportFilterSummary summary = f.Writer.CapturedExpenseList!.Filter;
        Assert.Null(summary.DateFrom);
        Assert.Null(summary.DateTo);
        Assert.Null(summary.CategoryName);
        Assert.Equal(0, summary.TagCount);
    }

    [Fact]
    public async Task ExecuteAsync_LineCategoryNameComesFromSameDictionary()
    {
        using var f = new Fixture();
        Guid food = f.AddCategory("Food");
        f.AddExpense(food, new DateOnly(2026, 3, 5), 5m, description: "lunch");

        await f.RunAsync(new ExpenseFilter { CategoryId = food });

        ReportTransactionLine line = Assert.Single(f.Writer.CapturedExpenseList!.Lines);
        Assert.Equal("Food", line.CategoryName);
        Assert.Equal(ReportTransactionKind.Expense, line.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_MissingRate_LeavesDopEmptyAndFlagsAndTotalsOnlyConverted()
    {
        using var f = new Fixture(Fixture.NoRate);
        Guid food = f.AddCategory("Food");
        f.AddExpense(food, new DateOnly(2026, 3, 5), 30m, Currency.USD);
        f.AddExpense(food, new DateOnly(2026, 3, 6), 50m);

        Result result = await f.RunAsync(new ExpenseFilter());

        Assert.True(result.IsSuccess);
        ExpenseListReport report = f.Writer.CapturedExpenseList!;
        Assert.Null(report.Lines.Single(l => l.Currency == Currency.USD).AmountDop);
        Assert.Equal(30m, report.Lines.Single(l => l.Currency == Currency.USD).Amount);
        Assert.True(report.HasMissingRates);
        Assert.Equal(50m, report.TotalDop);
        Assert.True(((ExportReportResult)result.Value!).HasMissingRates);
    }

    [Fact]
    public async Task ExecuteAsync_UsdAtRate60_ConvertsToDop()
    {
        using var f = new Fixture(Fixture.Rate60);
        Guid food = f.AddCategory("Food");
        f.AddExpense(food, new DateOnly(2026, 3, 5), 100m, Currency.USD);

        await f.RunAsync(new ExpenseFilter());

        ExpenseListReport report = f.Writer.CapturedExpenseList!;
        Assert.Equal(6000m, Assert.Single(report.Lines).AmountDop);
        Assert.Equal(6000m, report.TotalDop);
        Assert.False(report.HasMissingRates);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedOnIsClockToday_AndLabelsPassedByReference()
    {
        using var f = new Fixture();

        await f.RunAsync(new ExpenseFilter());

        Assert.Equal(_today, f.Writer.CapturedExpenseList!.GeneratedOn);
        Assert.Same(f.Labels, f.Writer.CapturedLabels);
    }

    [Fact]
    public async Task ExecuteAsync_Success_CopiesRenderedBytesToTarget()
    {
        using var f = new Fixture();
        f.Writer.Payload = new byte[] { 9, 8, 7 };

        Result result = await f.RunAsync(new ExpenseFilter());

        Assert.True(result.IsSuccess);
        Assert.Equal(new byte[] { 9, 8, 7 }, f.Target.WrittenBytes);
        Assert.Equal("recorded.bin", ((ExportReportResult)result.Value!).TargetName);
    }

    [Fact]
    public async Task ExecuteAsync_WriterReturnsFailure_ReportRenderFailedAndTargetNeverOpened()
    {
        using var f = new Fixture();
        f.Writer.FailWith = new Error("Boom", "render broke");

        Result result = await f.RunAsync(new ExpenseFilter());

        Assert.Equal("ReportRenderFailed", result.Error!.Code);
        Assert.Equal(0, f.Target.OpenCount);
    }

    [Fact]
    public async Task ExecuteAsync_WriterThrows_ReportRenderFailedAndTargetNeverOpened()
    {
        using var f = new Fixture();
        f.Writer.ThrowWith = new InvalidOperationException("lib crash");

        Result result = await f.RunAsync(new ExpenseFilter());

        Assert.Equal("ReportRenderFailed", result.Error!.Code);
        Assert.Equal(0, f.Target.OpenCount);
    }

    [Fact]
    public async Task ExecuteAsync_IOExceptionOnOpen_ReportSaveFailed()
    {
        using var f = new Fixture(target: SaveTargets.ThrowingOnOpen(new IOException("locked")));

        Result result = await f.RunAsync(new ExpenseFilter());

        Assert.Equal("ReportSaveFailed", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_NoWriterForFormat_ReportWriterUnavailableAndNoRepositoryCall()
    {
        using var f = new Fixture();

        Result result = await f.RunAsync(new ExpenseFilter(), ReportFormat.Xlsx);

        Assert.Equal("ReportWriterUnavailable", result.Error!.Code);
        Assert.Equal(0, f.Expenses.GetByFilterCallCount);
        Assert.Equal(0, f.Target.OpenCount);
    }

    [Fact]
    public async Task ExecuteAsync_RepositoryThrows_ReportDataFailed()
    {
        using var f = new Fixture();
        f.Expenses.SetThrowOnGetFilter(new InvalidOperationException("db down"));

        Result result = await f.RunAsync(new ExpenseFilter());

        Assert.Equal("ReportDataFailed", result.Error!.Code);
        Assert.Equal(0, f.Target.OpenCount);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_ThrowsInsteadOfFailure()
    {
        using var f = new Fixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        f.Writer.ThrowWith = new OperationCanceledException(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => f.RunAsync(new ExpenseFilter(), ct: cts.Token));
        Assert.Equal(0, f.Target.OpenCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingLongerTarget_IsTruncatedToNewContent()
    {
        using var f = new Fixture(target: SaveTargets.Recording(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }));
        f.Writer.Payload = new byte[] { 5, 6, 7 };

        await f.RunAsync(new ExpenseFilter());

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
            Target = target ?? SaveTargets.Recording();
        }

        public FakeExpenseRepository Expenses { get; } = new();

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

        public Task<Result> RunAsync(
            ExpenseFilter filter,
            ReportFormat format = ReportFormat.Pdf,
            CancellationToken ct = default)
        {
            var useCase = new ExportExpenseListReportUseCase(
                Expenses,
                _categories,
                _converter,
                new IReportWriter[] { Writer },
                new FakeClock { Today = _today },
                NullLogger<ExportExpenseListReportUseCase>.Instance);

            return useCase.ExecuteAsync(
                new ExportExpenseListReportCommand(filter, format, Labels),
                Target.Target,
                ct);
        }

        public void Dispose() => Target.Dispose();
    }
}
