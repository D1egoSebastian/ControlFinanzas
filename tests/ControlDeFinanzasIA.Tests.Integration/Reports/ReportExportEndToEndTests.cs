using ClosedXML.Excel;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Exports.ExportExpenseListReport;
using ControlDeFinanzasIA.Application.UseCases.Exports.ExportMonthSummaryReport;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Infrastructure.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlDeFinanzasIA.Tests.Integration.Reports;

/// <summary>
/// End-to-end export tests (F-018, verification warning W1): the REAL month-summary and
/// expense-list use cases run with the REAL writers resolved from <c>AddReportExporters()</c>
/// over real repositories on a temporary SQLite file, writing to a temp file through a
/// <see cref="SaveTarget"/>. The real user data directory is never touched.
/// </summary>
public sealed class ReportExportEndToEndTests : IDisposable
{
    private const decimal _noRateUsdAmount = 777.77m;

    private static readonly DateTime _createdAt = new(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly _anchor = new(2026, 9, 15);

    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"report-e2e-{Guid.NewGuid():N}");

    public ReportExportEndToEndTests()
    {
        Directory.CreateDirectory(_workDir);
    }

    public static TheoryData<ReportFormat, string> Matrix => new()
    {
        { ReportFormat.Pdf, "es-DO" },
        { ReportFormat.Pdf, "en" },
        { ReportFormat.Xlsx, "es-DO" },
        { ReportFormat.Xlsx, "en" },
    };

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_workDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task MonthSummary_RealUseCaseAndWriter_ProducesAValidFile(ReportFormat format, string culture)
    {
        await using ServiceProvider provider = BuildProvider();
        await SeedAsync(provider);
        string path = Path.Combine(_workDir, "month." + Extension(format));
        ReportLabels labels = ReportTestData.Labels(culture, culture + " ");

        Result result;
        using (IServiceScope scope = provider.CreateScope())
        {
            ExportMonthSummaryReportUseCase useCase = scope.ServiceProvider.GetRequiredService<ExportMonthSummaryReportUseCase>();
            using SaveTarget target = FileTarget(path);
            result = await useCase.ExecuteAsync(new ExportMonthSummaryReportCommand(_anchor, format, labels), target);
        }

        Assert.True(result.IsSuccess, result.Error?.Message);
        if (format == ReportFormat.Pdf)
        {
            AssertPdf(path);
            return;
        }

        using var wb = new XLWorkbook(path);
        Assert.Equal(new[] { "Summary", "Data" }, wb.Worksheets.Select(w => w.Name).ToArray());
        IXLWorksheet data = wb.Worksheet("Data");

        // Header + 3 expenses + 2 incomes.
        Assert.Equal(6, data.LastRowUsed()!.RowNumber());
        AssertNoRateLineIsBlank(data);
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task ExpenseList_RealUseCaseAndWriter_ProducesAValidFile(ReportFormat format, string culture)
    {
        await using ServiceProvider provider = BuildProvider();
        await SeedAsync(provider);
        string path = Path.Combine(_workDir, "list." + Extension(format));
        ReportLabels labels = ReportTestData.Labels(culture, culture + " ");

        Result result;
        using (IServiceScope scope = provider.CreateScope())
        {
            ExportExpenseListReportUseCase useCase = scope.ServiceProvider.GetRequiredService<ExportExpenseListReportUseCase>();
            using SaveTarget target = FileTarget(path);
            var filter = new ExpenseFilter { DateFrom = new DateOnly(2026, 9, 1), DateTo = new DateOnly(2026, 9, 30) };
            result = await useCase.ExecuteAsync(new ExportExpenseListReportCommand(filter, format, labels), target);
        }

        Assert.True(result.IsSuccess, result.Error?.Message);
        if (format == ReportFormat.Pdf)
        {
            AssertPdf(path);
            return;
        }

        using var wb = new XLWorkbook(path);
        Assert.Equal(new[] { "Summary", "Data" }, wb.Worksheets.Select(w => w.Name).ToArray());
        IXLWorksheet data = wb.Worksheet("Data");

        // Header + 3 expenses only: the two seeded incomes must not appear.
        Assert.Equal(4, data.LastRowUsed()!.RowNumber());
        Assert.DoesNotContain(
            data.RowsUsed().Skip(1).Select(r => r.Cell(3).GetString()),
            d => d.StartsWith("Ingreso", StringComparison.Ordinal));
        AssertNoRateLineIsBlank(data);
    }

    private static string Extension(ReportFormat format) => format == ReportFormat.Pdf ? "pdf" : "xlsx";

    private static SaveTarget FileTarget(string path) => new(
        Path.GetFileName(path),
        _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)));

    private static void AssertPdf(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        Assert.True(bytes.Length > 1024, $"PDF is too small: {bytes.Length} bytes.");
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    private static void AssertNoRateLineIsBlank(IXLWorksheet data)
    {
        IXLRow noRate = data.RowsUsed()
            .Skip(1)
            .Single(r => Math.Abs(r.Cell(5).GetDouble() - (double)_noRateUsdAmount) < 0.001);

        Assert.Equal("USD", noRate.Cell(6).GetString());
        Assert.True(noRate.Cell(7).IsEmpty(), "AmountDOP must be blank when there is no exchange rate.");

        IXLRow converted = data.RowsUsed()
            .Skip(1)
            .First(r => r.Cell(6).GetString() == "USD" && !r.Cell(7).IsEmpty());
        Assert.Equal(600d, converted.Cell(7).GetDouble(), 2);
    }

    private static async Task SeedAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;
        await sp.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        IReadOnlyList<Category> categories = await sp.GetRequiredService<ICategoryRepository>().GetAllAsync();
        Guid categoryId = categories[0].Id;
        IExpenseRepository expenses = sp.GetRequiredService<IExpenseRepository>();
        IIncomeRepository incomes = sp.GetRequiredService<IIncomeRepository>();

        Money[] expenseAmounts =
        [
            Money.Create(1000m, Currency.DOP),
            Money.Create(10m, Currency.USD),
            Money.Create(_noRateUsdAmount, Currency.USD),
        ];
        for (int i = 0; i < expenseAmounts.Length; i++)
        {
            Result added = await expenses.AddAsync(Expense.Create(
                categoryId, new DateOnly(2026, 9, 3 + i), expenseAmounts[i], $"Gasto {i}", null, _createdAt));
            Assert.True(added.IsSuccess, added.Error?.Message);
        }

        foreach ((Money amount, int day) in new[] { (Money.Create(5000m, Currency.DOP), 10), (Money.Create(50m, Currency.USD), 11) })
        {
            Result added = await incomes.AddAsync(Income.Create(
                categoryId, new DateOnly(2026, 9, day), amount, $"Ingreso {day}", null, null, _createdAt));
            Assert.True(added.IsSuccess, added.Error?.Message);
        }
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new StubClock());
        services.AddSingleton<ICurrencyConverter, FixedRateConverter>();
        services.AddPersistence(new StubPathResolver(Path.Combine(_workDir, "test.db")));
        services.AddReportExporters();
        services.AddTransient<GetMonthSummaryQuery>();
        services.AddTransient<GetCategoryBreakdownQuery>();
        services.AddTransient<GetBudgetVsActualQuery>();
        services.AddTransient<ExportMonthSummaryReportUseCase>();
        services.AddTransient<ExportExpenseListReportUseCase>();
        return services.BuildServiceProvider();
    }

    private sealed class StubClock : IClock
    {
        public DateTime UtcNow => _createdAt;

        public DateOnly Today => new(2026, 9, 29);
    }

    private sealed class StubPathResolver(string databasePath) : IPathResolver
    {
        public string DataDirectory => Path.GetDirectoryName(databasePath)!;

        public string DatabaseFilePath => databasePath;
    }

    /// <summary>DOP is identity, USD is 60 DOP, except the sentinel amount which has no rate.</summary>
    private sealed class FixedRateConverter : ICurrencyConverter
    {
        public Task<Result> ConvertAsync(Money source, Currency target, CancellationToken ct = default)
        {
            if (source.Currency == target)
            {
                return Task.FromResult(Result.Success(source));
            }

            if (source.Amount == _noRateUsdAmount)
            {
                return Task.FromResult(Result.Failure("RateUnavailable", "No exchange rate."));
            }

            return Task.FromResult(Result.Success(Money.Create(source.Amount * 60m, target)));
        }

        public Task<MoneyDisplay> DisplayAsync(Money source, CancellationToken ct = default) =>
            Task.FromResult(new MoneyDisplay(source, null, null));
    }
}
