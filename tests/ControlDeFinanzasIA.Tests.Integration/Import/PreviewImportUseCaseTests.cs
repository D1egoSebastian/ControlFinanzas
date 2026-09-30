using System.Globalization;
using System.Text;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using ControlDeFinanzasIA.Application.UseCases.Import.PreviewImport;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Infrastructure.Import;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>
/// Tests of the REAL <see cref="PreviewImportUseCase"/> with the REAL <see cref="SniffingTabularReader"/> and
/// <see cref="EfImportLookup"/> over a real migrated SQLite file and real CSV / XLSX bytes (F-020, S4b).
/// </summary>
public sealed class PreviewImportUseCaseTests
{
    private const string _header = "Date,Type,Description,Category,Amount,Currency";
    private static readonly DateOnly _today = new(2026, 9, 30);

    private static ImportOptions Options(
        DecimalStyle style = DecimalStyle.Auto,
        ImportDateFormat dateFormat = ImportDateFormat.DayMonthYear,
        ColumnMapping? mapping = null,
        bool skipDuplicates = true,
        bool createCategories = true) =>
        new(mapping, null, style, dateFormat, SkipDuplicates: skipDuplicates, CreateMissingCategories: createCategories);

    private static ImportPipeline CreatePipeline(ImportTestDb db) =>
        new(
            new SniffingTabularReader(new CsvTableReader(), new ClosedXmlTableReader()),
            new EfImportLookup(db.Scopes, NullLogger<EfImportLookup>.Instance),
            new ImportTestClock(_today));

    private static PreviewImportUseCase CreateUseCase(ImportTestDb db) =>
        new(CreatePipeline(db), NullLogger<PreviewImportUseCase>.Instance);

    private static byte[] Csv(params string[] lines) => new UTF8Encoding(false).GetBytes(string.Join("\n", lines) + "\n");

    private static ImportSource Source(byte[] bytes, string name = "import.csv") => new(name, bytes);

    private static async Task<ImportPreview> PreviewAsync(ImportTestDb db, byte[] bytes, ImportOptions? options = null)
    {
        Result result = await CreateUseCase(db).ExecuteAsync(Source(bytes), options ?? Options());
        Assert.True(result.IsSuccess, result.Error?.Code + " " + result.Error?.Message);
        return (ImportPreview)result.Value!;
    }

    private static async Task<Result> PreviewResultAsync(ImportTestDb db, byte[] bytes, ImportOptions? options = null) =>
        await CreateUseCase(db).ExecuteAsync(Source(bytes), options ?? Options());

    [Fact]
    public async Task HappyPath_ThreeValidRows_AutoMappedWithNoMappingStep()
    {
        using var db = new ImportTestDb();

        ImportPreview preview = await PreviewAsync(
            db,
            Csv(
                _header,
                "2026-03-01,Expense,Supermercado,Comida,1234.50,DOP",
                "2026-03-02,Expense,Taxi,Transporte,15.25,USD",
                "2026-03-03,Income,Salario,,50000,DOP"));

        Assert.True(preview.Mapping.IsComplete);
        Assert.Empty(preview.MissingFields);
        Assert.Empty(preview.ConflictingFields);
        Assert.Equal(ImportFileKind.Csv, preview.Source.Kind);
        Assert.Equal(',', preview.Source.Delimiter);
        Assert.Equal(3, preview.TotalRows);
        Assert.Equal(3, preview.ToImportCount);
        Assert.Equal(0, preview.RejectedCount);
        Assert.Equal(0, preview.DuplicateCount);
        Assert.Equal(0, preview.WarningCount);
        Assert.Matches("^[0-9a-f]{64}$", preview.PlanHash);
        Assert.Equal(3, preview.SampleRows.Count);
        Assert.Equal(1234.5m, preview.SampleRows[0].Amount);
        Assert.Equal("Comida", preview.SampleRows[0].CategoryName);
        Assert.Equal(Currency.USD, preview.SampleRows[1].Currency);
        Assert.Equal(TransactionType.Income, preview.SampleRows[2].Type);
        Assert.Null(preview.SampleRows[2].CategoryName);
    }

