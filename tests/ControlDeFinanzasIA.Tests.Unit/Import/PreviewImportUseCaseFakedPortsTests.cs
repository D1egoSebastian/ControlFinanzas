using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.PreviewImport;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Unit.Import;

/// <summary>
/// Tests of the REAL <see cref="PreviewImportUseCase"/> with fakes only for the reader, lookup, clock and
/// logger (F-020, S4b): summary arithmetic, the two 50-row lists, error mapping and cancellation.
/// </summary>
public sealed class PreviewImportUseCaseFakedPortsTests
{
    private static readonly string[] _headers = ["Date", "Type", "Description", "Category", "Amount", "Currency"];
    private static readonly DateOnly _today = new(2026, 9, 30);

    private static ImportOptions Options() => new(null, null, DecimalStyle.Auto, ImportDateFormat.DayMonthYear);

    private static string[] Row(string date, string description, string amount, string category = "Comida") =>
        [date, "Expense", description, category, amount, "DOP"];

    private static RawTable Table(params string[][] rows) =>
        new(
            _headers,
            rows.Select((cells, i) => new RawRow(i + 2, cells.Select(c => new RawCell(c, RawCellKind.Text)).ToList())).ToList(),
            new TableSourceInfo(ImportFileKind.Csv, "UTF-8", false, ',', null, string.Empty));

    private static (PreviewImportUseCase UseCase, RecordingLogger<PreviewImportUseCase> Logger) Create(
        FakeTabularReader reader,
        FakeImportLookup? lookup = null)
    {
        var logger = new RecordingLogger<PreviewImportUseCase>();
        var pipeline = new ImportPipeline(reader, lookup ?? new FakeImportLookup(FakeImportLookup.Snapshot()), new FakeClock { Today = _today, UtcNow = DateTime.UtcNow });
        return (new PreviewImportUseCase(pipeline, logger), logger);
    }

    private static async Task<ImportPreview> PreviewAsync(RawTable table, FakeImportLookup? lookup = null)
    {
        Result result = await Create(new FakeTabularReader(table), lookup).UseCase.ExecuteAsync(new ImportSource("f.csv", [1]), Options());
        Assert.True(result.IsSuccess, result.Error?.Code);
        return (ImportPreview)result.Value!;
    }

    [Fact]
    public async Task Execute_CountsEveryOutcome()
    {
        var lookup = new FakeImportLookup(FakeImportLookup.Snapshot(existing:
        [
            new ExistingTransactionKey(new DateOnly(2026, 3, 1), TransactionType.Expense, 100m, Currency.DOP, "Coffee", new Guid("11111111-1111-1111-1111-000000000001")),
        ]));

        ImportPreview preview = await PreviewAsync(
            Table(
                Row("2026-03-01", "Coffee", "100"),
                Row("2026-03-02", "Lunch", "250.50"),
                Row("2026-03-03", "Bad", "abc"),
                Row("2026-03-04", "NoCat", "75", category: string.Empty),
                Row("2026-03-05", "Pet", "10", category: "Mascotas")),
            lookup);

        Assert.Equal(5, preview.TotalRows);
        Assert.Equal(3, preview.ToImportCount);
        Assert.Equal(1, preview.DuplicateCount);
        Assert.Equal(1, preview.RejectedCount);
        Assert.Equal(1, preview.WarningCount);
        Assert.Equal(new[] { "Mascotas" }, preview.CategoriesToCreate);
        Assert.Equal(64, preview.PlanHash.Length);
    }

    [Fact]
    public async Task Execute_ReasonCounts_CountEveryReasonOfEveryRow()
    {
        ImportPreview preview = await PreviewAsync(
            Table(
                Row("2026-03-03", "Bad 1", "abc"),
                Row("2026-03-03", "Bad 2", "xyz"),
                Row("2026-03-04", "NoCat 1", "75", category: string.Empty),
                Row("2026-03-04", "NoCat 2", "76", category: string.Empty),
                Row("2026-03-04", "NoCat 3", "77", category: string.Empty)));

        Assert.Equal(2, preview.ReasonCounts.Single(c => c.Reason == ImportReason.InvalidAmount).Count);
        Assert.Equal(3, preview.ReasonCounts.Single(c => c.Reason == ImportReason.CategoryFallback).Count);
        Assert.Equal(
            preview.SampleRows.Sum(r => r.Reasons.Count),
            preview.ReasonCounts.Sum(c => c.Count));
        Assert.DoesNotContain(preview.ReasonCounts, c => c.Count == 0);
    }

