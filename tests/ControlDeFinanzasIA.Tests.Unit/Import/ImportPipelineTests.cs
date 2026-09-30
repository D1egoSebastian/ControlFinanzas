using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;

namespace ControlDeFinanzasIA.Tests.Unit.Import;

/// <summary>
/// Tests of the REAL <see cref="ImportPipeline"/> and <see cref="ImportPlanner"/> with fakes only for the
/// reader, the lookup and the clock (F-020, S4b). The pipeline is the code Preview and Commit share, so what
/// is pinned here is what keeps the two identical.
/// </summary>
public sealed class ImportPipelineTests
{
    private static readonly string[] _headers = ["Date", "Type", "Description", "Category", "Amount", "Currency"];
    private static readonly DateOnly _today = new(2026, 9, 30);
    private static readonly Guid _comida = new("11111111-1111-1111-1111-000000000001");

    private static ImportOptions Options(
        DecimalStyle style = DecimalStyle.Auto,
        ColumnMapping? mapping = null,
        bool skipDuplicates = true,
        bool createCategories = true,
        char? delimiter = null) =>
        new(mapping, delimiter, style, ImportDateFormat.DayMonthYear, SkipDuplicates: skipDuplicates, CreateMissingCategories: createCategories);

    private static RawTable Table(string[] headers, params string[][] rows) =>
        new(
            headers,
            rows.Select((cells, i) => new RawRow(i + 2, cells.Select(c => new RawCell(c, RawCellKind.Text)).ToList())).ToList(),
            new TableSourceInfo(ImportFileKind.Csv, "UTF-8", false, ',', null, string.Empty));

    private static RawTable Rows(params string[][] rows) => Table(_headers, rows);

    private static string[] Row(string date, string description, string amount, string category = "Comida", string type = "Expense", string currency = "DOP") =>
        [date, type, description, category, amount, currency];

    private static ImportPipeline Pipeline(FakeTabularReader reader, FakeImportLookup lookup) => new(reader, lookup, new FakeClock { Today = _today, UtcNow = DateTime.UtcNow });

    private static ImportSource Source() => new("file.csv", [1, 2, 3]);

    private static async Task<ImportPlan> BuildAsync(RawTable table, ImportOptions? options = null, FakeImportLookup? lookup = null)
    {
        Result result = await Pipeline(new FakeTabularReader(table), lookup ?? new FakeImportLookup(FakeImportLookup.Snapshot()))
            .BuildAsync(Source(), options ?? Options(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Code);
        return (ImportPlan)result.Value!;
    }

    private static ExistingTransactionKey Existing(string date, string description, decimal amount) =>
        new(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), TransactionType.Expense, amount, Currency.DOP, description, _comida);