    [Fact]
    public async Task MixedFile_CountsValidDuplicateRejectedAndWarningRows()
    {
        using var db = new ImportTestDb();
        Guid comida = (await CategoryIdAsync(db, "Comida"));
        db.SeedExpense(comida, new DateOnly(2026, 3, 1), 100m, Currency.DOP, "Coffee");

        ImportPreview preview = await PreviewAsync(
            db,
            Csv(
                _header,
                "2026-03-01,Expense,Coffee,Comida,100.00,DOP",
                "2026-03-02,Expense,Lunch,Comida,250.50,DOP",
                "2026-03-03,Expense,Bad,Comida,abc,DOP",
                "2026-03-04,Expense,NoCat,,75,DOP",
                "2026-03-05,Income,Salary,Nomina,1000,DOP"));

        Assert.Equal(5, preview.TotalRows);
        Assert.Equal(3, preview.ToImportCount);
        Assert.Equal(1, preview.DuplicateCount);
        Assert.Equal(1, preview.RejectedCount);
        Assert.Equal(1, preview.WarningCount);
        Assert.Equal(new[] { "Nomina" }, preview.CategoriesToCreate);
        Assert.Equal(1, preview.ReasonCounts.Single(c => c.Reason == ImportReason.InvalidAmount).Count);
        Assert.Equal(1, preview.ReasonCounts.Single(c => c.Reason == ImportReason.CategoryFallback).Count);
        Assert.Equal(ImportRowStatus.Duplicate, preview.SampleRows[0].Status);
        Assert.Equal("Otros", preview.SampleRows[3].CategoryName);
    }

