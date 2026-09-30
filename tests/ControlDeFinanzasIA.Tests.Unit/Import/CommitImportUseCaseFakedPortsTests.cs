using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import;
using ControlDeFinanzasIA.Application.UseCases.Import.CommitImport;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;

namespace ControlDeFinanzasIA.Tests.Unit.Import;

/// <summary>
/// Tests of the REAL <see cref="CommitImportUseCase"/> and pipeline with fakes only for the reader, lookup,
/// writer and clock (F-020, S5): guard order, stale-plan refusal, error mapping and cancellation. What reaches
/// a real database is covered by the integration tests.
/// </summary>
public sealed class CommitImportUseCaseFakedPortsTests
{
    private static readonly string[] _headers = ["Date", "Type", "Description", "Category", "Amount", "Currency"];
    private static readonly DateOnly _today = new(2026, 9, 30);
    private static readonly DateTime _utcNow = new(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc);

    private static ImportOptions Options() => new(null, null, DecimalStyle.Auto, ImportDateFormat.DayMonthYear);

    private static string[] Row(string date, string description, string amount, string category = "Comida", string type = "Expense") =>
        [date, type, description, category, amount, "DOP"];

    private static RawTable TableWith(string[] headers, params string[][] rows) =>
        new(
            headers,
            rows.Select((cells, i) => new RawRow(i + 2, cells.Select(c => new RawCell(c, RawCellKind.Text)).ToList())).ToList(),
            new TableSourceInfo(ImportFileKind.Csv, "UTF-8", false, ',', null, string.Empty));

    private static RawTable Table(params string[][] rows) => TableWith(_headers, rows);

    private static ImportSource Source() => new("f.csv", [1]);

    private sealed class Harness
    {
        public Harness(FakeTabularReader reader, FakeImportLookup? lookup = null, FakeImportWriter? writer = null)
        {
            Writer = writer ?? new FakeImportWriter();
            Logger = new RecordingLogger<CommitImportUseCase>();
            var clock = new FakeClock { Today = _today, UtcNow = _utcNow };
            Pipeline = new ImportPipeline(reader, lookup ?? new FakeImportLookup(FakeImportLookup.Snapshot()), clock);
            UseCase = new CommitImportUseCase(Pipeline, Writer, clock, Logger);
        }

        public ImportPipeline Pipeline { get; }

        public FakeImportWriter Writer { get; }

        public RecordingLogger<CommitImportUseCase> Logger { get; }

        public CommitImportUseCase UseCase { get; }

        public async Task<string> HashAsync(ImportOptions options)
        {
            Result plan = await Pipeline.BuildAsync(Source(), options, CancellationToken.None);
            Assert.True(plan.IsSuccess, plan.Error?.Code);
            return ((ImportPlan)plan.Value!).PlanHash;
        }
    }

    [Fact]
    public async Task Execute_MatchingHash_WritesTheMaterializedBatchWithANonCancellableToken()
    {
        var harness = new Harness(new FakeTabularReader(Table(Row("2026-03-01", "Uno", "10"), Row("2026-03-02", "Salario", "20", string.Empty, "Income"))));
        ImportOptions options = Options();
        using var cts = new CancellationTokenSource();

        Result result = await harness.UseCase.ExecuteAsync(Source(), options, await harness.HashAsync(options), cts.Token);

        Assert.True(result.IsSuccess, result.Error?.Code);
        ImportBatch batch = Assert.Single(harness.Writer.Batches);
        Assert.Single(batch.Expenses);
        Assert.Single(batch.Incomes);
        Assert.False(harness.Writer.Tokens[0].CanBeCanceled);
        Assert.Equal(new ImportCommitResult(1, 1, 0, 0, 0), result.Value);
    }

    [Fact]
    public async Task Execute_StampsEntitiesWithTheClockInstant()
    {
        var harness = new Harness(new FakeTabularReader(Table(Row("2026-03-01", "Uno", "10", "Mascotas"))));
        ImportOptions options = Options();

        await harness.UseCase.ExecuteAsync(Source(), options, await harness.HashAsync(options));

        ImportBatch batch = Assert.Single(harness.Writer.Batches);
        Assert.Equal(_utcNow, batch.Expenses[0].CreatedAt);
        Assert.Equal(_utcNow, batch.NewCategories[0].CreatedAt);
    }

