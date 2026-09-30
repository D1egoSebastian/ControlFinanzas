using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Exports.ExportExpenseListReport;
using ControlDeFinanzasIA.Application.UseCases.Exports.ExportMonthSummaryReport;
using ControlDeFinanzasIA.Application.UseCases.Import;
using ControlDeFinanzasIA.Application.UseCases.Import.CommitImport;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.PreviewImport;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Import;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Infrastructure.Reports;
using ControlDeFinanzasIA.Tests.Integration.Backup;
using ControlDeFinanzasIA.Tests.Integration.Reports;
using ControlDeFinanzasIA.UI.Services;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>
/// The Phase 20 roadmap criterion, end to end (F-020, S7, R-17, R-21, R-22, S-19, S-23, S-26, C-18): a database A
/// is exported with the REAL Phase 18 use cases and the REAL <c>ClosedXmlReportWriter</c>, the file is imported
/// into a fresh migrated database B through the REAL preview/commit use cases, reader, lookup and writer, and B
/// must hold the same transactions. Every collaborator comes from a real <see cref="ServiceCollection"/>
/// (<c>AddPersistence</c> + <c>AddReportExporters</c> + <c>AddImport</c>); only the clock, the exchange-rate
/// converter, the file picker and the notification centre are fakes. Temp directories only: the real user data
/// directory is never touched.
/// </summary>
public sealed class ImportEndToEndTests : IDisposable
{
    private static readonly DateTime _seedInstant = new(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly _anchor = new(2026, 9, 15);

    private readonly List<Site> _sites = [];

    public void Dispose()
    {
        foreach (Site site in _sites)
        {
            site.Dispose();
        }

        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task ExpenseListExport_ImportedIntoFreshDatabase_IsFieldIdentical()
    {
        Site a = NewSite();
        await SeedSourceAsync(a);
        byte[] xlsx = await ExportExpenseListAsync(a);
        Site b = NewSite();

        ImportCommitResult committed = await b.ImportOkAsync(xlsx);

        var source = (await a.ReadRowsAsync()).Where(r => r.Type == "Expense").ToList();
        Assert.Equal(source.Count, committed.ExpensesInserted);
        Assert.Equal(0, committed.IncomesInserted);
        Assert.Equal(0, committed.Rejected);
        Assert.Equal(source, await b.ReadRowsAsync());

        // Field-level guarantees called out by the roadmap: 4-place decimals and both currencies survive untouched.
        Assert.Contains(source, r => r.Amount == 12.3456m && r.Currency == Currency.USD);
        Assert.Contains(source, r => r.Amount == 1234.5m && r.Currency == Currency.DOP);
    }

    [Fact]
    public async Task MonthSummaryExport_ImportedIntoFreshDatabase_KeepsExpensesAndIncomes()
    {
        Site a = NewSite();
        await SeedSourceAsync(a);
        byte[] xlsx = await ExportMonthSummaryAsync(a);
        Site b = NewSite();

        ImportCommitResult committed = await b.ImportOkAsync(xlsx);

        List<StoredRow> source = await a.ReadRowsAsync();
        Assert.Equal(source.Count(r => r.Type == "Expense"), committed.ExpensesInserted);
        Assert.Equal(source.Count(r => r.Type == "Income"), committed.IncomesInserted);
        Assert.Equal(source, await b.ReadRowsAsync());

        // An income exported with an empty category comes back with no category at all.
        Assert.Contains(source, r => r.Type == "Income" && r.Category is null);
        Assert.Contains(await b.ReadRowsAsync(), r => r.Type == "Income" && r.Category is null);
    }

    [Fact]
    public async Task MonthSummaryExport_BlankAmountDopColumn_IsIgnoredAndOriginalAmountKept()
    {
        Site a = NewSite();
        await SeedSourceAsync(a);
        byte[] xlsx = await ExportMonthSummaryAsync(a);
        Site b = NewSite();

        await b.ImportOkAsync(xlsx);

        // The USD sentinel has no exchange rate, so the exported AmountDOP cell is blank; the import must not care.
        StoredRow noRate = Assert.Single(await b.ReadRowsAsync(), r => r.Description == "Sin tasa");
        Assert.Equal(777.77m, noRate.Amount);
        Assert.Equal(Currency.USD, noRate.Currency);
    }

    [Fact]
    public async Task ExpenseWithoutCategory_FallsBackToOtrosWithAWarning_IncomeStaysNullWithoutOne()
    {
        // The exported "" category cannot be produced through the DB (expenses always have one), so the REAL
        // writer is fed the report lines directly; everything after the file is real.
        byte[] xlsx = await XlsxFixtures.MonthSummaryAsync();
        Site b = NewSite();

        ImportPreview preview = await b.PreviewAsync(xlsx);

        ImportRowResult expense = Assert.Single(preview.SampleRows, r => r.Description == "Cafe, con leche");
        Assert.Contains(ImportReason.CategoryFallback, expense.Reasons);
        Assert.Equal("Otros", expense.CategoryName);

        ImportRowResult income = Assert.Single(preview.SampleRows, r => r.Description == "Salario");
        Assert.Null(income.CategoryName);
        Assert.DoesNotContain(ImportReason.CategoryFallback, income.Reasons);
        Assert.DoesNotContain(ImportReason.CategoryDropped, income.Reasons);

        await b.ImportOkAsync(xlsx);
        List<StoredRow> stored = await b.ReadRowsAsync();
        Assert.Equal("Otros", Assert.Single(stored, r => r.Description == "Cafe, con leche").Category);
        Assert.Null(Assert.Single(stored, r => r.Description == "Salario").Category);
    }

    [Fact]
    public async Task ReimportingTheSameFile_InsertsZeroRows()
    {
        Site a = NewSite();
        await SeedSourceAsync(a);
        byte[] xlsx = await ExportMonthSummaryAsync(a);
        Site b = NewSite();
        ImportCommitResult first = await b.ImportOkAsync(xlsx);
        (long Expenses, long Incomes, long Categories) afterFirst = b.Counts();

        ImportCommitResult second = await b.ImportOkAsync(xlsx);

        Assert.Equal(0, second.ExpensesInserted);
        Assert.Equal(0, second.IncomesInserted);
        Assert.Equal(0, second.CategoriesCreated);
        Assert.Equal(first.ExpensesInserted + first.IncomesInserted, second.DuplicatesSkipped);
        Assert.Equal(afterFirst, b.Counts());
    }

    [Theory]
    [InlineData("expense-list")]
    [InlineData("month-summary")]
    public async Task ImportingAnExportBackIntoItsSourceDatabase_InsertsZeroRows(string reportType)
    {
        Site a = NewSite();
        await SeedSourceAsync(a);
        byte[] xlsx = reportType == "expense-list" ? await ExportExpenseListAsync(a) : await ExportMonthSummaryAsync(a);
        (long Expenses, long Incomes, long Categories) before = a.Counts();

        ImportCommitResult result = await a.ImportOkAsync(xlsx);

        Assert.Equal(0, result.ExpensesInserted);
        Assert.Equal(0, result.IncomesInserted);
        Assert.Equal(0, result.CategoriesCreated);
        Assert.True(result.DuplicatesSkipped > 0);
        Assert.Equal(before, a.Counts());
    }

    [Fact]
    public async Task GenericSpanishBankCsv_WithOptionsSet_ProducesTheExpectedRows()
    {
        byte[] csv = CsvTestSupport.Fixture("bank-es-semicolon-comma-1252.csv");
        Site b = NewSite();
        var options = new ImportOptions(null, null, DecimalStyle.Comma, ImportDateFormat.DayMonthYear);

        ImportPreview preview = await b.PreviewAsync(csv, options);
        Assert.True(preview.Source.EncodingFallback);
        Assert.Equal(';', preview.Source.Delimiter);
        Assert.Equal(2, preview.ToImportCount);
        Assert.Equal(["Alimentación"], preview.CategoriesToCreate);

        ImportCommitResult committed = await b.ImportOkAsync(csv, options);

        Assert.Equal(2, committed.ExpensesInserted);
        Assert.Equal(1, committed.CategoriesCreated);
        List<StoredRow> stored = await b.ReadRowsAsync();
        Assert.Equal(
            new StoredRow("Expense", new DateOnly(2026, 3, 1), 1234.56m, Currency.DOP, "Café", "Alimentación"),
            Assert.Single(stored, r => r.Description == "Café"));
        Assert.Equal(
            new StoredRow("Expense", new DateOnly(2026, 3, 2), 99.50m, Currency.DOP, "Niño", "Educación"),
            Assert.Single(stored, r => r.Description == "Niño"));
    }

    [Fact]
    public async Task GenericSpanishBankCsv_WithAutoDecimalAndAMismatchedDateFormat_RejectsEveryRowAsInvalidDate()
    {
        // There is no "no format" value: a text date always needs one of the three formats. The dash format does
        // not match the slashes of this file, which is exactly the "user did not tell us the format" situation.
        byte[] csv = CsvTestSupport.Fixture("bank-es-semicolon-comma-1252.csv");
        Site b = NewSite();
        var options = new ImportOptions(null, null, DecimalStyle.Auto, ImportDateFormat.DayMonthYearDash);
        Snapshot before = b.Snapshot();

        ImportPreview preview = await b.PreviewAsync(csv, options);

        Assert.Equal(2, preview.RejectedCount);
        Assert.Equal(0, preview.ToImportCount);
        Assert.All(preview.SampleRows, row =>
        {
            Assert.Equal(ImportRowStatus.Rejected, row.Status);
            Assert.Contains(ImportReason.InvalidDate, row.Reasons);
        });

        ImportCommitResult committed = await b.ImportOkAsync(csv, options);
        Assert.Equal(0, committed.ExpensesInserted);
        Assert.Equal(2, committed.Rejected);
        Assert.Equal(before.Tables, b.Snapshot().Tables);
    }

    [Fact]
    public async Task Import_LeavesEveryOtherTableByteIdentical()
    {
        Site a = NewSite();
        await SeedSourceAsync(a);
        byte[] xlsx = await ExportMonthSummaryAsync(a);
        Site b = NewSite();
        Snapshot before = b.Snapshot();

        await b.ImportOkAsync(xlsx);
        await b.ImportOkAsync(CsvTestSupport.Fixture("bank-es-semicolon-comma-1252.csv"), new ImportOptions(null, null, DecimalStyle.Comma, ImportDateFormat.DayMonthYear));

        Snapshot after = b.Snapshot();
        string[] touched = ["Expenses", "Incomes", "Categories"];
        Assert.Equal(before.Tables.Keys.OrderBy(k => k, StringComparer.Ordinal), after.Tables.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (string table in before.Tables.Keys.Where(k => !touched.Contains(k)))
        {
            Assert.Equal(before.Tables[table], after.Tables[table]);
        }

        Assert.NotEqual(before.Tables["Expenses"], after.Tables["Expenses"]);
    }

    [Fact]
    public async Task ImportViewModel_PickRealXlsx_PreviewsConfirmsAndFinishesDone()
    {
        Site a = NewSite();
        await SeedSourceAsync(a);
        byte[] xlsx = await ExportExpenseListAsync(a);
        Site b = NewSite();
        b.Picker.Result = new OpenTarget("expenses.xlsx", _ => Task.FromResult<Stream>(new MemoryStream(xlsx, writable: false)));
        ImportViewModel vm = b.Services.GetRequiredService<ImportViewModel>();
        vm.PreviewDebounce = TimeSpan.Zero;
        var completed = new List<ImportCompletedEventArgs>();
        vm.Completed += (_, e) => completed.Add(e);
        int closeRequests = 0;
        vm.CloseRequested += (_, _) => closeRequests++;

        await vm.ChooseFileCommand.ExecuteAsync(null);

        Assert.Equal(ImportPanelState.Previewed, vm.State);
        Assert.Equal("expenses.xlsx", vm.FileName);
        Assert.False(vm.IsError);
        Assert.NotNull(vm.Preview);
        int expected = (await a.ReadRowsAsync()).Count(r => r.Type == "Expense");
        Assert.Equal(expected, vm.Preview.ToImportCount);
        Assert.Equal(1, b.Picker.Calls);
        Assert.Equal(0, b.Counts().Expenses);

        await vm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal(ImportPanelState.Done, vm.State);
        ImportCompletedEventArgs args = Assert.Single(completed);
        Assert.Equal(expected, args.Result.ExpensesInserted);
        Assert.Equal(expected, args.Inserted);
        Assert.Equal(expected, b.Counts().Expenses);
        Assert.Equal(0, b.Counts().Incomes);
        Assert.Equal(0, closeRequests);

        await b.Center.RefreshObserved.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, b.Center.TryRefreshCalls);

        Assert.Equal((await a.ReadRowsAsync()).Where(r => r.Type == "Expense").ToList(), await b.ReadRowsAsync());
    }

    private Site NewSite()
    {
        var site = new Site();
        _sites.Add(site);
        return site;
    }

    /// <summary>Database A: DOP and USD, 4 decimals, a categorised and an uncategorised income, an unrated USD amount.</summary>
    private static async Task SeedSourceAsync(Site site)
    {
        await using AsyncServiceScope scope = site.Services.CreateAsyncScope();
        IServiceProvider sp = scope.ServiceProvider;
        IReadOnlyList<Category> categories = await sp.GetRequiredService<ICategoryRepository>().GetAllAsync();
        Guid Id(string name) => categories.Single(c => c.Name == name).Id;
        IExpenseRepository expenses = sp.GetRequiredService<IExpenseRepository>();
        IIncomeRepository incomes = sp.GetRequiredService<IIncomeRepository>();

        (string Description, string Category, decimal Amount, Currency Currency, int Day)[] expenseSeeds =
        [
            ("Supermercado", "Comida", 1234.5m, Currency.DOP, 1),
            ("Cafe, con leche", "Comida", 4.5m, Currency.USD, 2),
            ("Precio raro", "Transporte", 12.3456m, Currency.USD, 4),
            ("Sin tasa", "Ocio", Site.NoRateAmount, Currency.USD, 5),
        ];
        foreach ((string description, string category, decimal amount, Currency currency, int day) in expenseSeeds)
        {
            Result added = await expenses.AddAsync(Expense.Create(
                Id(category), new DateOnly(2026, 9, day), Money.Create(amount, currency), description, null, _seedInstant));
            Assert.True(added.IsSuccess, added.Error?.Message);
        }

        (string Description, string? Category, decimal Amount, Currency Currency, int Day)[] incomeSeeds =
        [
            ("Salario", null, 50000m, Currency.DOP, 3),
            ("Regalo", "Ocio", 25.25m, Currency.USD, 6),
        ];
        foreach ((string description, string? category, decimal amount, Currency currency, int day) in incomeSeeds)
        {
            Result added = await incomes.AddAsync(Income.Create(
                category is null ? null : Id(category), new DateOnly(2026, 9, day), Money.Create(amount, currency), description, null, null, _seedInstant));
            Assert.True(added.IsSuccess, added.Error?.Message);
        }
    }

    private static async Task<byte[]> ExportExpenseListAsync(Site site)
    {
        string path = Path.Combine(site.WorkDir, $"list-{Guid.NewGuid():N}.xlsx");
        await using AsyncServiceScope scope = site.Services.CreateAsyncScope();
        ExportExpenseListReportUseCase useCase = scope.ServiceProvider.GetRequiredService<ExportExpenseListReportUseCase>();
        using SaveTarget target = FileTarget(path);
        var filter = new ExpenseFilter { DateFrom = new DateOnly(2026, 9, 1), DateTo = new DateOnly(2026, 9, 30) };

        Result result = await useCase.ExecuteAsync(new ExportExpenseListReportCommand(filter, ReportFormat.Xlsx, ReportTestData.EnLabels()), target);

        Assert.True(result.IsSuccess, result.Error?.Message);
        return await File.ReadAllBytesAsync(path);
    }

    private static async Task<byte[]> ExportMonthSummaryAsync(Site site)
    {
        string path = Path.Combine(site.WorkDir, $"month-{Guid.NewGuid():N}.xlsx");
        await using AsyncServiceScope scope = site.Services.CreateAsyncScope();
        ExportMonthSummaryReportUseCase useCase = scope.ServiceProvider.GetRequiredService<ExportMonthSummaryReportUseCase>();
        using SaveTarget target = FileTarget(path);

        Result result = await useCase.ExecuteAsync(new ExportMonthSummaryReportCommand(_anchor, ReportFormat.Xlsx, ReportTestData.EsLabels()), target);

        Assert.True(result.IsSuccess, result.Error?.Message);
        return await File.ReadAllBytesAsync(path);
    }

    private static SaveTarget FileTarget(string path) => new(
        Path.GetFileName(path),
        _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)));