    [Fact]
    public async Task Execute_SampleHoldsTheFirstFiftyRows_ProblemRowsTheFirstFiftyProblems()
    {
        var rows = new List<string[]>();
        for (int i = 0; i < 300; i++)
        {
            // Every row from the 100th on is rejected, so the first 50 rows are all valid.
            rows.Add(Row("2026-03-01", $"Item {i}", i >= 100 ? "abc" : "10"));
        }

        ImportPreview preview = await PreviewAsync(Table(rows.ToArray()));

        Assert.Equal(300, preview.TotalRows);
        Assert.Equal(ImportLimits.PreviewRows, preview.SampleRows.Count);
        Assert.Equal(Enumerable.Range(2, 50), preview.SampleRows.Select(r => r.RowNumber));
        Assert.All(preview.SampleRows, r => Assert.NotEqual(ImportRowStatus.Rejected, r.Status));
        Assert.Equal(ImportLimits.PreviewRows, preview.ProblemRows.Count);
        Assert.All(preview.ProblemRows, r => Assert.True(r.Status == ImportRowStatus.Rejected || r.Reasons.Count > 0));
        Assert.Equal(200, preview.RejectedCount);
        Assert.Equal(200, preview.ReasonCounts.Single(c => c.Reason == ImportReason.InvalidAmount).Count);
    }

    [Fact]
    public async Task Execute_ProblemRows_IncludeDuplicatesAndWarningsInFileOrder()
    {
        var lookup = new FakeImportLookup(FakeImportLookup.Snapshot(existing:
        [
            new ExistingTransactionKey(new DateOnly(2026, 3, 2), TransactionType.Expense, 5m, Currency.DOP, "dup", new Guid("11111111-1111-1111-1111-000000000001")),
        ]));

        ImportPreview preview = await PreviewAsync(
            Table(
                Row("2026-03-01", "clean", "1"),
                Row("2026-03-02", "dup", "5"),
                Row("2026-03-03", "warn", "2", category: string.Empty),
                Row("2026-03-04", "bad", "abc")),
            lookup);

        Assert.Equal(new[] { 3, 4, 5 }, preview.ProblemRows.Select(r => r.RowNumber));
        Assert.Equal(
            new[] { ImportRowStatus.Duplicate, ImportRowStatus.Valid, ImportRowStatus.Rejected },
            preview.ProblemRows.Select(r => r.Status));
    }

    [Fact]
    public async Task Execute_RowResults_CarryTheParsedValuesAndResolvedCategory()
    {
        ImportPreview preview = await PreviewAsync(
            Table(
                Row("2026-03-02", "  Lunch  ", "250.5000", category: "comida"),
                Row("2026-03-03", "Pet", "10", category: "Mascotas")));

        ImportRowResult lunch = preview.SampleRows[0];
        Assert.Equal(new DateOnly(2026, 3, 2), lunch.Date);
        Assert.Equal(TransactionType.Expense, lunch.Type);
        Assert.Equal(250.5m, lunch.Amount);
        Assert.Equal(Currency.DOP, lunch.Currency);
        Assert.Equal("Lunch", lunch.Description);
        Assert.Equal("Comida", lunch.CategoryName);
        Assert.False(lunch.IsNewCategory);

        ImportRowResult pet = preview.SampleRows[1];
        Assert.Equal("Mascotas", pet.CategoryName);
        Assert.True(pet.IsNewCategory);
    }

    [Fact]
    public async Task Execute_IncompleteMapping_IsASuccessWithMissingFieldsAndNoEvaluatedRows()
    {
        var table = new RawTable(
            ["When", "What", "How much"],
            [new RawRow(2, [new RawCell("2026-01-01", RawCellKind.Text), new RawCell("x", RawCellKind.Text), new RawCell("5", RawCellKind.Text)])],
            new TableSourceInfo(ImportFileKind.Csv, "UTF-8", false, ',', null, string.Empty));

        ImportPreview preview = await PreviewAsync(table);

        Assert.False(preview.Mapping.IsComplete);
        Assert.Equal(new[] { ImportField.Date, ImportField.Description, ImportField.Amount }, preview.MissingFields);
        Assert.Empty(preview.SampleRows);
        Assert.Empty(preview.ProblemRows);
        Assert.Equal(0, preview.ToImportCount);
        Assert.Equal(0, preview.RejectedCount);
        Assert.Equal(new[] { "When", "What", "How much" }, preview.Headers);
    }