    private static ExistingTransactionKey ExistingIncomeWithoutCategory(string date, string description, decimal amount) =>
        new(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), TransactionType.Income, amount, Currency.DOP, description, null);

    [Fact]
    public async Task Build_ReadsWithTheOptionsDelimiter()
    {
        var reader = new FakeTabularReader(Rows(Row("2026-03-01", "a", "10")));

        await Pipeline(reader, new FakeImportLookup(FakeImportLookup.Snapshot())).BuildAsync(Source(), Options(delimiter: ';'), CancellationToken.None);

        Assert.Equal(';', reader.LastOptions!.Delimiter);
    }

    [Fact]
    public async Task Build_SpanPassedToLookup_IsMinAndMaxOfNonRejectedRows()
    {
        var lookup = new FakeImportLookup(FakeImportLookup.Snapshot());

        await BuildAsync(
            Rows(
                Row("2026-03-05", "late", "10"),
                Row("2026-03-01", "early", "10"),
                Row("2025-01-01", "rejected earlier", "not-a-number"),
                Row("2027-01-01", "rejected later", "0")),
            lookup: lookup);

        Assert.Equal(new DateOnly(2026, 3, 1), lookup.LastMin);
        Assert.Equal(new DateOnly(2026, 3, 5), lookup.LastMax);
    }

    [Fact]
    public async Task Build_AllRowsRejected_LooksUpWithoutASpan()
    {
        var lookup = new FakeImportLookup(FakeImportLookup.Snapshot());

        ImportPlan plan = await BuildAsync(Rows(Row("2026-03-05", "x", "abc"), Row("nope", "y", "10")), lookup: lookup);

        Assert.Equal(1, lookup.Calls);
        Assert.Null(lookup.LastMin);
        Assert.Null(lookup.LastMax);
        Assert.All(plan.Rows, r => Assert.Equal(ImportRowStatus.Rejected, r.Status));
        Assert.NotEmpty(plan.PlanHash);
    }

    [Fact]
    public async Task Build_BlankRows_AreFilteredBeforeParsing()
    {
        ImportPlan plan = await BuildAsync(
            Rows(
                Row("2026-03-01", "a", "10"),
                ["", "", "", "", "", ""],
                [""],
                ["   ", "", "", "", "", "  "],
                Row("2026-03-02", "b", "20")));

        Assert.Equal(2, plan.TotalRows);
        Assert.Equal(2, plan.Rows.Count);
        Assert.DoesNotContain(plan.Rows.SelectMany(r => r.Reasons), r => r == ImportReason.ColumnCountMismatch);
    }

    [Fact]
    public async Task Build_IncompleteMapping_ReturnsAnUnevaluatedPlanAndSkipsTheLookup()
    {
        var lookup = new FakeImportLookup(FakeImportLookup.Snapshot());

        ImportPlan plan = await BuildAsync(Table(["When", "What", "How much"], ["2026-01-01", "x", "5"]), lookup: lookup);

        Assert.False(plan.Mapping.IsComplete);
        Assert.Equal(new[] { ImportField.Date, ImportField.Description, ImportField.Amount }, plan.MissingFields);
        Assert.Empty(plan.Rows);
        Assert.Equal(1, plan.TotalRows);
        Assert.Equal(0, lookup.Calls);
    }

    [Fact]
    public async Task Build_DuplicateHeaders_AreReportedAsConflictingAndStayUnmapped()
    {
        ImportPlan plan = await BuildAsync(Table(["Fecha", "Date", "Description", "Amount"], ["2026-01-01", "2026-01-01", "x", "5"]));

        Assert.Contains(ImportField.Date, plan.ConflictingFields);
        Assert.Contains(ImportField.Date, plan.MissingFields);
        Assert.False(plan.Mapping.IsComplete);
    }

    [Fact]
    public async Task Build_UserMapping_IsUsedInsteadOfTheAutoMapping()
    {
        RawTable table = Table(["When", "What", "How much"], ["2026-01-01", "Taxi", "5"]);

        ImportPlan plan = await BuildAsync(table, Options(mapping: new ColumnMapping(0, null, 1, null, 2, null, null)));

        PlannedRow row = Assert.Single(plan.Rows);
        Assert.Equal(ImportRowStatus.Valid, row.Status);
        Assert.Equal("Taxi", row.Parsed.Description);
        Assert.Equal(5m, row.Parsed.Amount);
    }

    [Theory]
    [InlineData(99, 1, 2)]
    [InlineData(-1, 1, 2)]
    [InlineData(0, 0, 2)]
    [InlineData(0, 1, 1)]
    public async Task Build_UserMapping_OutOfRangeOrReusedIndex_IsMappingIncomplete(int date, int description, int amount)
    {
        RawTable table = Table(["When", "What", "How much"], ["2026-01-01", "Taxi", "5"]);

        Result result = await Pipeline(new FakeTabularReader(table), new FakeImportLookup(FakeImportLookup.Snapshot()))
            .BuildAsync(Source(), Options(mapping: new ColumnMapping(date, null, description, null, amount, null, null)), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.MappingIncomplete, result.Error!.Code);
    }

    [Fact]
    public async Task Build_ReaderFailure_IsReturnedUnchangedAndTheLookupIsNotCalled()
    {
        var failure = Result.Failure(ImportErrorCodes.UnreadableFile, "bad");
        var lookup = new FakeImportLookup(FakeImportLookup.Snapshot());

        Result result = await Pipeline(new FakeTabularReader(failure), lookup).BuildAsync(Source(), Options(), CancellationToken.None);

        Assert.Same(failure, result);
        Assert.Equal(0, lookup.Calls);
    }

    [Fact]
    public async Task Build_LookupFailure_IsReturnedUnchanged()
    {
        var failure = Result.Failure(ImportErrorCodes.LookupFailed, "db");

        Result result = await Pipeline(new FakeTabularReader(Rows(Row("2026-03-01", "a", "10"))), new FakeImportLookup(() => failure))
            .BuildAsync(Source(), Options(), CancellationToken.None);

        Assert.Same(failure, result);
    }

    [Fact]
    public async Task Build_CancelledToken_IsRethrown()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Pipeline(new FakeTabularReader(Rows(Row("2026-03-01", "a", "10"))), new FakeImportLookup(FakeImportLookup.Snapshot()))
                .BuildAsync(Source(), Options(), cts.Token));
    }

    [Fact]
    public async Task Build_UserDecimalStyle_IsHonoredAndReportedAsResolved()
    {
        RawTable table = Rows(Row("2026-03-01", "a", "1.234"));

        ImportPlan comma = await BuildAsync(table, Options(DecimalStyle.Comma));
        ImportPlan point = await BuildAsync(table, Options(DecimalStyle.Point));
        ImportPlan auto = await BuildAsync(table, Options(DecimalStyle.Auto));

        Assert.Equal(1234m, comma.Rows[0].Parsed.Amount);
        Assert.Equal(DecimalStyle.Comma, comma.Decimal.Resolved);
        Assert.Equal(1.234m, point.Rows[0].Parsed.Amount);
        Assert.Equal(DecimalStyle.Point, point.Decimal.Resolved);
        Assert.Equal(1.234m, auto.Rows[0].Parsed.Amount);
        Assert.Equal(1, auto.Decimal.Ambiguous);
        Assert.Contains(ImportReason.AmbiguousAmount, auto.Rows[0].Reasons);
    }

    [Fact]
    public async Task Build_DecimalEvidence_IgnoresRowsWithTheWrongColumnCount()
    {
        // A shifted row carries a comma-style token in its amount position. It is rejected, so it must not sway
        // the file-level decision: the three real rows say Point.
        ImportPlan plan = await BuildAsync(
            Rows(
                Row("2026-03-01", "a", "1,234.50"),
                Row("2026-03-02", "b", "2,000.25"),
                Row("2026-03-03", "c", "3.10"),
                ["2026-03-04", "Expense", "d", "Comida", "1.234,56"]));

        Assert.Equal(DecimalStyle.Point, plan.Decimal.Resolved);
        Assert.Equal(0, plan.Decimal.CommaEvidence);
        Assert.Contains(ImportReason.ColumnCountMismatch, plan.Rows[3].Reasons);
    }

    [Fact]
    public async Task Build_DateEvidence_ReflectsTheSelectedFormat()
    {
        ImportPlan contradicts = await BuildAsync(Rows(Row("25/03/2026", "a", "10")), Options() with { DateFormat = ImportDateFormat.MonthDayYear });
        ImportPlan ambiguous = await BuildAsync(Rows(Row("03/04/2026", "a", "10")));
        ImportPlan iso = await BuildAsync(Rows(Row("2026-03-04", "a", "10")));

        Assert.Equal(DateEvidence.Contradicts, contradicts.Dates);
        Assert.Equal(DateEvidence.AmbiguousOnly, ambiguous.Dates);
        Assert.Equal(DateEvidence.NotApplicable, iso.Dates);
    }

    [Fact]
    public async Task Build_LegitimateRepeatedRows_AreAllKeptWhenNothingExists()
    {
        ImportPlan plan = await BuildAsync(Rows(Row("2026-03-01", "Coffee", "100"), Row("2026-03-01", "Coffee", "100")));

        Assert.All(plan.Rows, r => Assert.Equal(ImportRowStatus.Valid, r.Status));
    }

    [Fact]
    public async Task Build_OccurrenceCounting_OneStoredRowAbsorbsOnlyTheFirstRepeat()
    {
        var lookup = new FakeImportLookup(FakeImportLookup.Snapshot(existing: [Existing("2026-03-01", "coffee", 100m)]));

        ImportPlan plan = await BuildAsync(Rows(Row("2026-03-01", "Coffee", "100"), Row("2026-03-01", "Coffee", "100")), lookup: lookup);

        Assert.Equal(new[] { ImportRowStatus.Duplicate, ImportRowStatus.Valid }, plan.Rows.Select(r => r.Status));
    }

    [Fact]
    public async Task Build_ReImport_MarksEveryRowAsDuplicate()
    {
        var lookup = new FakeImportLookup(FakeImportLookup.Snapshot(existing:
        [
            Existing("2026-03-01", "Coffee", 100m),
            Existing("2026-03-01", "Coffee", 100m),
            ExistingIncomeWithoutCategory("2026-03-02", "Salary", 500m),
        ]));

        ImportPlan plan = await BuildAsync(
            Rows(
                Row("2026-03-01", "Coffee", "100"),
                Row("2026-03-01", "Coffee", "100"),
                Row("2026-03-02", "Salary", "500", category: "", type: "Income")),
            lookup: lookup);

        Assert.All(plan.Rows, r => Assert.Equal(ImportRowStatus.Duplicate, r.Status));
    }

    [Fact]
    public async Task Build_SkipDuplicatesOff_ImportsThemWithAWarning()
    {
        var lookup = new FakeImportLookup(FakeImportLookup.Snapshot(existing: [Existing("2026-03-01", "Coffee", 100m)]));

        ImportPlan plan = await BuildAsync(Rows(Row("2026-03-01", "Coffee", "100")), Options(skipDuplicates: false), lookup);

        PlannedRow row = Assert.Single(plan.Rows);
        Assert.Equal(ImportRowStatus.Valid, row.Status);
        Assert.Contains(ImportReason.DuplicateImported, row.Reasons);
    }

    [Fact]
    public async Task Build_RejectedRow_DoesNotReserveANewCategoryOrAnOccurrence()
    {
        ImportPlan plan = await BuildAsync(Rows(Row("2026-03-01", "bad", "abc", category: "Brand New"), Row("2026-03-02", "ok", "10", category: "Comida")));

        Assert.Empty(plan.NewCategories);
        Assert.Equal(ImportRowStatus.Rejected, plan.Rows[0].Status);
        Assert.Equal(ImportRowStatus.Valid, plan.Rows[1].Status);
    }

    [Fact]
    public async Task Build_NewCategories_AreDeduplicatedByFoldFirstSpellingWins()
    {
        ImportPlan plan = await BuildAsync(
            Rows(
                Row("2026-03-01", "a", "1", category: "Mascotas Y   Más"),
                Row("2026-03-02", "b", "2", category: "MASCOTAS Y MAS"),
                Row("2026-03-03", "c", "3", category: "Viajes")));

        Assert.Equal(new[] { "Mascotas Y Más", "Viajes" }, plan.NewCategories.Select(c => c.Name));
        Assert.True(plan.Rows[0].IsNewCategory);
        Assert.Equal("Mascotas Y Más", plan.Rows[1].CategoryName);
    }

    [Fact]
    public async Task Build_PlanHash_IsStableAcrossRunsAndInstances()
    {
        RawTable table = Rows(Row("2026-03-01", "a", "10"), Row("2026-03-02", "b", "abc"), Row("2026-03-03", "c", "3", category: "Nueva"));

        ImportPlan first = await BuildAsync(table);
        ImportPlan second = await BuildAsync(table);

        Assert.Matches("^[0-9a-f]{64}$", first.PlanHash);
        Assert.Equal(first.PlanHash, second.PlanHash);
        Assert.Equal(first.Rows.Select(SignatureOf), second.Rows.Select(SignatureOf));
    }

    [Fact]
    public async Task Build_PlanHash_ChangesWhenAnAcceptedRowChanges()
    {
        string baseline = (await BuildAsync(Rows(Row("2026-03-01", "a", "10")))).PlanHash;

        Assert.NotEqual(baseline, (await BuildAsync(Rows(Row("2026-03-01", "a", "10.5")))).PlanHash);
        Assert.NotEqual(baseline, (await BuildAsync(Rows(Row("2026-03-02", "a", "10")))).PlanHash);
        Assert.NotEqual(baseline, (await BuildAsync(Rows(Row("2026-03-01", "b", "10")))).PlanHash);
        Assert.NotEqual(baseline, (await BuildAsync(Rows(Row("2026-03-01", "a", "10", currency: "USD")))).PlanHash);
        Assert.NotEqual(baseline, (await BuildAsync(Rows(Row("2026-03-01", "a", "10", type: "Income")))).PlanHash);
        Assert.NotEqual(baseline, (await BuildAsync(Rows(Row("2026-03-01", "a", "10"), Row("2026-03-01", "a", "10")))).PlanHash);
    }

    [Fact]
    public async Task Build_PlanHash_ChangesWhenARowBecomesRejected()
    {
        string accepted = (await BuildAsync(Rows(Row("2026-03-01", "a", "10")))).PlanHash;

        string rejected = (await BuildAsync(Rows(Row("2026-03-01", "a", "abc")))).PlanHash;

        Assert.NotEqual(accepted, rejected);
    }

    [Fact]
    public async Task Build_PlanHash_ChangesWhenANewCategoryIsAdded()
    {
        string existing = (await BuildAsync(Rows(Row("2026-03-01", "a", "10", category: "Comida")))).PlanHash;
        string created = (await BuildAsync(Rows(Row("2026-03-01", "a", "10", category: "Zoologico")))).PlanHash;
        string renamed = (await BuildAsync(Rows(Row("2026-03-01", "a", "10", category: "Zoologicos")))).PlanHash;

        Assert.NotEqual(existing, created);
        Assert.NotEqual(created, renamed);
    }

    [Fact]
    public async Task Build_PlanHash_ChangesWhenADuplicateClassificationChanges()
    {
        RawTable table = Rows(Row("2026-03-01", "Coffee", "100"));
        string fresh = (await BuildAsync(table)).PlanHash;

        string afterStoredCopy = (await BuildAsync(table, lookup: new FakeImportLookup(FakeImportLookup.Snapshot(existing: [Existing("2026-03-01", "Coffee", 100m)])))).PlanHash;

        Assert.NotEqual(fresh, afterStoredCopy);
    }

    [Fact]
    public async Task Build_PlanHash_ChangesWhenTheDatabaseCatalogChangesWhatARowResolvesTo()
    {
        RawTable table = Rows(Row("2026-03-01", "a", "10", category: "Mascotas"));
        string creating = (await BuildAsync(table)).PlanHash;

        ImportLookupSnapshot withCategory = FakeImportLookup.Snapshot(categories: FakeImportLookup.DefaultCategories().Append(new CatalogEntry(Guid.NewGuid(), "Mascotas", false)));
        string matching = (await BuildAsync(table, lookup: new FakeImportLookup(withCategory))).PlanHash;

        Assert.NotEqual(creating, matching);
    }

    [Fact]
    public async Task Build_PlanHash_ChangesWhenAPaymentMethodResolutionChanges()
    {
        RawTable table = Table(
            [.. _headers, "Payment Method"],
            [.. Row("2026-03-01", "a", "10"), "Visa"]);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        string none = (await BuildAsync(table)).PlanHash;
        string first = (await BuildAsync(table, lookup: new FakeImportLookup(FakeImportLookup.Snapshot(paymentMethods: [new CatalogEntry(firstId, "Visa", false)])))).PlanHash;
        string second = (await BuildAsync(table, lookup: new FakeImportLookup(FakeImportLookup.Snapshot(paymentMethods: [new CatalogEntry(secondId, "Visa", false)])))).PlanHash;

        Assert.NotEqual(none, first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Build_PlanHash_ChangesWhenAnOptionChangesTheOutcome()
    {
        Func<FakeImportLookup> lookup = () => new FakeImportLookup(FakeImportLookup.Snapshot(existing: [Existing("2026-03-01", "Coffee", 100m)]));
        RawTable table = Rows(Row("2026-03-01", "Coffee", "100"));

        string skip = (await BuildAsync(table, Options(skipDuplicates: true), lookup())).PlanHash;
        string keep = (await BuildAsync(table, Options(skipDuplicates: false), lookup())).PlanHash;

        Assert.NotEqual(skip, keep);
    }

    [Fact]
    public async Task Build_PlanHash_IgnoresCultureAndClockTimeOfDay()
    {
        RawTable table = Rows(Row("2026-03-01", "a", "1.234,56"), Row("2026-03-02", "b", "10"));
        System.Globalization.CultureInfo original = System.Globalization.CultureInfo.CurrentCulture;

        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("es-DO");
            string es = (await BuildAsync(table, Options(DecimalStyle.Comma))).PlanHash;
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            string en = (await BuildAsync(table, Options(DecimalStyle.Comma))).PlanHash;

            Assert.Equal(es, en);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    private static string SignatureOf(PlannedRow row) =>
        $"{row.Parsed.RowNumber}|{row.Status}|{row.CategoryKey}|{row.CategoryName}|{string.Join(',', row.Reasons)}";
}