    private sealed record Snapshot(Dictionary<string, List<string>> Tables);

    private sealed record StoredRow(string Type, DateOnly Date, decimal Amount, Currency Currency, string Description, string? Category);

    /// <summary>One private database with the full real container around it.</summary>
    private sealed class Site : IDisposable
    {
        public const decimal NoRateAmount = 777.77m;

        private readonly ServiceProvider _provider;

        public Site()
        {
            WorkDir = Path.Combine(Path.GetTempPath(), $"import-e2e-{Guid.NewGuid():N}");
            Directory.CreateDirectory(WorkDir);
            DatabasePath = Path.Combine(WorkDir, "data.db");
            Picker = new ScriptedPicker();
            Center = new RecordingCenter();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IClock>(new ImportTestClock(new DateOnly(2026, 9, 30)));
            services.AddSingleton<ICurrencyConverter, FixedRateConverter>();
            services.AddPersistence(new PathResolver(WorkDir, DatabasePath));
            services.AddReportExporters();
            services.AddImport();
            services.AddTransient<GetMonthSummaryQuery>();
            services.AddTransient<GetCategoryBreakdownQuery>();
            services.AddTransient<GetBudgetVsActualQuery>();
            services.AddTransient<ExportMonthSummaryReportUseCase>();
            services.AddTransient<ExportExpenseListReportUseCase>();
            services.AddTransient<ImportPipeline>();
            services.AddTransient<PreviewImportUseCase>();
            services.AddTransient<CommitImportUseCase>();
            services.AddSingleton<IFileOpenDialog>(Picker);
            services.AddSingleton<INotificationCenter>(Center);
            services.AddTransient<ImportViewModel>();
            _provider = services.BuildServiceProvider();

            using IServiceScope scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
            SqliteConnection.ClearAllPools();
        }