    [Fact]
    public async Task Preview_ProblemRowsBeyondSample()
    {
        using var db = new ImportTestDb();
        var lines = new List<string> { _header };
        for (int i = 1; i <= 6000; i++)
        {
            string amount = i == 4999 ? "not-a-number" : "10";
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"2026-03-01,Expense,Item {i},Comida,{amount},DOP"));
        }

        ImportPreview preview = await PreviewAsync(db, Csv(lines.ToArray()));

        Assert.Equal(6000, preview.TotalRows);
        Assert.Equal(ImportLimits.PreviewRows, preview.SampleRows.Count);
        Assert.DoesNotContain(preview.SampleRows, r => r.Status == ImportRowStatus.Rejected);
        ImportRowResult problem = Assert.Single(preview.ProblemRows);
        Assert.Equal(5000, problem.RowNumber);
        Assert.Equal(ImportRowStatus.Rejected, problem.Status);
        Assert.Contains(ImportReason.InvalidAmount, problem.Reasons);
        Assert.Equal(1, preview.RejectedCount);
    }

    [Fact]
    public async Task Preview_RejectedRowsBeyondTheFirstFiftyAreStillCounted()
    {
        using var db = new ImportTestDb();
        var lines = new List<string> { _header };
        for (int i = 1; i <= 120; i++)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"2026-03-01,Expense,Bad {i},Comida,abc,DOP"));
        }

        ImportPreview preview = await PreviewAsync(db, Csv(lines.ToArray()));

        Assert.Equal(120, preview.RejectedCount);
        Assert.Equal(0, preview.ToImportCount);
        Assert.Equal(ImportLimits.PreviewRows, preview.ProblemRows.Count);
        Assert.Equal(Enumerable.Range(2, 50), preview.ProblemRows.Select(r => r.RowNumber));
        Assert.Equal(120, preview.ReasonCounts.Single(c => c.Reason == ImportReason.InvalidAmount).Count);
    }

    [Fact]
    public async Task Preview_ReasonCounts_SumMatchesTheReasonsOfEveryRow()
    {
        using var db = new ImportTestDb();

        ImportPreview preview = await PreviewAsync(
            db,
            Csv(
                _header,
                "2026-03-01,Expense,A,,10,DOP",
                "2026-03-02,Expense,B,,abc,DOP",
                "2026-03-03,Expense,C,Comida,1,EUR",
                "2027-03-03,Expense,D,Comida,1,DOP"));

        Assert.Equal(preview.SampleRows.Sum(r => r.Reasons.Count), preview.ReasonCounts.Sum(c => c.Count));
        Assert.Contains(preview.ReasonCounts, c => c.Reason == ImportReason.FutureDate);
        Assert.Contains(preview.ReasonCounts, c => c.Reason == ImportReason.UnsupportedCurrency);
    }

    [Fact]
    public async Task Preview_CategoriesToCreate_ListsEachUnknownNameOnce()
    {
        using var db = new ImportTestDb();

        ImportPreview preview = await PreviewAsync(
            db,
            Csv(
                _header,
                "2026-03-01,Expense,A,Mascotas,1,DOP",
                "2026-03-02,Expense,B,MASCOTAS,1,DOP",
                "2026-03-03,Expense,C,Viajes,1,DOP",
                "2026-03-04,Expense,D,Comida,1,DOP",
                "2026-03-05,Income,E,Bonos,1,DOP"));

        Assert.Equal(new[] { "Mascotas", "Viajes", "Bonos" }, preview.CategoriesToCreate);
    }

    [Fact]
    public async Task Preview_CategoriesToCreate_HonorsTheCapOfFifty()
    {
        using var db = new ImportTestDb();
        var lines = new List<string> { _header };
        for (int i = 1; i <= 55; i++)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"2026-03-01,Expense,Item {i},Categoria numero {i},1,DOP"));
        }

        ImportPreview preview = await PreviewAsync(db, Csv(lines.ToArray()));

        Assert.Equal(ImportLimits.MaxNewCategories, preview.CategoriesToCreate.Count);
        Assert.Equal(5, preview.RejectedCount);
        Assert.Equal(5, preview.ReasonCounts.Single(c => c.Reason == ImportReason.TooManyNewCategories).Count);
        Assert.Equal(50, preview.ToImportCount);
    }

    [Fact]
    public async Task CreationOff_UnknownExpenseCategoryFallsBackToOtros()
    {
        using var db = new ImportTestDb();

        ImportPreview preview = await PreviewAsync(db, Csv(_header, "2026-03-01,Expense,A,Mascotas,1,DOP"), Options(createCategories: false));

        Assert.Empty(preview.CategoriesToCreate);
        Assert.Equal("Otros", preview.SampleRows[0].CategoryName);
        Assert.Contains(ImportReason.CategoryFallback, preview.SampleRows[0].Reasons);
    }

    [Theory]
    [InlineData(DecimalStyle.Auto)]
    [InlineData(DecimalStyle.Point)]
    [InlineData(DecimalStyle.Comma)]
    public async Task Xlsx_NumberCell_IgnoresCommaStyle(DecimalStyle style)
    {
        using var db = new ImportTestDb();

        ImportPreview preview = await PreviewAsync(db, await XlsxFixtures.ExpenseListAsync(), Options(style));

        Assert.Equal(ImportFileKind.Xlsx, preview.Source.Kind);
        Assert.Equal(new decimal?[] { 1234.5m, 4.5m, 12.3456m }, preview.SampleRows.Select(r => r.Amount).ToArray());
        Assert.Equal(new Currency?[] { Currency.DOP, Currency.USD, Currency.USD }, preview.SampleRows.Select(r => r.Currency).ToArray());
        Assert.Equal(0, preview.RejectedCount);
    }

    [Fact]
    public async Task Xlsx_ExpenseListReport_ImportsFieldForField()
    {
        using var db = new ImportTestDb();

        ImportPreview preview = await PreviewAsync(db, await XlsxFixtures.ExpenseListAsync());

        Assert.Empty(preview.MissingFields);
        Assert.Equal(3, preview.ToImportCount);
        Assert.Equal(new DateOnly(2026, 9, 1), preview.SampleRows[0].Date);
        Assert.Equal("Supermercado", preview.SampleRows[0].Description);
        Assert.Equal("Comida", preview.SampleRows[0].CategoryName);
        Assert.False(preview.SampleRows[0].IsNewCategory);
        Assert.Equal("Otros", preview.SampleRows[1].CategoryName);
        Assert.Contains(ImportReason.CategoryFallback, preview.SampleRows[1].Reasons);
        Assert.All(preview.SampleRows, r => Assert.Equal(TransactionType.Expense, r.Type));
    }

    [Fact]
    public async Task Xlsx_MonthSummaryReport_ImportsExpensesAndIncomesWithoutTheAmountDopColumn()
    {
        using var db = new ImportTestDb();

        ImportPreview preview = await PreviewAsync(db, await XlsxFixtures.MonthSummaryAsync(), Options(DecimalStyle.Comma));

        Assert.Equal(4, preview.TotalRows);
        Assert.Equal(4, preview.ToImportCount);
        Assert.Equal(new decimal?[] { 1234.5m, 4.5m, 50000m, 12.3456m }, preview.SampleRows.Select(r => r.Amount).ToArray());
        ImportRowResult income = preview.SampleRows[2];
        Assert.Equal(TransactionType.Income, income.Type);
        Assert.Null(income.CategoryName);
        Assert.Empty(income.Reasons);
    }

    [Fact]
    public async Task PlanHash_IsIdenticalAcrossTwoPreviewsOfTheSameBytesOptionsAndDatabase()
    {
        using var db = new ImportTestDb();
        byte[] bytes = Csv(_header, "2026-03-01,Expense,A,Comida,1,DOP", "2026-03-02,Expense,B,Nueva,2,DOP", "2026-03-03,Expense,C,Comida,x,DOP");

        ImportPreview first = await PreviewAsync(db, bytes);
        ImportPreview second = await PreviewAsync(db, bytes);

        Assert.Equal(first.PlanHash, second.PlanHash);
        Assert.Equal(first.SampleRows, second.SampleRows, new RowResultComparer());
    }

    [Fact]
    public async Task PlanHash_ChangesWhenAnExpenseIsAddedThatChangesADuplicateClassification()
    {
        using var db = new ImportTestDb();
        byte[] bytes = Csv(_header, "2026-03-01,Expense,Coffee,Comida,100,DOP");
        string before = (await PreviewAsync(db, bytes)).PlanHash;

        db.SeedExpense(await CategoryIdAsync(db, "Comida"), new DateOnly(2026, 3, 1), 100m, Currency.DOP, "Coffee");
        ImportPreview after = await PreviewAsync(db, bytes);

        Assert.NotEqual(before, after.PlanHash);
        Assert.Equal(1, after.DuplicateCount);
    }

    [Fact]
    public async Task PlanHash_ChangesWhenAnOptionFlips()
    {
        using var db = new ImportTestDb();
        db.SeedExpense(await CategoryIdAsync(db, "Comida"), new DateOnly(2026, 3, 1), 100m, Currency.DOP, "Coffee");
        byte[] bytes = Csv(_header, "2026-03-01,Expense,Coffee,Comida,100,DOP", "03/04/2026,Expense,Ambiguous,Comida,5,DOP");

        string skip = (await PreviewAsync(db, bytes, Options(skipDuplicates: true))).PlanHash;
        string keep = (await PreviewAsync(db, bytes, Options(skipDuplicates: false))).PlanHash;
        string dayFirst = (await PreviewAsync(db, bytes, Options(dateFormat: ImportDateFormat.DayMonthYear))).PlanHash;
        string monthFirst = (await PreviewAsync(db, bytes, Options(dateFormat: ImportDateFormat.MonthDayYear))).PlanHash;

        Assert.NotEqual(skip, keep);
        Assert.NotEqual(dayFirst, monthFirst);
    }

    [Fact]
    public async Task PlanHash_ChangesWhenTheFileChanges()
    {
        using var db = new ImportTestDb();

        string a = (await PreviewAsync(db, Csv(_header, "2026-03-01,Expense,A,Comida,1,DOP"))).PlanHash;
        string b = (await PreviewAsync(db, Csv(_header, "2026-03-01,Expense,A,Comida,1.5,DOP"))).PlanHash;

        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task PlanHash_IsCultureIndependent()
    {
        using var db = new ImportTestDb();
        byte[] bytes = Csv(_header, "2026-03-01,Expense,A,Comida,\"1.234,56\",DOP");
        CultureInfo original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-DO");
            string es = (await PreviewAsync(db, bytes)).PlanHash;
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            string en = (await PreviewAsync(db, bytes)).PlanHash;

            Assert.Equal(es, en);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task PreviewAndCommitPipeline_ProduceTheIdenticalPlanFromTheSameRetainedBytes()
    {
        using var db = new ImportTestDb();
        db.SeedExpense(await CategoryIdAsync(db, "Comida"), new DateOnly(2026, 3, 1), 100m, Currency.DOP, "Coffee");
        byte[] bytes = Csv(
            _header,
            "2026-03-01,Expense,Coffee,Comida,100,DOP",
            "2026-03-01,Expense,Coffee,Comida,100,DOP",
            "2026-03-02,Expense,Lunch,Nuevas,20,DOP",
            "2026-03-03,Expense,Bad,Comida,zz,DOP");
        ImportSource source = Source(bytes);
        ImportOptions options = Options();

        // The commit use case (S5) calls exactly this pipeline with exactly these arguments.
        Result first = await CreatePipeline(db).BuildAsync(source, options, CancellationToken.None);
        Result second = await CreatePipeline(db).BuildAsync(source, options, CancellationToken.None);
        Result preview = await CreateUseCase(db).ExecuteAsync(source, options);

        var planA = (ImportPlan)first.Value!;
        var planB = (ImportPlan)second.Value!;
        Assert.Equal(planA.PlanHash, planB.PlanHash);
        Assert.Equal(planA.PlanHash, ((ImportPreview)preview.Value!).PlanHash);
        Assert.Equal(
            planA.Rows.Select(r => (r.Parsed.RowNumber, r.Status, r.CategoryId, r.CategoryKey)),
            planB.Rows.Select(r => (r.Parsed.RowNumber, r.Status, r.CategoryId, r.CategoryKey)));
        Assert.Equal(
            new[] { ImportRowStatus.Duplicate, ImportRowStatus.Valid, ImportRowStatus.Valid, ImportRowStatus.Rejected },
            planA.Rows.Select(r => r.Status));
    }

    [Fact]
    public async Task LegitimateRepeatedRows_AreKeptAndAReImportIsAllDuplicates()
    {
        using var db = new ImportTestDb();
        byte[] bytes = Csv(_header, "2026-03-01,Expense,Coffee,Comida,100,DOP", "2026-03-01,Expense,Coffee,Comida,100,DOP");

        ImportPreview fresh = await PreviewAsync(db, bytes);
        Assert.Equal(2, fresh.ToImportCount);
        Assert.Equal(0, fresh.DuplicateCount);

        Guid comida = await CategoryIdAsync(db, "Comida");
        db.SeedExpense(comida, new DateOnly(2026, 3, 1), 100m, Currency.DOP, "Coffee");
        ImportPreview oneStored = await PreviewAsync(db, bytes);
        Assert.Equal(1, oneStored.ToImportCount);
        Assert.Equal(1, oneStored.DuplicateCount);
        Assert.Equal(new[] { ImportRowStatus.Duplicate, ImportRowStatus.Valid }, oneStored.SampleRows.Select(r => r.Status));

        db.SeedExpense(comida, new DateOnly(2026, 3, 1), 100m, Currency.DOP, "Coffee");
        ImportPreview reImport = await PreviewAsync(db, bytes);
        Assert.Equal(0, reImport.ToImportCount);
        Assert.Equal(2, reImport.DuplicateCount);
    }

    [Fact]
    public async Task ReImportOfTheRealReport_IsAllDuplicatesOnceStored()
    {
        using var db = new ImportTestDb();
        byte[] xlsx = await XlsxFixtures.MonthSummaryAsync();
        ImportPreview fresh = await PreviewAsync(db, xlsx);
        Assert.Equal(4, fresh.ToImportCount);

        foreach (ImportRowResult row in fresh.SampleRows)
        {
            Guid? category = row.CategoryName is null ? null : await CategoryIdAsync(db, row.CategoryName);
            if (row.Type == TransactionType.Income)
            {
                db.SeedIncome(category, row.Date!.Value, row.Amount!.Value, row.Currency!.Value, row.Description!);
            }
            else
            {
                db.SeedExpense(category!.Value, row.Date!.Value, row.Amount!.Value, row.Currency!.Value, row.Description!);
            }
        }

        ImportPreview again = await PreviewAsync(db, xlsx);

        Assert.Equal(0, again.ToImportCount);
        Assert.Equal(4, again.DuplicateCount);
    }

    [Fact]
    public async Task IncompleteMapping_IsASuccessWithMissingFieldsAndNoEvaluatedRows()
    {
        using var db = new ImportTestDb();

        ImportPreview preview = await PreviewAsync(db, Csv("When,What,How much", "2026-01-01,x,5"));

        Assert.False(preview.Mapping.IsComplete);
        Assert.Equal(new[] { ImportField.Date, ImportField.Description, ImportField.Amount }, preview.MissingFields);
        Assert.Empty(preview.SampleRows);
        Assert.Empty(preview.ProblemRows);
        Assert.Equal(0, preview.ToImportCount);
        Assert.Equal(new[] { "When", "What", "How much" }, preview.Headers);
    }

    [Fact]
    public async Task UserMapping_FixesAnIncompleteAutoMapping()
    {
        using var db = new ImportTestDb();
        byte[] bytes = Csv("When,What,How much", "2026-01-01,x,5");

        ImportPreview preview = await PreviewAsync(db, bytes, Options(mapping: new ColumnMapping(0, null, 1, null, 2, null, null)));

        Assert.Equal(1, preview.ToImportCount);
        Assert.Equal(5m, preview.SampleRows[0].Amount);
    }

    [Fact]
    public async Task DuplicateHeaders_AreReportedAsConflicting()
    {
        using var db = new ImportTestDb();

        ImportPreview preview = await PreviewAsync(db, Csv("Fecha,Date,Description,Amount", "2026-01-01,2026-01-02,x,5"));

        Assert.Contains(ImportField.Date, preview.ConflictingFields);
        Assert.Contains(ImportField.Date, preview.MissingFields);
    }

    [Theory]
    [InlineData(99, 1, 2)]
    [InlineData(-1, 1, 2)]
    [InlineData(0, 0, 2)]
    public async Task UserMapping_OutOfRangeOrReusedIndex_IsMappingIncomplete(int date, int description, int amount)
    {
        using var db = new ImportTestDb();

        Result result = await PreviewResultAsync(
            db,
            Csv("When,What,How much", "2026-01-01,x,5"),
            Options(mapping: new ColumnMapping(date, null, description, null, amount, null, null)));

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.MappingIncomplete, result.Error!.Code);
    }

    [Fact]
    public async Task AllRowsInvalid_IsASuccessWithZeroImportableRows()
    {
        using var db = new ImportTestDb();

        ImportPreview preview = await PreviewAsync(db, Csv(_header, "nope,Expense,A,Comida,1,DOP", "2026-03-01,Expense,,Comida,1,DOP", "2026-03-01,Expense,B,Comida,0,DOP"));

        Assert.Equal(0, preview.ToImportCount);
        Assert.Equal(3, preview.RejectedCount);
        Assert.Equal(3, preview.TotalRows);
        Assert.NotEmpty(preview.PlanHash);
    }

    [Fact]
    public async Task FileLevelFailures_SurfaceAsFailuresWithTheRightCodes()
    {
        using var db = new ImportTestDb();

        Result binary = await PreviewResultAsync(db, [0x41, 0x00, 0x42, 0x00, 0x00]);
        Result tooLarge = await PreviewResultAsync(db, new byte[ImportLimits.MaxFileBytes + 1]);
        Result noRows = await PreviewResultAsync(db, Csv(_header));
        Result empty = await PreviewResultAsync(db, []);
        Result malformed = await PreviewResultAsync(db, Csv(_header, "2026-03-01,Expense,\"unterminated,Comida,1,DOP"));
        Result legacyXls = await PreviewResultAsync(db, [0xD0, 0xCF, 0x11, 0xE0, 1, 2, 3]);

        Assert.Equal(ImportErrorCodes.UnreadableFile, binary.Error!.Code);
        Assert.Equal(ImportErrorCodes.TooLarge, tooLarge.Error!.Code);
        Assert.Equal(ImportErrorCodes.NoRows, noRows.Error!.Code);
        Assert.Equal(ImportErrorCodes.NoRows, empty.Error!.Code);
        Assert.Equal(ImportErrorCodes.UnreadableFile, malformed.Error!.Code);
        Assert.Equal(ImportErrorCodes.ReasonMalformedCsv, malformed.Error.Details![ImportErrorCodes.DetailReason][0]);
        Assert.Equal(ImportErrorCodes.UnreadableFile, legacyXls.Error!.Code);
        Assert.Equal(ImportErrorCodes.ReasonLegacyXls, legacyXls.Error.Details![ImportErrorCodes.DetailReason][0]);
    }

    [Fact]
    public async Task TruncatedFiles_AreFileLevelFailuresNeverASilentShorterImport()
    {
        using var db = new ImportTestDb();
        byte[] csv = Csv(_header, "2026-03-01,Expense,\"Long description, with comma\",Comida,100,DOP");
        int insideTheQuotedField = Encoding.UTF8.GetString(csv).IndexOf("with comma", StringComparison.Ordinal);
        byte[] truncatedCsv = csv[..insideTheQuotedField];
        byte[] xlsx = await XlsxFixtures.ExpenseListAsync();
        byte[] truncatedXlsx = xlsx[..(xlsx.Length / 2)];

        Result cutInsideQuote = await PreviewResultAsync(db, truncatedCsv);
        Result cutXlsx = await PreviewResultAsync(db, truncatedXlsx);

        Assert.False(cutInsideQuote.IsSuccess);
        Assert.Equal(ImportErrorCodes.UnreadableFile, cutInsideQuote.Error!.Code);
        Assert.False(cutXlsx.IsSuccess);
        Assert.Equal(ImportErrorCodes.UnreadableFile, cutXlsx.Error!.Code);
    }

    [Fact]
    public async Task BlankAndCommaOnlyLines_AreNotCountedNorRejected()
    {
        using var db = new ImportTestDb();

        ImportPreview preview = await PreviewAsync(db, Csv(_header, "2026-03-01,Expense,A,Comida,1,DOP", string.Empty, ",,,,,", "2026-03-02,Expense,B,Comida,2,DOP"));

        Assert.Equal(2, preview.TotalRows);
        Assert.Equal(0, preview.RejectedCount);
    }

    [Fact]
    public async Task EvidenceFields_ArePopulatedForTheAmbiguousCases()
    {
        using var db = new ImportTestDb();

        ImportPreview ambiguousAmount = await PreviewAsync(db, Csv(_header, "2026-03-01,Expense,A,Comida,\"1,234\",DOP"));
        ImportPreview commaStyle = await PreviewAsync(db, Csv(_header, "2026-03-01,Expense,A,Comida,\"1.234,56\",DOP"));
        ImportPreview conflict = await PreviewAsync(db, Csv(_header, "2026-03-01,Expense,A,Comida,\"1.234,56\",DOP", "2026-03-01,Expense,B,Comida,\"1,234.56\",DOP"));
        ImportPreview ambiguousDates = await PreviewAsync(db, Csv(_header, "03/04/2026,Expense,A,Comida,1,DOP"));
        ImportPreview contradicting = await PreviewAsync(db, Csv(_header, "25/03/2026,Expense,A,Comida,1,DOP"), Options(dateFormat: ImportDateFormat.MonthDayYear));

        Assert.Equal(1, ambiguousAmount.Decimal.Ambiguous);
        Assert.Contains(ImportReason.AmbiguousAmount, ambiguousAmount.SampleRows[0].Reasons);
        Assert.Equal(DecimalStyle.Comma, commaStyle.Decimal.Resolved);
        Assert.Equal(1234.56m, commaStyle.SampleRows[0].Amount);
        Assert.True(conflict.Decimal.IsConflict);
        Assert.Equal(1, conflict.RejectedCount);
        Assert.Equal(DateEvidence.AmbiguousOnly, ambiguousDates.Dates);
        Assert.Equal(new DateOnly(2026, 4, 3), ambiguousDates.SampleRows[0].Date);
        Assert.Equal(DateEvidence.Contradicts, contradicting.Dates);
    }

    [Fact]
    public async Task UserDecimalStyle_ReinterpretsTheSameBytesAndIsReportedAsResolved()
    {
        using var db = new ImportTestDb();
        byte[] bytes = Csv(_header, "2026-03-01,Expense,A,Comida,\"1.234\",DOP");

        ImportPreview point = await PreviewAsync(db, bytes, Options(DecimalStyle.Point));
        ImportPreview comma = await PreviewAsync(db, bytes, Options(DecimalStyle.Comma));

        Assert.Equal(1.234m, point.SampleRows[0].Amount);
        Assert.Equal(1234m, comma.SampleRows[0].Amount);
        Assert.Equal(DecimalStyle.Comma, comma.Decimal.Resolved);
        Assert.NotEqual(point.PlanHash, comma.PlanHash);
    }

    [Fact]
    public async Task Preview_PerformsNoWrites()
    {
        using var db = new ImportTestDb();
        db.SeedExpense(await CategoryIdAsync(db, "Comida"), new DateOnly(2026, 3, 1), 100m, Currency.DOP, "Coffee");
        Dictionary<string, List<string>> tablesBefore = db.Snapshot();
        string shaBefore = db.LiveSha256();
        byte[] bytes = Csv(_header, "2026-03-01,Expense,Coffee,Comida,100,DOP", "2026-03-02,Expense,New,Brand New Category,5,DOP", "2026-03-03,Expense,Bad,Comida,zz,DOP");

        ImportPreview preview = await PreviewAsync(db, bytes);
        await PreviewAsync(db, await XlsxFixtures.MonthSummaryAsync());

        Assert.Equal(1, preview.ToImportCount);
        Dictionary<string, List<string>> tablesAfter = db.Snapshot();
        Assert.Equal(tablesBefore.Keys.OrderBy(k => k, StringComparer.Ordinal), tablesAfter.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (string table in tablesBefore.Keys)
        {
            Assert.Equal(tablesBefore[table], tablesAfter[table]);
        }

        Assert.Equal(shaBefore, db.LiveSha256());
    }

    [Fact]
    public async Task PaymentMethodColumn_ResolvesExistingAndWarnsForUnknown()
    {
        using var db = new ImportTestDb();
        db.SeedPaymentMethod("Visa Gold");
        byte[] bytes = Csv(
            "Date,Description,Amount,Category,Payment Method",
            "2026-03-01,A,1,Comida,visa gold",
            "2026-03-02,B,2,Comida,Nope");

        ImportPreview preview = await PreviewAsync(db, bytes);

        Assert.DoesNotContain(ImportReason.PaymentMethodNotFound, preview.SampleRows[0].Reasons);
        Assert.Contains(ImportReason.PaymentMethodNotFound, preview.SampleRows[1].Reasons);
        Assert.Equal(1, preview.WarningCount);
    }

    private static async Task<Guid> CategoryIdAsync(ImportTestDb db, string name)
    {
        var lookup = new EfImportLookup(db.Scopes, NullLogger<EfImportLookup>.Instance);
        Result result = await lookup.LoadAsync(null, null);
        var snapshot = (ImportLookupSnapshot)result.Value!;
        return snapshot.Categories.Single(c => CategoryMatcher.FoldKey(c.Name) == CategoryMatcher.FoldKey(name)).Id;
    }

    [Fact]
    public async Task RealXlsx_AmountWithMoreThanFourRealDecimals_IsRejectedNotRounded_WhileIeeeNoiseIsAccepted()
    {
        using var wb = new ClosedXML.Excel.XLWorkbook();
        ClosedXML.Excel.IXLWorksheet ws = wb.AddWorksheet("Data");
        string[] headers = ["Date", "Type", "Description", "Category", "Amount", "Currency"];
        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
        }

        void Row(int r, string description, double amount)
        {
            ws.Cell(r, 1).Value = "2026-03-01";
            ws.Cell(r, 2).Value = "Expense";
            ws.Cell(r, 3).Value = description;
            ws.Cell(r, 4).Value = "Otros";
            ws.Cell(r, 5).Value = amount;
            ws.Cell(r, 6).Value = "DOP";
        }

        Row(2, "Noise", 12.300000000000001d);
        Row(3, "FiveDecimals", 12.34567d);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        using var db = new ImportTestDb();
        ImportPreview preview = await PreviewAsync(db, ms.ToArray());

        ImportRowResult noise = Assert.Single(preview.SampleRows, r => r.Description == "Noise");
        Assert.Equal(ImportRowStatus.Valid, noise.Status);
        Assert.Equal(12.3m, noise.Amount);
        ImportRowResult bad = Assert.Single(preview.SampleRows, r => r.Description == "FiveDecimals");
        Assert.Equal(ImportRowStatus.Rejected, bad.Status);
        Assert.Contains(ImportReason.TooManyDecimals, bad.Reasons);
        Assert.Equal(1, preview.ToImportCount);
    }

    [Fact]
    public async Task Windows1252Csv_WithUndefinedByte_RejectsTheRowInsteadOfImportingTheReplacementCharacter()
    {
        // 0x81 is undefined in Windows-1252; 0xE9 is "e acute". The file is not valid UTF-8, so the fallback decodes it.
        byte[] header = Encoding.ASCII.GetBytes(_header + "\n");
        byte[] good = [.. Encoding.ASCII.GetBytes("2026-03-01,Expense,Caf"), 0xE9, .. Encoding.ASCII.GetBytes(",Comida,10,DOP\n")];
        byte[] bad = [.. Encoding.ASCII.GetBytes("2026-03-02,Expense,Bad"), 0x81, .. Encoding.ASCII.GetBytes(",Comida,20,DOP\n")];
        byte[] badCategory = [.. Encoding.ASCII.GetBytes("2026-03-03,Expense,Plain,Cat"), 0x81, .. Encoding.ASCII.GetBytes(",30,DOP\n")];
        byte[] csv = [.. header, .. good, .. bad, .. badCategory];

        using var db = new ImportTestDb();
        ImportPreview preview = await PreviewAsync(db, csv);

        Assert.True(preview.Source.EncodingFallback);
        Assert.Equal("Windows-1252", preview.Source.EncodingName);
        Assert.Equal(1, preview.ToImportCount);
        Assert.Equal(2, preview.RejectedCount);
        ImportRowResult ok = Assert.Single(preview.SampleRows, r => r.Status == ImportRowStatus.Valid);
        Assert.Equal("Café", ok.Description);
        Assert.All(
            preview.SampleRows.Where(r => r.Status == ImportRowStatus.Rejected),
            r => Assert.Contains(ImportReason.UndecodableText, r.Reasons));
        Assert.DoesNotContain(preview.CategoriesToCreate, name => name.Contains((char)0xFFFD));
    }

    private sealed class RowResultComparer : IEqualityComparer<ImportRowResult>
    {
        public bool Equals(ImportRowResult? x, ImportRowResult? y) =>
            x is not null && y is not null
            && x.RowNumber == y.RowNumber && x.Status == y.Status && x.Date == y.Date && x.Type == y.Type
            && x.Amount == y.Amount && x.Currency == y.Currency && x.Description == y.Description
            && x.CategoryName == y.CategoryName && x.IsNewCategory == y.IsNewCategory && x.Reasons.SequenceEqual(y.Reasons);

        public int GetHashCode(ImportRowResult obj) => HashCode.Combine(obj.RowNumber, obj.Status);
    }
}