    [Fact]
    public async Task Execute_HashMismatch_ReturnsPreviewStaleAndNeverWrites()
    {
        var harness = new Harness(new FakeTabularReader(Table(Row("2026-03-01", "Uno", "10"))));

        Result result = await harness.UseCase.ExecuteAsync(Source(), Options(), new string('0', 64));

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.PreviewStale, result.Error!.Code);
        Assert.Empty(harness.Writer.Batches);
    }

    [Fact]
    public async Task Execute_OptionsDifferentFromThePreviewed_ReturnsPreviewStale()
    {
        var harness = new Harness(new FakeTabularReader(Table(Row("2026-03-01", "Uno", "10", "Mascotas"))));
        ImportOptions previewed = Options();
        string hash = await harness.HashAsync(previewed);

        Result result = await harness.UseCase.ExecuteAsync(Source(), previewed with { CreateMissingCategories = false }, hash);

        Assert.Equal(ImportErrorCodes.PreviewStale, result.Error!.Code);
        Assert.Empty(harness.Writer.Batches);
    }

    [Fact]
    public async Task Execute_ReparsesTheRetainedBytesAtCommit()
    {
        var reader = new FakeTabularReader(Table(Row("2026-03-01", "Uno", "10")));
        var harness = new Harness(reader);
        ImportOptions options = Options();
        string hash = await harness.HashAsync(options);
        int readsBefore = reader.Calls;

        await harness.UseCase.ExecuteAsync(Source(), options, hash);

        Assert.Equal(readsBefore + 1, reader.Calls);
    }

    [Fact]
    public async Task Execute_MappingIncomplete_ReturnsMappingIncomplete()
    {
        string[] headers = ["When", "What", "How much"];
        var harness = new Harness(new FakeTabularReader(TableWith(headers, ["2026-03-01", "Uno", "10"])));
        ImportOptions options = Options();

        Result result = await harness.UseCase.ExecuteAsync(Source(), options, await harness.HashAsync(options));

        Assert.Equal(ImportErrorCodes.MappingIncomplete, result.Error!.Code);
        Assert.Empty(harness.Writer.Batches);
    }

    [Fact]
    public async Task Execute_ReaderFailure_IsReturnedAsIs()
    {
        var reader = new FakeTabularReader(Result.Failure(ImportErrorCodes.UnreadableFile, "bad"));
        var harness = new Harness(reader);

        Result result = await harness.UseCase.ExecuteAsync(Source(), Options(), "hash");

        Assert.Equal(ImportErrorCodes.UnreadableFile, result.Error!.Code);
        Assert.Empty(harness.Writer.Batches);
    }

    [Fact]
    public async Task Execute_LookupFailure_IsReturnedAsLookupFailed()
    {
        var lookup = new FakeImportLookup(() => Result.Failure(ImportErrorCodes.LookupFailed, "db"));
        var harness = new Harness(new FakeTabularReader(Table(Row("2026-03-01", "Uno", "10"))), lookup);

        Result result = await harness.UseCase.ExecuteAsync(Source(), Options(), "hash");

        Assert.Equal(ImportErrorCodes.LookupFailed, result.Error!.Code);
        Assert.Empty(harness.Writer.Batches);
    }

    [Fact]
    public async Task Execute_UnexpectedExceptionWhileBuildingThePlan_IsLoggedAndMappedToLookupFailed()
    {
        var harness = new Harness(new FakeTabularReader(() => throw new InvalidOperationException("boom")));

        Result result = await harness.UseCase.ExecuteAsync(Source(), Options(), "hash");

        Assert.Equal(ImportErrorCodes.LookupFailed, result.Error!.Code);
        Assert.True(harness.Logger.HasError);
        Assert.Empty(harness.Writer.Batches);
    }

    [Fact]
    public async Task Execute_DomainRefusesARow_ReturnsWriteFailedAndNeverCallsTheWriter()
    {
        // A payment method with an empty id passes the validator and the planner but the domain refuses it.
        var emptyId = new CatalogEntry(Guid.Empty, "Efectivo", false);
        var lookup = new FakeImportLookup(FakeImportLookup.Snapshot(paymentMethods: [emptyId]));
        string[] headers = [.. _headers, "Payment method"];
        RawTable table = TableWith(headers, [.. Row("2026-03-01", "Uno", "10"), "Efectivo"]);
        var harness = new Harness(new FakeTabularReader(table), lookup);
        ImportOptions options = Options();

        Result result = await harness.UseCase.ExecuteAsync(Source(), options, await harness.HashAsync(options));

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.WriteFailed, result.Error!.Code);
        Assert.Empty(harness.Writer.Batches);
        Assert.True(harness.Logger.HasError);
    }

    [Fact]
    public async Task Execute_NoValidRows_SucceedsWithCountsAndNeverCallsTheWriter()
    {
        var existing = new ExistingTransactionKey(new DateOnly(2026, 3, 1), TransactionType.Expense, 10m, Currency.DOP, "Taxi", new Guid("11111111-1111-1111-1111-000000000001"));
        var lookup = new FakeImportLookup(FakeImportLookup.Snapshot(existing: [existing]));
        var harness = new Harness(new FakeTabularReader(Table(Row("2026-03-01", "Taxi", "10"), Row("2026-03-02", "Bad", "abc"))), lookup);
        ImportOptions options = Options();

        Result result = await harness.UseCase.ExecuteAsync(Source(), options, await harness.HashAsync(options));

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal(new ImportCommitResult(0, 0, 0, 1, 1), result.Value);
        Assert.Empty(harness.Writer.Batches);
    }

    [Fact]
    public async Task Execute_CountsDuplicatesAndRejectedAlongsideWhatWasInserted()
    {
        var existing = new ExistingTransactionKey(new DateOnly(2026, 3, 1), TransactionType.Expense, 10m, Currency.DOP, "Taxi", new Guid("11111111-1111-1111-1111-000000000001"));
        var lookup = new FakeImportLookup(FakeImportLookup.Snapshot(existing: [existing]));
        var harness = new Harness(
            new FakeTabularReader(Table(Row("2026-03-01", "Taxi", "10"), Row("2026-03-02", "Bad", "abc"), Row("2026-03-03", "Cine", "5", "Mascotas"))),
            lookup);
        ImportOptions options = Options();

        Result result = await harness.UseCase.ExecuteAsync(Source(), options, await harness.HashAsync(options));

        Assert.Equal(new ImportCommitResult(1, 0, 1, 1, 1), result.Value);
    }

    [Fact]
    public async Task Execute_WriterFailure_IsReturnedAsIs()
    {
        var writer = new FakeImportWriter(_ => Result.Failure(ImportErrorCodes.WriteFailed, "disk"));
        var harness = new Harness(new FakeTabularReader(Table(Row("2026-03-01", "Uno", "10"))), writer: writer);
        ImportOptions options = Options();

        Result result = await harness.UseCase.ExecuteAsync(Source(), options, await harness.HashAsync(options));

        Assert.Equal(ImportErrorCodes.WriteFailed, result.Error!.Code);
    }

    [Fact]
    public async Task Execute_WriterThrows_IsLoggedAndMappedToWriteFailed()
    {
        var writer = new FakeImportWriter(_ => throw new IOException("disk"));
        var harness = new Harness(new FakeTabularReader(Table(Row("2026-03-01", "Uno", "10"))), writer: writer);
        ImportOptions options = Options();

        Result result = await harness.UseCase.ExecuteAsync(Source(), options, await harness.HashAsync(options));

        Assert.Equal(ImportErrorCodes.WriteFailed, result.Error!.Code);
        Assert.True(harness.Logger.HasError);
    }

    [Fact]
    public async Task Execute_CancelledBeforeTheWrite_ThrowsAndWritesNothing()
    {
        var harness = new Harness(new FakeTabularReader(Table(Row("2026-03-01", "Uno", "10"))));
        ImportOptions options = Options();
        string hash = await harness.HashAsync(options);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.UseCase.ExecuteAsync(Source(), options, hash, cts.Token));

        Assert.Empty(harness.Writer.Batches);
    }

    [Fact]
    public async Task Execute_NullArguments_Throw()
    {
        var harness = new Harness(new FakeTabularReader(Table(Row("2026-03-01", "Uno", "10"))));

        await Assert.ThrowsAsync<ArgumentNullException>(() => harness.UseCase.ExecuteAsync(null!, Options(), "h"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => harness.UseCase.ExecuteAsync(Source(), null!, "h"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => harness.UseCase.ExecuteAsync(Source(), Options(), null!));
    }
}