        public string WorkDir { get; }

        public string DatabasePath { get; }

        public ScriptedPicker Picker { get; }

        public RecordingCenter Center { get; }

        public IServiceProvider Services => _provider;

        public async Task<ImportPreview> PreviewAsync(byte[] bytes, ImportOptions? options = null)
        {
            PreviewImportUseCase useCase = _provider.GetRequiredService<PreviewImportUseCase>();
            Result result = await useCase.ExecuteAsync(new ImportSource("import", bytes), options ?? DefaultOptions());
            Assert.True(result.IsSuccess, result.Error?.Code + " " + result.Error?.Message);
            return (ImportPreview)result.Value!;
        }

        /// <summary>Previews, then commits with the previewed hash and the SAME options instance.</summary>
        public async Task<ImportCommitResult> ImportOkAsync(byte[] bytes, ImportOptions? options = null)
        {
            options ??= DefaultOptions();
            ImportPreview preview = await PreviewAsync(bytes, options);
            CommitImportUseCase commit = _provider.GetRequiredService<CommitImportUseCase>();
            Result result = await commit.ExecuteAsync(new ImportSource("import", bytes), options, preview.PlanHash);
            Assert.True(result.IsSuccess, result.Error?.Code + " " + result.Error?.Message);
            return (ImportCommitResult)result.Value!;
        }