    [Fact]
    public async Task Execute_PlanHash_EqualsThePipelinePlanHash()
    {
        RawTable table = Table(Row("2026-03-01", "a", "10"), Row("2026-03-02", "b", "abc"));
        var reader = new FakeTabularReader(table);
        var lookup = new FakeImportLookup(FakeImportLookup.Snapshot());
        var pipeline = new ImportPipeline(reader, lookup, new FakeClock { Today = _today, UtcNow = DateTime.UtcNow });
        var useCase = new PreviewImportUseCase(pipeline, new RecordingLogger<PreviewImportUseCase>());
        var source = new ImportSource("f.csv", [1]);

        Result preview = await useCase.ExecuteAsync(source, Options());
        Result plan = await pipeline.BuildAsync(source, Options(), CancellationToken.None);

        Assert.Equal(((ImportPlan)plan.Value!).PlanHash, ((ImportPreview)preview.Value!).PlanHash);
    }

    [Theory]
    [InlineData(ImportErrorCodes.UnreadableFile)]
    [InlineData(ImportErrorCodes.TooLarge)]
    [InlineData(ImportErrorCodes.NoRows)]
    public async Task Execute_FileLevelFailure_IsAFailureWithTheSameCode(string code)
    {
        (PreviewImportUseCase useCase, _) = Create(new FakeTabularReader(Result.Failure(code, "nope")));

        Result result = await useCase.ExecuteAsync(new ImportSource("f.csv", [1]), Options());

        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Error!.Code);
    }

    [Fact]
    public async Task Execute_MappingIncompleteFromAnInvalidUserMapping_IsAFailure()
    {
        (PreviewImportUseCase useCase, _) = Create(new FakeTabularReader(Table(Row("2026-03-01", "a", "10"))));

        Result result = await useCase.ExecuteAsync(
            new ImportSource("f.csv", [1]),
            Options() with { Mapping = new ColumnMapping(0, null, 0, null, 4, null, null) });

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.MappingIncomplete, result.Error!.Code);
    }

    [Fact]
    public async Task Execute_ReaderThrows_BecomesLookupFailedAndIsLogged()
    {
        (PreviewImportUseCase useCase, RecordingLogger<PreviewImportUseCase> logger) =
            Create(new FakeTabularReader((Func<Result>)(() => throw new InvalidOperationException("boom"))));

        Result result = await useCase.ExecuteAsync(new ImportSource("f.csv", [1]), Options());

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.LookupFailed, result.Error!.Code);
        Assert.Contains(LogLevel.Error, logger.Levels);
    }

    [Fact]
    public async Task Execute_LookupThrows_BecomesLookupFailed()
    {
        (PreviewImportUseCase useCase, _) = Create(
            new FakeTabularReader(Table(Row("2026-03-01", "a", "10"))),
            new FakeImportLookup((Func<Result>)(() => throw new InvalidOperationException("db down"))));

        Result result = await useCase.ExecuteAsync(new ImportSource("f.csv", [1]), Options());

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.LookupFailed, result.Error!.Code);
    }

    [Fact]
    public async Task Execute_LookupFailureResult_IsReturned()
    {
        (PreviewImportUseCase useCase, _) = Create(
            new FakeTabularReader(Table(Row("2026-03-01", "a", "10"))),
            new FakeImportLookup(() => Result.Failure(ImportErrorCodes.LookupFailed, "db")));

        Result result = await useCase.ExecuteAsync(new ImportSource("f.csv", [1]), Options());

        Assert.Equal(ImportErrorCodes.LookupFailed, result.Error!.Code);
    }

    [Fact]
    public async Task Execute_Cancellation_IsRethrownNotSwallowed()
    {
        (PreviewImportUseCase useCase, RecordingLogger<PreviewImportUseCase> logger) = Create(new FakeTabularReader(Table(Row("2026-03-01", "a", "10"))));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useCase.ExecuteAsync(new ImportSource("f.csv", [1]), Options(), cts.Token));
        Assert.DoesNotContain(LogLevel.Error, logger.Levels);
    }

    [Fact]
    public async Task Execute_LookupCancelling_IsRethrown()
    {
        (PreviewImportUseCase useCase, _) = Create(
            new FakeTabularReader(Table(Row("2026-03-01", "a", "10"))),
            new FakeImportLookup((Func<Result>)(() => throw new OperationCanceledException())));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useCase.ExecuteAsync(new ImportSource("f.csv", [1]), Options()));
    }
}