        public (long Expenses, long Incomes, long Categories) Counts() =>
            (Count("Expenses"), Count("Incomes"), Count("Categories"));

        public Snapshot Snapshot()
        {
            SqliteConnection.ClearAllPools();
            return new Snapshot(BackupTestDb.Snapshot(DatabasePath));
        }

        public async Task<List<StoredRow>> ReadRowsAsync()
        {
            await using AsyncServiceScope scope = _provider.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Dictionary<Guid, string> names = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name);
            List<Expense> expenses = await db.Expenses.AsNoTracking().ToListAsync();
            List<Income> incomes = await db.Incomes.AsNoTracking().ToListAsync();

            return expenses
                .Select(e => new StoredRow("Expense", e.Date, e.Amount.Amount, e.Amount.Currency, e.Description, names[e.CategoryId]))
                .Concat(incomes.Select(i => new StoredRow(
                    "Income", i.Date, i.Amount.Amount, i.Amount.Currency, i.Description, i.CategoryId is Guid id ? names[id] : null)))
                .OrderBy(r => r.Type, StringComparer.Ordinal)
                .ThenBy(r => r.Date)
                .ThenBy(r => r.Description, StringComparer.Ordinal)
                .ToList();
        }

        public void Dispose()
        {
            _provider.Dispose();
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(WorkDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup.
            }
        }

        private static ImportOptions DefaultOptions() => new(null, null, DecimalStyle.Auto, ImportDateFormat.DayMonthYear);

        private long Count(string table)
        {
            SqliteConnection.ClearAllPools();
            using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
            return (long)command.ExecuteScalar()!;
        }
    }

    private sealed class PathResolver(string dataDirectory, string databasePath) : IPathResolver
    {
        public string DataDirectory => dataDirectory;

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

            if (source.Amount == Site.NoRateAmount)
            {
                return Task.FromResult(Result.Failure("RateUnavailable", "No exchange rate."));
            }

            return Task.FromResult(Result.Success(Money.Create(source.Amount * 60m, target)));
        }

        public Task<MoneyDisplay> DisplayAsync(Money source, CancellationToken ct = default) =>
            Task.FromResult(new MoneyDisplay(source, null, null));
    }

    private sealed class ScriptedPicker : IFileOpenDialog
    {
        public OpenTarget? Result { get; set; }

        public int Calls { get; private set; }

        public Task<OpenTarget?> PickAsync(FileTypeFilter filter, string? title = null, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingCenter : INotificationCenter
    {
        private readonly TaskCompletionSource _refreshed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _tryRefreshCalls;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public int TryRefreshCalls => Volatile.Read(ref _tryRefreshCalls);

        public Task RefreshObserved => _refreshed.Task;

        public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> TryRefreshAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref _tryRefreshCalls);
            _refreshed.TrySetResult();
            return Task.FromResult(true);
        }

        public Task<NotificationListDto?> GetAsync(CancellationToken ct = default) => Task.FromResult<NotificationListDto?>(null);

        public Task MarkReadAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) => Task.CompletedTask;

        public Task DismissAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IDisposable> SuspendAsync(CancellationToken ct = default) => Task.FromResult<IDisposable>(new Noop());

        private sealed class Noop : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
