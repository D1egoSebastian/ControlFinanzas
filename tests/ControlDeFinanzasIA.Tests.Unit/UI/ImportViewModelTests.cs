using System.Globalization;
using System.Reflection;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import;
using ControlDeFinanzasIA.Application.UseCases.Import.CommitImport;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using ControlDeFinanzasIA.Application.UseCases.Import.PreviewImport;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Tests of the REAL <see cref="ImportViewModel"/> (F-020, S6a) over the REAL preview and commit use cases:
/// fakes exist only for the ports around them (reader, lookup, writer, picker, clock, notification centre).
/// </summary>
public sealed class ImportViewModelTests : IDisposable
{
    private static readonly string[] _headers = ["Date", "Type", "Description", "Category", "Amount", "Currency"];
    private static readonly DateOnly _today = new(2026, 9, 30);
    private static readonly DateTime _utcNow = new(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc);

    private readonly CultureInfo _previousUi = CultureInfo.CurrentUICulture;
    private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;

    public ImportViewModelTests()
    {
        UseCulture("es-DO");
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _previousUi;
        CultureInfo.CurrentCulture = _previousCulture;
    }

    private static void UseCulture(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
    }

    private static string[] Row(string date, string description, string amount, string category = "Comida", string type = "Expense") =>
        [date, type, description, category, amount, "DOP"];

    private static RawTable TableWith(string[] headers, TableSourceInfo? source, params string[][] rows) =>
        new(
            headers,
            rows.Select((cells, i) => new RawRow(i + 2, cells.Select(c => new RawCell(c, RawCellKind.Text)).ToList())).ToList(),
            source ?? new TableSourceInfo(ImportFileKind.Csv, "UTF-8", false, ',', null, string.Empty));

    private static RawTable Table(params string[][] rows) => TableWith(_headers, null, rows);

    private static RawTable GoodTable() => Table(Row("2026-03-01", "Uno", "10"), Row("2026-03-02", "Dos", "20"));

    private static IReadOnlyDictionary<string, string[]> Details(string reason, string? line = null)
    {
        var details = new Dictionary<string, string[]> { [ImportErrorCodes.DetailReason] = [reason] };
        if (line is not null)
        {
            details[ImportErrorCodes.DetailLine] = [line];
        }

        return details;
    }

    // Every await on work a test can gate goes through here, so a regression fails fast instead of hanging.
    private static Task Bounded(Task task) => task.WaitAsync(TimeSpan.FromSeconds(5));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5);
        }

        Assert.True(condition(), "Condition was not reached in time.");
    }

    private static string Text(string key, CultureInfo culture, params object[] args) =>
        string.Format(culture, Strings.ResourceManager.GetString(key, culture)!, args);

    private sealed class GatedWriter : IImportWriter
    {
        public TaskCompletionSource<Result> Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public Task<Result> WriteAsync(ImportBatch batch, CancellationToken ct = default)
        {
            Calls++;
            return Gate.Task;
        }
    }

    private sealed class GatedReader : ITabularReader
    {
        private readonly RawTable _table;

        public GatedReader(RawTable table)
        {
            _table = table;
        }

        public TaskCompletionSource? Gate { get; set; }

        public int Calls { get; private set; }

        public int Cancelled { get; private set; }

        public async Task<Result> ReadAsync(ReadOnlyMemory<byte> content, TabularReadOptions options, CancellationToken ct = default)
        {
            Calls++;
            if (Gate is not null)
            {
                try
                {
                    await Gate.Task.WaitAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    Cancelled++;
                    throw;
                }
            }

            return Result.Success(_table);
        }
    }

    private sealed class ThrowingDialog : IFileOpenDialog
    {
        private readonly Exception _exception;

        public ThrowingDialog(Exception exception)
        {
            _exception = exception;
        }

        public Task<OpenTarget?> PickAsync(FileTypeFilter filter, string? title = null, CancellationToken ct = default) =>
            throw _exception;
    }

    private sealed class Fixture
    {
        public Fixture(
            RawTable? table = null,
            Result? readerResult = null,
            Func<Result>? lookup = null,
            IImportWriter? writer = null,
            ITabularReader? reader = null,
            IFileOpenDialog? dialog = null,
            bool withCenter = true)
        {
            Reader = readerResult is not null ? new FakeTabularReader(readerResult) : new FakeTabularReader(table ?? GoodTable());
            Lookup = new FakeImportLookup(lookup ?? (() => Result.Success(FakeImportLookup.Snapshot())));
            Writer = new FakeImportWriter();
            Dialog = new FakeFileOpenDialog { Result = NewTarget() };
            Center = new FakeNotificationCenter();
            Logger = new RecordingLogger<ImportViewModel>();
            var clock = new FakeClock { Today = _today, UtcNow = _utcNow };
            var pipeline = new ImportPipeline(reader ?? Reader, Lookup, clock);
            var preview = new PreviewImportUseCase(pipeline, new RecordingLogger<PreviewImportUseCase>());
            var commit = new CommitImportUseCase(pipeline, writer ?? Writer, clock, new RecordingLogger<CommitImportUseCase>());
            ViewModel = new ImportViewModel(preview, commit, dialog ?? Dialog, Logger, withCenter ? Center : null)
            {
                PreviewDebounce = TimeSpan.Zero,
            };
            ViewModel.Completed += (_, _) => Events.Add("Completed");
            ViewModel.CloseRequested += (_, _) => Events.Add("Close");
        }

        public FakeTabularReader Reader { get; }

        public FakeImportLookup Lookup { get; }

        public FakeImportWriter Writer { get; }

        public FakeFileOpenDialog Dialog { get; }

        public FakeNotificationCenter Center { get; }

        public RecordingLogger<ImportViewModel> Logger { get; }

        public ImportViewModel ViewModel { get; }

        public List<string> Events { get; } = [];

        public static OpenTarget NewTarget() => OpenTargets.Recording([1, 2, 3]).Target;

        public async Task ChooseAsync()
        {
            Dialog.Result = NewTarget();
            await Bounded(ViewModel.ChooseFileCommand.ExecuteAsync(null));
        }
    }

    // ---- (a) cancelled picker (S-24) ----

    [Fact]
    public async Task ChooseFile_Cancelled_RaisesCloseWithoutMessageOrUseCaseCall()
    {
        var f = new Fixture();
        f.Dialog.Result = null;

        await Bounded(f.ViewModel.ChooseFileCommand.ExecuteAsync(null));

        Assert.Equal(["Close"], f.Events);
        Assert.Equal(string.Empty, f.ViewModel.StatusMessage);
        Assert.False(f.ViewModel.IsError);
        Assert.Equal(0, f.Reader.Calls);
        Assert.Equal(0, f.Lookup.Calls);
        Assert.Equal(ImportPanelState.Idle, f.ViewModel.State);
    }

    [Fact]
    public async Task ChooseFile_OffersCsvTextAndExcelFilters_WithTheLocalizedTitle()
    {
        var f = new Fixture();

        await Bounded(f.ChooseAsync());

        Assert.Equal(["csv", "txt", "xlsx"], f.Dialog.LastFilters.Select(x => x.Extension));
        Assert.Equal(
            [Strings.ImportFileTypeCsv, Strings.ImportFileTypeText, Strings.ImportFileTypeExcel],
            f.Dialog.LastFilters.Select(x => x.DisplayName));
        Assert.Equal(Strings.ImportDialogTitle, f.Dialog.LastTitle);
    }

    // ---- (b) file-level failures (S-28, R-24) ----

    [Theory]
    [InlineData(ImportErrorCodes.UnreadableFile, ImportErrorCodes.ReasonMalformedCsv, "7", "ImportErrorMalformedCsv")]
    [InlineData(ImportErrorCodes.UnreadableFile, ImportErrorCodes.ReasonLegacyXls, null, "ImportErrorLegacyXls")]
    [InlineData(ImportErrorCodes.UnreadableFile, ImportErrorCodes.ReasonBinary, null, "ImportErrorBinary")]
    [InlineData(ImportErrorCodes.UnreadableFile, ImportErrorCodes.ReasonNotAWorkbook, null, "ImportErrorUnreadable")]
    [InlineData(ImportErrorCodes.UnreadableFile, "somethingElse", null, "ImportErrorUnreadable")]
    [InlineData(ImportErrorCodes.TooLarge, ImportErrorCodes.ReasonFileTooLarge, null, "ImportErrorTooLarge")]
    [InlineData(ImportErrorCodes.TooLarge, ImportErrorCodes.ReasonTooManyRows, null, "ImportErrorTooLarge")]
    [InlineData(ImportErrorCodes.NoRows, null, null, "ImportErrorNoRows")]
    public async Task ChooseFile_FileLevelFailure_ShowsTheExactLocalizedTextInBothCultures(
        string code,
        string? reason,
        string? line,
        string expectedKey)
    {
        foreach (string cultureName in new[] { "es-DO", "en" })
        {
            UseCulture(cultureName);
            var culture = CultureInfo.GetCultureInfo(cultureName);
            Result failure = reason is null
                ? Result.Failure(code, @"C:\secret\folder\bank.csv exploded")
                : Result.Failure(code, @"C:\secret\folder\bank.csv exploded", Details(reason, line));
            var f = new Fixture(readerResult: failure);

            await Bounded(f.ChooseAsync());

            object[] args = expectedKey switch
            {
                "ImportErrorMalformedCsv" => [line!],
                "ImportErrorTooLarge" =>
                [
                    (ImportLimits.MaxFileBytes / (1024 * 1024)).ToString("N0", culture),
                    ImportLimits.MaxDataRows.ToString("N0", culture),
                    ImportLimits.MaxColumns.ToString("N0", culture),
                    ImportLimits.MaxFieldChars.ToString("N0", culture),
                ],
                _ => [],
            };
            Assert.Equal(Text(expectedKey, culture, args), f.ViewModel.StatusMessage);
            Assert.True(f.ViewModel.IsError);
            Assert.DoesNotContain("secret", f.ViewModel.StatusMessage);
            Assert.NotEqual(ImportPanelState.Previewed, f.ViewModel.State);
            Assert.Equal(ImportPanelState.Idle, f.ViewModel.State);
            Assert.False(f.ViewModel.HasSource);
        }
    }

    [Fact]
    public async Task ChooseFile_UnreadableStream_ShowsTheUnreadableMessageWithoutTheException()
    {
        var f = new Fixture();
        f.Dialog.Result = new OpenTarget("bank.csv", _ => throw new IOException(@"C:\secret\bank.csv locked"));

        await Bounded(f.ViewModel.ChooseFileCommand.ExecuteAsync(null));

        Assert.Equal(Strings.ImportErrorUnreadable, f.ViewModel.StatusMessage);
        Assert.DoesNotContain("secret", f.ViewModel.StatusMessage);
        Assert.Equal(0, f.Reader.Calls);
        Assert.Equal(ImportPanelState.Idle, f.ViewModel.State);
    }

    // ---- (c) auto mapping vs mapping step (S-9) ----

    [Fact]
    public async Task ChooseFile_CompleteAutoMapping_GoesStraightToPreviewed()
    {
        var f = new Fixture();

        await Bounded(f.ChooseAsync());

        Assert.Equal(ImportPanelState.Previewed, f.ViewModel.State);
        Assert.NotNull(f.ViewModel.Preview);
        Assert.True(f.ViewModel.ConfirmCommand.CanExecute(null));
        Assert.Equal(2, f.ViewModel.Preview!.ToImportCount);
        Assert.Equal(0, f.ViewModel.SelectedDateColumn?.Index);
        Assert.Equal(2, f.ViewModel.SelectedDescriptionColumn?.Index);
        Assert.Equal(4, f.ViewModel.SelectedAmountColumn?.Index);
        Assert.False(f.ViewModel.IsBusy);
    }

    [Fact]
    public async Task ChooseFile_IncompleteMapping_EntersMapping_AndConfirmStaysDisabledUntilRequiredFieldsAreMapped()
    {
        RawTable table = TableWith(["When", "What", "How much"], null, ["2026-03-01", "Uno", "10"], ["2026-03-02", "Dos", "20"]);
        var f = new Fixture(table);

        await Bounded(f.ChooseAsync());

        Assert.Equal(ImportPanelState.Mapping, f.ViewModel.State);
        Assert.False(f.ViewModel.ConfirmCommand.CanExecute(null));
        Assert.Null(f.ViewModel.SelectedDateColumn?.Index);
        Assert.Equal(4, f.ViewModel.ColumnChoices.Count);
        Assert.Null(f.ViewModel.ColumnChoices[0].Index);
        Assert.Equal(Strings.ImportColumnNone, f.ViewModel.ColumnChoices[0].Label);

        // One change at a time, like a user: with a zero debounce each refresh continues on another thread
        // (Task.Yield), so changing the next property while it applies its preview would race the VM's
        // UI-thread-confined state (the change made while suppressed is silently dropped). Settle each refresh first.
        f.ViewModel.SelectedDateColumn = f.ViewModel.ColumnChoices[1];
        await Bounded(f.ViewModel.RefreshTask);
        f.ViewModel.SelectedDescriptionColumn = f.ViewModel.ColumnChoices[2];
        await Bounded(f.ViewModel.RefreshTask);
        Assert.False(f.ViewModel.ConfirmCommand.CanExecute(null));
        f.ViewModel.SelectedAmountColumn = f.ViewModel.ColumnChoices[3];
        await Bounded(f.ViewModel.RefreshTask);

        Assert.Equal(ImportPanelState.Previewed, f.ViewModel.State);
        Assert.True(f.ViewModel.ConfirmCommand.CanExecute(null));
        Assert.Equal(2, f.ViewModel.Preview!.ToImportCount);
        Assert.Equal(1, f.Dialog.Calls);
    }

    // ---- (d) option changes re-preview from the retained bytes (R-5, S-5) ----

    [Fact]
    public async Task OptionChange_MarksPreviewStale_ThenReprevewsOnceFromTheRetainedBytes()
    {
        var reader = new GatedReader(GoodTable());
        var f = new Fixture(reader: reader);
        await Bounded(f.ChooseAsync());
        Assert.Equal(1, reader.Calls);
        reader.Gate = new TaskCompletionSource();

        f.ViewModel.DecimalStyle = DecimalStyle.Comma;
        await WaitUntilAsync(() => reader.Calls == 2);

        // The re-preview is held at the reader, so the stale state is observed deterministically.
        Assert.True(f.ViewModel.IsPreviewStale);
        Assert.False(f.ViewModel.ConfirmCommand.CanExecute(null));

        reader.Gate.SetResult();
        await Bounded(f.ViewModel.RefreshTask);

        Assert.False(f.ViewModel.IsPreviewStale);
        Assert.Equal(ImportPanelState.Previewed, f.ViewModel.State);
        Assert.Equal(2, reader.Calls);
        Assert.Equal(1, f.Dialog.Calls);
    }

    [Fact]
    public async Task RapidOptionChanges_CollapseIntoOneDebouncedPreview()
    {
        var f = new Fixture();
        await Bounded(f.ChooseAsync());
        f.ViewModel.PreviewDebounce = TimeSpan.FromSeconds(1);

        f.ViewModel.DecimalStyle = DecimalStyle.Point;
        f.ViewModel.SkipDuplicates = false;
        f.ViewModel.DefaultCurrency = Currency.USD;
        f.ViewModel.SignMode = SignMode.NegativeIsExpense;
        await Bounded(f.ViewModel.RefreshTask);

        Assert.Equal(2, f.Reader.Calls);
        Assert.False(f.ViewModel.IsPreviewStale);
    }

    [Fact]
    public async Task ANewChange_CancelsThePreviewInFlight_AndOnlyTheLastRunIsApplied()
    {
        var reader = new GatedReader(GoodTable());
        var f = new Fixture(reader: reader);
        await Bounded(f.ChooseAsync());
        reader.Gate = new TaskCompletionSource();

        f.ViewModel.DecimalStyle = DecimalStyle.Point;
        await WaitUntilAsync(() => reader.Calls == 2);
        Task first = f.ViewModel.RefreshTask;
        f.ViewModel.DecimalStyle = DecimalStyle.Comma;
        await WaitUntilAsync(() => reader.Cancelled == 1);
        reader.Gate.SetResult();
        await Bounded(f.ViewModel.RefreshTask);
        await Bounded(first);

        Assert.Equal(1, reader.Cancelled);
        Assert.Equal(ImportPanelState.Previewed, f.ViewModel.State);
        Assert.False(f.ViewModel.IsPreviewStale);
        Assert.False(f.ViewModel.IsBusy);
    }

    [Fact]
    public async Task DelimiterOverride_ReparsesTheRetainedBytesWithThatDelimiter()
    {
        var f = new Fixture();
        await Bounded(f.ChooseAsync());

        f.ViewModel.SelectedDelimiter = ';';
        await Bounded(f.ViewModel.RefreshTask);

        Assert.Equal(';', f.Reader.LastOptions!.Delimiter);
        Assert.Equal(2, f.Reader.Calls);
        Assert.Equal(1, f.Dialog.Calls);
    }

    [Fact]
    public async Task Confirm_AfterAnOptionChange_CommitsWithTheExactOptionsOfTheLastPreview()
    {
        var f = new Fixture();
        await Bounded(f.ChooseAsync());
        f.ViewModel.SkipDuplicates = false;
        await Bounded(f.ViewModel.RefreshTask);
        f.ViewModel.CreateMissingCategories = false;
        await Bounded(f.ViewModel.RefreshTask);

        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));

        Assert.Equal(ImportPanelState.Done, f.ViewModel.State);
        Assert.Single(f.Writer.Batches);
    }

    [Fact]
    public async Task ReprevewFailure_ShowsTheMessage_KeepsConfirmDisabled_AndNeverStaysBusy()
    {
        int calls = 0;
        var f = new Fixture(reader: new FakeTabularReader(() =>
            ++calls == 1
                ? Result.Success(GoodTable())
                : Result.Failure(ImportErrorCodes.UnreadableFile, "x", Details(ImportErrorCodes.ReasonMalformedCsv, "3"))));
        await Bounded(f.ChooseAsync());

        f.ViewModel.SelectedDelimiter = '\t';
        await Bounded(f.ViewModel.RefreshTask);

        Assert.True(f.ViewModel.IsError);
        Assert.Equal(Text("ImportErrorMalformedCsv", CultureInfo.CurrentUICulture, "3"), f.ViewModel.StatusMessage);
        Assert.True(f.ViewModel.IsPreviewStale);
        Assert.False(f.ViewModel.ConfirmCommand.CanExecute(null));
        Assert.False(f.ViewModel.IsBusy);
        Assert.Equal(ImportPanelState.Mapping, f.ViewModel.State);
    }

    // ---- (e) Confirm gating (S-22) ----

    [Fact]
    public async Task Confirm_IsDisabled_WhenNothingWouldBeImported()
    {
        var f = new Fixture(Table(Row("2026-03-01", "Uno", "abc"), Row("2026-03-02", "Dos", "xyz")));

        await Bounded(f.ChooseAsync());

        Assert.Equal(ImportPanelState.Previewed, f.ViewModel.State);
        Assert.Equal(0, f.ViewModel.Preview!.ToImportCount);
        Assert.False(f.ViewModel.ConfirmCommand.CanExecute(null));
        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));
        Assert.Empty(f.Writer.Batches);
    }

    [Fact]
    public async Task Confirm_WhileStale_DoesNothingEvenWhenExecutedDirectly()
    {
        var f = new Fixture();
        await Bounded(f.ChooseAsync());
        f.ViewModel.PreviewDebounce = TimeSpan.FromMinutes(5);
        f.ViewModel.DecimalStyle = DecimalStyle.Point;

        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));

        Assert.Empty(f.Writer.Batches);
        f.ViewModel.CancelCommand.Execute(null);
        await Bounded(f.ViewModel.RefreshTask);
    }

    // ---- (f) busy guards (S-25, R-22) ----

    [Fact]
    public async Task ChooseFile_ConcurrentCalls_OpenOneDialog_AndOtherCommandsReturnImmediately()
    {
        var f = new Fixture();
        f.Dialog.Gate = new TaskCompletionSource();

        Task first = f.ViewModel.ChooseFileCommand.ExecuteAsync(null);
        Task second = f.ViewModel.ChooseFileCommand.ExecuteAsync(null);
        await Bounded(second);
        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));
        f.ViewModel.CloseCommand.Execute(null);

        Assert.Equal(1, f.Dialog.Calls);
        Assert.Equal(ImportPanelState.Loading, f.ViewModel.State);
        Assert.True(f.ViewModel.IsBusy);
        Assert.Empty(f.Events);

        f.Dialog.Gate.SetResult();
        await Bounded(first);

        Assert.Equal(1, f.Dialog.Calls);
        Assert.Equal(ImportPanelState.Previewed, f.ViewModel.State);
        Assert.Equal(1, f.Reader.Calls);
    }

    [Fact]
    public async Task Confirm_ConcurrentCalls_CommitOnce_AndChooseFileIsIgnoredWhileImporting()
    {
        var writer = new GatedWriter();
        var f = new Fixture(writer: writer);
        await Bounded(f.ChooseAsync());

        Task first = f.ViewModel.ConfirmCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => writer.Calls == 1);
        Task second = f.ViewModel.ConfirmCommand.ExecuteAsync(null);
        await Bounded(second);
        await Bounded(f.ViewModel.ChooseFileCommand.ExecuteAsync(null));

        Assert.Equal(ImportPanelState.Importing, f.ViewModel.State);
        Assert.True(f.ViewModel.IsBusy);
        Assert.Equal(1, f.Dialog.Calls);

        writer.Gate.SetResult(Result.Success(new ImportCommitResult(2, 0, 0, 0, 0)));
        await Bounded(first);

        Assert.Equal(1, writer.Calls);
        Assert.Equal(ImportPanelState.Done, f.ViewModel.State);
        Assert.Single(f.Events, "Completed");
    }

    // ---- (g) stale preview at commit (C-19) ----

    [Fact]
    public async Task Confirm_WhenTheDatabaseChangedAfterThePreview_RefreshesThePreviewWithTheStaleMessage_AndWritesNothing()
    {
        int lookups = 0;
        Guid comida = FakeImportLookup.DefaultCategories().First().Id;
        var f = new Fixture(lookup: () => Result.Success(
            ++lookups == 1
                ? FakeImportLookup.Snapshot()
                : FakeImportLookup.Snapshot(existing:
                [
                    new ExistingTransactionKey(new DateOnly(2026, 3, 1), TransactionType.Expense, 10m, Currency.DOP, "Uno", comida),
                ])));
        await Bounded(f.ChooseAsync());
        Assert.Equal(0, f.ViewModel.Preview!.DuplicateCount);

        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));

        Assert.Empty(f.Writer.Batches);
        Assert.Equal(ImportPanelState.Previewed, f.ViewModel.State);
        Assert.Equal(Strings.ImportStaleMessage, f.ViewModel.StatusMessage);
        Assert.Equal(1, f.ViewModel.Preview!.DuplicateCount);
        Assert.False(f.ViewModel.IsPreviewStale);
        Assert.Empty(f.Events);
        Assert.False(f.ViewModel.IsBusy);
    }

    // ---- (h) success (S-26) ----

    [Fact]
    public async Task Confirm_Success_RaisesCompletedBeforeClose_AndRefreshesNotificationsOnce()
    {
        var f = new Fixture(Table(Row("2026-03-01", "Uno", "10", "Mascotas"), Row("2026-03-02", "Dos", "20")));
        await Bounded(f.ChooseAsync());
        ImportCompletedEventArgs? args = null;
        f.ViewModel.Completed += (_, e) => args = e;

        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));

        Assert.Equal(ImportPanelState.Done, f.ViewModel.State);
        Assert.Equal(["Completed"], f.Events);
        Assert.NotNull(args);
        Assert.Equal(new ImportCommitResult(2, 0, 1, 0, 0), args!.Result);
        Assert.Equal(2, args.Inserted);
        Assert.Equal(1, f.Center.TryRefreshCalls);
        Assert.False(f.ViewModel.HasSource);
        Assert.Contains("2", f.ViewModel.DoneMessage);
        Assert.False(f.ViewModel.IsBusy);

        f.ViewModel.CloseCommand.Execute(null);

        Assert.Equal(["Completed", "Close"], f.Events);
        Assert.Equal(ImportPanelState.Idle, f.ViewModel.State);
    }

    [Fact]
    public async Task Confirm_Success_SwallowsAFailingNotificationRefresh()
    {
        var f = new Fixture();
        var gate = new TaskCompletionSource<bool>();
        gate.SetException(new InvalidOperationException("boom"));
        f.Center.TryRefreshGate = gate;
        await Bounded(f.ChooseAsync());

        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));

        Assert.Equal(ImportPanelState.Done, f.ViewModel.State);
        Assert.Equal(1, f.Center.TryRefreshCalls);
        Assert.False(f.ViewModel.IsError);
    }

    [Fact]
    public async Task Confirm_Success_WorksWithoutANotificationCenter()
    {
        var f = new Fixture(withCenter: false);
        await Bounded(f.ChooseAsync());

        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));

        Assert.Equal(ImportPanelState.Done, f.ViewModel.State);
        Assert.Equal(0, f.Center.TryRefreshCalls);
    }

    [Fact]
    public async Task Confirm_Success_ASubscriberThatThrowsDoesNotTurnTheImportIntoAFailure()
    {
        var f = new Fixture();
        f.ViewModel.Completed += (_, _) => throw new InvalidOperationException("subscriber bug");
        await Bounded(f.ChooseAsync());

        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));

        Assert.Equal(ImportPanelState.Done, f.ViewModel.State);
        Assert.False(f.ViewModel.IsError);
        Assert.True(f.Logger.HasError);
    }

    // ---- (i) commit failure ----

    [Fact]
    public async Task Confirm_WriteFailure_ShowsTheMappedMessage_AndReturnsToPreviewed()
    {
        var writer = new FakeImportWriter(_ => Result.Failure(ImportErrorCodes.WriteFailed, @"C:\secret\data.db locked"));
        var f = new Fixture(writer: writer);
        await Bounded(f.ChooseAsync());

        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));

        Assert.Equal(Strings.ImportErrorWriteFailed, f.ViewModel.StatusMessage);
        Assert.True(f.ViewModel.IsError);
        Assert.DoesNotContain("secret", f.ViewModel.StatusMessage);
        Assert.Equal(ImportPanelState.Previewed, f.ViewModel.State);
        Assert.False(f.ViewModel.IsBusy);
        Assert.Empty(f.Events);
        Assert.Equal(0, f.Center.TryRefreshCalls);
    }

    // ---- (j) unexpected exceptions ----

    [Fact]
    public async Task ChooseFile_UnexpectedException_ShowsTheGenericMessage_LogsIt_AndLeavesNoBusyState()
    {
        var f = new Fixture(dialog: new ThrowingDialog(new InvalidOperationException(@"C:\secret\path exploded")));

        await Bounded(f.ViewModel.ChooseFileCommand.ExecuteAsync(null));

        Assert.Equal(Strings.ImportErrorFailed, f.ViewModel.StatusMessage);
        Assert.True(f.ViewModel.IsError);
        Assert.DoesNotContain("secret", f.ViewModel.StatusMessage);
        Assert.True(f.Logger.HasError);
        Assert.Equal(ImportPanelState.Idle, f.ViewModel.State);
        Assert.False(f.ViewModel.IsBusy);
    }

    [Fact]
    public async Task ChooseFile_CancellationWhileLoading_IsSilent()
    {
        var f = new Fixture(dialog: new ThrowingDialog(new OperationCanceledException()));

        await Bounded(f.ViewModel.ChooseFileCommand.ExecuteAsync(null));

        Assert.Equal(string.Empty, f.ViewModel.StatusMessage);
        Assert.False(f.ViewModel.IsError);
        Assert.Equal(ImportPanelState.Idle, f.ViewModel.State);
        Assert.False(f.Logger.HasError);
    }

    [Fact]
    public async Task Preview_LookupFailure_ShowsTheLookupMessage()
    {
        var f = new Fixture(lookup: () => Result.Failure(ImportErrorCodes.LookupFailed, "db"));

        await Bounded(f.ChooseAsync());

        Assert.Equal(Strings.ImportErrorLookupFailed, f.ViewModel.StatusMessage);
        Assert.Equal(ImportPanelState.Idle, f.ViewModel.State);
    }

    // ---- (k) cancel ----

    [Theory]
    [InlineData("mapping")]
    [InlineData("previewed")]
    public async Task Cancel_FromMappingOrPreviewed_DropsTheSource_AndRaisesClose(string from)
    {
        RawTable table = from == "mapping"
            ? TableWith(["When", "What", "How much"], null, ["2026-03-01", "Uno", "10"])
            : GoodTable();
        var f = new Fixture(table);
        await Bounded(f.ChooseAsync());
        Assert.Equal(from == "mapping" ? ImportPanelState.Mapping : ImportPanelState.Previewed, f.ViewModel.State);

        f.ViewModel.CancelCommand.Execute(null);

        Assert.Equal(ImportPanelState.Idle, f.ViewModel.State);
        Assert.Equal(["Close"], f.Events);
        Assert.False(f.ViewModel.HasSource);
        Assert.Null(f.ViewModel.Preview);

        f.ViewModel.DecimalStyle = DecimalStyle.Comma;
        Assert.False(f.ViewModel.IsPreviewStale);
        await Bounded(f.ViewModel.RefreshTask);
        Assert.Equal(1, f.Reader.Calls);
    }

    [Fact]
    public async Task Cancel_WhileLoading_CancelsThePendingWork_AndALateResultChangesNothing()
    {
        var f = new Fixture();
        f.Dialog.Gate = new TaskCompletionSource();
        Task choose = f.ViewModel.ChooseFileCommand.ExecuteAsync(null);
        Assert.Equal(ImportPanelState.Loading, f.ViewModel.State);

        f.ViewModel.CancelCommand.Execute(null);
        await Bounded(choose);

        Assert.Equal(ImportPanelState.Idle, f.ViewModel.State);
        Assert.Equal(["Close"], f.Events);
        Assert.Equal(0, f.Reader.Calls);
        Assert.Equal(string.Empty, f.ViewModel.StatusMessage);
    }

    [Fact]
    public async Task Cancel_IsIgnoredWhileImporting()
    {
        var writer = new GatedWriter();
        var f = new Fixture(writer: writer);
        await Bounded(f.ChooseAsync());
        Task confirm = f.ViewModel.ConfirmCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => writer.Calls == 1);

        f.ViewModel.CancelCommand.Execute(null);

        Assert.Equal(ImportPanelState.Importing, f.ViewModel.State);
        Assert.Empty(f.Events);
        writer.Gate.SetResult(Result.Success(new ImportCommitResult(2, 0, 0, 0, 0)));
        await Bounded(confirm);
    }

    // ---- (l) banners (C-6) ----

    [Fact]
    public async Task Banners_AmbiguousDates_ShowTheAmbiguousDateBanner()
    {
        var f = new Fixture(Table(Row("03/04/2026", "Uno", "10"), Row("05/06/2026", "Dos", "20")));

        await Bounded(f.ChooseAsync());

        Assert.Equal(DateEvidence.AmbiguousOnly, f.ViewModel.Preview!.Dates);
        Assert.Equal([Strings.ImportDateAmbiguousBanner], f.ViewModel.Banners);
    }

    [Fact]
    public async Task Banners_DatesThatContradictTheSelectedFormat_ShowTheContradictionBanner()
    {
        UseCulture("en");
        var f = new Fixture(Table(Row("25/03/2026", "Uno", "10"), Row("05/06/2026", "Dos", "20")));

        await Bounded(f.ChooseAsync());

        Assert.Equal(ImportDateFormat.MonthDayYear, f.ViewModel.DateFormat);
        Assert.Equal(DateEvidence.Contradicts, f.ViewModel.Preview!.Dates);
        Assert.Contains(Strings.ImportDateContradictsBanner, f.ViewModel.Banners);
    }

    [Fact]
    public async Task Banners_MixedDecimalStyles_ShowTheConflictBanner()
    {
        var f = new Fixture(Table(Row("2026-03-01", "Uno", "1,234.56"), Row("2026-03-02", "Dos", "1.234,56")));

        await Bounded(f.ChooseAsync());

        Assert.True(f.ViewModel.Preview!.Decimal.IsConflict);
        Assert.Contains(Strings.ImportDecimalConflictBanner, f.ViewModel.Banners);
        Assert.DoesNotContain(Strings.ImportDecimalAmbiguousBanner, f.ViewModel.Banners);
    }

    [Fact]
    public async Task Banners_AmbiguousDecimals_ShowTheAmbiguousBanner_OnlyWhileTheStyleIsAutomatic()
    {
        var f = new Fixture(Table(Row("2026-03-01", "Uno", "1,234"), Row("2026-03-02", "Dos", "2,500")));

        await Bounded(f.ChooseAsync());

        Assert.Contains(Strings.ImportDecimalAmbiguousBanner, f.ViewModel.Banners);
        Assert.Contains(Text("ImportDecimalStyleAutoDetectedFormat", CultureInfo.CurrentUICulture, "1,234.56"), f.ViewModel.DecimalDetectionNote);

        f.ViewModel.DecimalStyle = DecimalStyle.Point;
        await Bounded(f.ViewModel.RefreshTask);

        Assert.DoesNotContain(Strings.ImportDecimalAmbiguousBanner, f.ViewModel.Banners);
    }

    [Fact]
    public async Task Banners_EncodingFallback_ShowsTheSampleText()
    {
        var source = new TableSourceInfo(ImportFileKind.Csv, "Windows-1252", true, ';', null, "Categoría;Café");
        var f = new Fixture(TableWith(_headers, source, Row("2026-03-01", "Uno", "10")));

        await Bounded(f.ChooseAsync());

        Assert.Contains(Text("ImportEncodingFallbackBanner", CultureInfo.CurrentUICulture, "Categoría;Café"), f.ViewModel.Banners);
    }

    // ---- (l2) acknowledgement gate (W1/W2, C-1, C-6) ----

    private static RawTable AmbiguousAmountTable() =>
        Table(Row("2026-03-01", "Uno", "1,234"), Row("2026-03-02", "Dos", "2,500"));

    [Fact]
    public async Task Acknowledgement_AmbiguousAmounts_GateConfirmUntilAcknowledged()
    {
        var f = new Fixture(AmbiguousAmountTable());
        await Bounded(f.ChooseAsync());

        Assert.True(f.ViewModel.RequiresAcknowledgement);
        Assert.False(f.ViewModel.IsAcknowledged);
        Assert.False(f.ViewModel.ConfirmCommand.CanExecute(null));

        // Executing directly must be a no-op as well: ExecuteAsync ignores CanExecute.
        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));
        Assert.Empty(f.Writer.Batches);
        Assert.Equal(ImportPanelState.Previewed, f.ViewModel.State);
        Assert.Empty(f.Events);

        f.ViewModel.IsAcknowledged = true;
        Assert.True(f.ViewModel.ConfirmCommand.CanExecute(null));

        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));
        Assert.Single(f.Writer.Batches);
        Assert.Equal(ImportPanelState.Done, f.ViewModel.State);
    }

    [Fact]
    public async Task Acknowledgement_CleanFile_IsNotRequired()
    {
        var f = new Fixture();
        await Bounded(f.ChooseAsync());

        Assert.False(f.ViewModel.RequiresAcknowledgement);
        Assert.False(f.ViewModel.IsAcknowledged);
        Assert.True(f.ViewModel.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public async Task Acknowledgement_ExplicitDecimalStyle_ResolvesTheAmbiguityAndClearsTheRequirement()
    {
        var f = new Fixture(AmbiguousAmountTable());
        await Bounded(f.ChooseAsync());
        Assert.True(f.ViewModel.RequiresAcknowledgement);
        f.ViewModel.IsAcknowledged = true;

        f.ViewModel.DecimalStyle = DecimalStyle.Point;
        await Bounded(f.ViewModel.RefreshTask);

        Assert.False(f.ViewModel.RequiresAcknowledgement);
        Assert.False(f.ViewModel.IsAcknowledged);
        Assert.True(f.ViewModel.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public async Task Acknowledgement_ResetsWhenAnOptionChangesEvenIfTheAmbiguityRemains()
    {
        var f = new Fixture(AmbiguousAmountTable());
        await Bounded(f.ChooseAsync());
        f.ViewModel.IsAcknowledged = true;
        Assert.True(f.ViewModel.ConfirmCommand.CanExecute(null));

        f.ViewModel.SkipDuplicates = false;
        await Bounded(f.ViewModel.RefreshTask);

        Assert.True(f.ViewModel.RequiresAcknowledgement);
        Assert.False(f.ViewModel.IsAcknowledged);
        Assert.False(f.ViewModel.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public async Task Acknowledgement_ResetsWhenTheUserChangesOptionsWhileTheRefreshIsPending()
    {
        var f = new Fixture(AmbiguousAmountTable());
        await Bounded(f.ChooseAsync());
        f.ViewModel.IsAcknowledged = true;
        f.ViewModel.PreviewDebounce = TimeSpan.FromMinutes(5);

        f.ViewModel.DefaultCurrency = Currency.USD;

        Assert.False(f.ViewModel.IsAcknowledged);
        Assert.False(f.ViewModel.ConfirmCommand.CanExecute(null));
        f.ViewModel.CancelCommand.Execute(null);
        await Bounded(f.ViewModel.RefreshTask);
    }

    [Fact]
    public async Task Acknowledgement_ResetsWhenTheCommitFindsAStalePlan()
    {
        int lookups = 0;
        Guid comida = FakeImportLookup.DefaultCategories().First().Id;
        var f = new Fixture(
            AmbiguousAmountTable(),
            lookup: () => Result.Success(
                ++lookups == 1
                    ? FakeImportLookup.Snapshot()
                    : FakeImportLookup.Snapshot(existing:
                    [
                        new ExistingTransactionKey(new DateOnly(2026, 3, 1), TransactionType.Expense, 1234m, Currency.DOP, "Uno", comida),
                    ])));
        await Bounded(f.ChooseAsync());
        string firstHash = f.ViewModel.Preview!.PlanHash;
        f.ViewModel.IsAcknowledged = true;

        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));

        Assert.Empty(f.Writer.Batches);
        Assert.NotEqual(firstHash, f.ViewModel.Preview!.PlanHash);
        Assert.True(f.ViewModel.RequiresAcknowledgement);
        Assert.False(f.ViewModel.IsAcknowledged);
        Assert.False(f.ViewModel.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public async Task Acknowledgement_MixedDecimalStyles_Required_WhileAutomatic()
    {
        var f = new Fixture(Table(Row("2026-03-01", "Uno", "1,234.56"), Row("2026-03-02", "Dos", "1.234,56")));
        await Bounded(f.ChooseAsync());

        Assert.True(f.ViewModel.Preview!.Decimal.IsConflict);
        Assert.True(f.ViewModel.Preview.ToImportCount > 0);
        Assert.True(f.ViewModel.RequiresAcknowledgement);
        Assert.False(f.ViewModel.ConfirmCommand.CanExecute(null));

        f.ViewModel.DecimalStyle = DecimalStyle.Point;
        await Bounded(f.ViewModel.RefreshTask);

        Assert.False(f.ViewModel.RequiresAcknowledgement);
    }

    [Fact]
    public async Task Acknowledgement_AmbiguousDates_Required()
    {
        var f = new Fixture(Table(Row("03/04/2026", "Uno", "10"), Row("05/06/2026", "Dos", "20")));
        await Bounded(f.ChooseAsync());

        Assert.Equal(DateEvidence.AmbiguousOnly, f.ViewModel.Preview!.Dates);
        Assert.True(f.ViewModel.RequiresAcknowledgement);
        Assert.False(f.ViewModel.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public async Task Acknowledgement_DatesContradictingTheFormat_RequiredUntilTheFormatResolvesThem()
    {
        UseCulture("en");
        var f = new Fixture(Table(Row("25/03/2026", "Uno", "10"), Row("26/03/2026", "Dos", "20")));
        await Bounded(f.ChooseAsync());

        Assert.Equal(DateEvidence.Contradicts, f.ViewModel.Preview!.Dates);
        // Every row contradicts the month-first format, so nothing would be imported: nothing to acknowledge.
        Assert.Equal(0, f.ViewModel.Preview.ToImportCount);
        Assert.False(f.ViewModel.RequiresAcknowledgement);

        // A row that parses month-first next to rows that contradict it: there is something to import.
        var g = new Fixture(Table(Row("25/03/2026", "Uno", "10"), Row("2026-03-02", "Dos", "20")));
        await Bounded(g.ChooseAsync());
        Assert.Equal(DateEvidence.Contradicts, g.ViewModel.Preview!.Dates);
        Assert.True(g.ViewModel.Preview.ToImportCount > 0);
        Assert.True(g.ViewModel.RequiresAcknowledgement);

        g.ViewModel.DateFormat = ImportDateFormat.DayMonthYear;
        await Bounded(g.ViewModel.RefreshTask);

        Assert.Equal(DateEvidence.Consistent, g.ViewModel.Preview!.Dates);
        Assert.False(g.ViewModel.RequiresAcknowledgement);
        Assert.True(g.ViewModel.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public async Task Acknowledgement_IsClearedWhenThePanelIsClosed()
    {
        var f = new Fixture(AmbiguousAmountTable());
        await Bounded(f.ChooseAsync());
        f.ViewModel.IsAcknowledged = true;

        f.ViewModel.CancelCommand.Execute(null);

        Assert.False(f.ViewModel.IsAcknowledged);
        Assert.False(f.ViewModel.RequiresAcknowledgement);
    }

    [Fact]
    public void Acknowledgement_TextsAreLocalizedInBothCultures() => AssertLocalized("ImportAcknowledgeLabel");

    [Fact]
    public async Task Banners_CleanFile_ShowsNone()
    {
        var f = new Fixture();

        await Bounded(f.ChooseAsync());

        Assert.Empty(f.ViewModel.Banners);
    }

    // ---- (m) culture defaults ----

    [Theory]
    [InlineData("es-DO", ImportDateFormat.DayMonthYear)]
    [InlineData("en", ImportDateFormat.MonthDayYear)]
    public async Task DateFormat_DefaultsFromTheUiCulture(string culture, ImportDateFormat expected)
    {
        UseCulture(culture);
        var f = new Fixture();

        await Bounded(f.ChooseAsync());

        Assert.Equal(expected, f.ViewModel.DateFormat);
        Assert.Equal(DecimalStyle.Auto, f.ViewModel.DecimalStyle);
        Assert.Equal(TransactionType.Expense, f.ViewModel.DefaultType);
        Assert.Equal(Currency.DOP, f.ViewModel.DefaultCurrency);
        Assert.Equal(SignMode.Unsigned, f.ViewModel.SignMode);
        Assert.True(f.ViewModel.SkipDuplicates);
        Assert.True(f.ViewModel.CreateMissingCategories);
    }

    // ---- (n) error mapping covers every code ----

    [Fact]
    public void ImportErrorKey_CoversEveryFileLevelCode_AndDefaultsToTheGenericMessage()
    {
        var expected = new Dictionary<string, string>
        {
            [ImportErrorCodes.UnreadableFile] = "ImportErrorUnreadable",
            [ImportErrorCodes.TooLarge] = "ImportErrorTooLarge",
            [ImportErrorCodes.NoRows] = "ImportErrorNoRows",
            [ImportErrorCodes.PreviewStale] = "ImportErrorPreviewStale",
            [ImportErrorCodes.LookupFailed] = "ImportErrorLookupFailed",
            [ImportErrorCodes.WriteFailed] = "ImportErrorWriteFailed",
            [ImportErrorCodes.MappingIncomplete] = "ImportErrorMappingIncomplete",
        };

        string[] codes = ConstantsOf(typeof(ImportErrorCodes))
            .Where(c => !c.Name.StartsWith("Detail", StringComparison.Ordinal) && !c.Name.StartsWith("Reason", StringComparison.Ordinal))
            .Select(c => c.Value)
            .ToArray();

        // A new file-level code fails here until someone decides its message.
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), codes.Order(StringComparer.Ordinal));
        foreach ((string code, string key) in expected)
        {
            Assert.Equal(key, ImportViewModel.ImportErrorKey(code, null));
            AssertLocalized(key);
        }

        Assert.Equal("ImportErrorFailed", ImportViewModel.ImportErrorKey("SomethingNew", null));
        Assert.Equal("ImportErrorFailed", ImportViewModel.ImportErrorKey(null, null));
        AssertLocalized("ImportErrorFailed");
    }

    [Fact]
    public void ImportErrorKey_CoversEveryReasonConstant()
    {
        var expected = new Dictionary<string, string>
        {
            [ImportErrorCodes.ReasonMalformedCsv] = "ImportErrorMalformedCsv",
            [ImportErrorCodes.ReasonBinary] = "ImportErrorBinary",
            [ImportErrorCodes.ReasonLegacyXls] = "ImportErrorLegacyXls",
            [ImportErrorCodes.ReasonNotAWorkbook] = "ImportErrorUnreadable",
            [ImportErrorCodes.ReasonFileTooLarge] = "ImportErrorTooLarge",
            [ImportErrorCodes.ReasonTooManyRows] = "ImportErrorTooLarge",
            [ImportErrorCodes.ReasonTooManyColumns] = "ImportErrorTooLarge",
            [ImportErrorCodes.ReasonFieldTooLong] = "ImportErrorTooLarge",
            [ImportErrorCodes.ReasonZipTooLarge] = "ImportErrorTooLarge",
        };
        var tooLargeReasons = new HashSet<string>
        {
            ImportErrorCodes.ReasonFileTooLarge,
            ImportErrorCodes.ReasonTooManyRows,
            ImportErrorCodes.ReasonTooManyColumns,
            ImportErrorCodes.ReasonFieldTooLong,
            ImportErrorCodes.ReasonZipTooLarge,
        };

        string[] reasons = ConstantsOf(typeof(ImportErrorCodes))
            .Where(c => c.Name.StartsWith("Reason", StringComparison.Ordinal))
            .Select(c => c.Value)
            .ToArray();

        // A new reason constant fails here until someone decides its message.
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), reasons.Order(StringComparer.Ordinal));
        foreach ((string reason, string key) in expected)
        {
            string code = tooLargeReasons.Contains(reason) ? ImportErrorCodes.TooLarge : ImportErrorCodes.UnreadableFile;
            Assert.Equal(key, ImportViewModel.ImportErrorKey(code, reason));
            AssertLocalized(key);
        }
    }

    [Fact]
    public void ReasonText_ExistsForEveryImportReason_InBothCultures()
    {
        foreach (string cultureName in new[] { "es-DO", "en" })
        {
            UseCulture(cultureName);
            foreach (ImportReason reason in Enum.GetValues<ImportReason>())
            {
                string text = ImportRowItem.ReasonText(reason);
                Assert.False(string.IsNullOrWhiteSpace(text), reason.ToString());
                Assert.NotEqual(reason.ToString(), text);
            }
        }
    }

    // ---- (o) backup recommendation is copy only ----

    [Fact]
    public async Task ConfirmPanel_ShowsTheBackupRecommendation_ButDoesNotGateOnIt()
    {
        var f = new Fixture();
        await Bounded(f.ChooseAsync());

        Assert.Equal(Strings.ImportBackupRecommendation, f.ViewModel.BackupRecommendation);
        Assert.True(f.ViewModel.ConfirmCommand.CanExecute(null));
    }

    // ---- preview presentation ----

    [Fact]
    public async Task PreviewRows_RenderDatesWithTheUiCulture_AndLocalizedReasonText()
    {
        var f = new Fixture(Table(
            Row("2026-03-04", "Uno", "1234.5", "Mascotas"),
            Row("2026-03-05", "Dos", "abc")));

        await Bounded(f.ChooseAsync());

        ImportRowItem first = f.ViewModel.SampleRows[0];
        Assert.Equal(new DateTime(2026, 3, 4).ToString("dd MMM yyyy", CultureInfo.CurrentUICulture), first.DateText);
        Assert.Equal(1234.5m.ToString("#,##0.00##", CultureInfo.CurrentUICulture) + " DOP", first.AmountText);
        Assert.Equal(Strings.ImportTypeExpense, first.TypeText);
        Assert.True(first.IsNewCategory);
        Assert.Equal("Mascotas", first.CategoryText);
        Assert.Equal(Strings.ImportStatusValid, first.StatusText);

        ImportRowItem problem = Assert.Single(f.ViewModel.ProblemRows);
        Assert.Equal(3, problem.RowNumber);
        Assert.Equal(Strings.ImportStatusRejected, problem.StatusText);
        Assert.Equal(Strings.ImportReasonInvalidAmount, problem.ReasonsText);
        Assert.Equal(["Mascotas"], f.ViewModel.CategoriesToCreate);
        Assert.True(f.ViewModel.HasCategoriesToCreate);
        Assert.False(first.IsDuplicate);
        Assert.False(problem.IsDuplicate);
        Assert.True(problem.IsRejected);
        Assert.Contains(f.ViewModel.ReasonSummary, s => s.Text == Strings.ImportReasonInvalidAmount && s.Count == 1);

        f.ViewModel.OnlyProblems = true;
        Assert.Equal(f.ViewModel.ProblemRows, f.ViewModel.VisibleRows);
        f.ViewModel.OnlyProblems = false;
        Assert.Equal(f.ViewModel.SampleRows, f.ViewModel.VisibleRows);
    }

    [Fact]
    public void RowItem_OfADuplicateRow_IsFlaggedDuplicateAndAProblem_ButNotRejected()
    {
        var row = new ImportRowResult(4, ImportRowStatus.Duplicate, new DateOnly(2026, 3, 4), TransactionType.Expense, 10m, Currency.DOP, "Uno", "Comida", false, [ImportReason.DuplicateImported]);

        var item = new ImportRowItem(row);

        Assert.True(item.IsDuplicate);
        Assert.True(item.IsProblem);
        Assert.False(item.IsRejected);
        Assert.Equal(Strings.ImportStatusDuplicate, item.StatusText);
    }

    [Fact]
    public async Task PreviewRows_EnglishDatesUseTheEnglishMonthName()
    {
        UseCulture("en");
        var f = new Fixture(Table(Row("2026-03-04", "Uno", "10")));

        await Bounded(f.ChooseAsync());

        Assert.Equal("04 Mar 2026", f.ViewModel.SampleRows[0].DateText);
    }

    [Fact]
    public async Task SourceFacts_DescribeTheFileAndTheSummaryCountsTheRows()
    {
        var f = new Fixture(Table(Row("2026-03-01", "Uno", "10"), Row("2026-03-02", "Dos", "abc")));

        await Bounded(f.ChooseAsync());

        Assert.Equal(Text("ImportSourceFactsCsvFormat", CultureInfo.CurrentUICulture, "UTF-8", ",", "2"), f.ViewModel.SourceFacts);
        Assert.Equal("recorded.zip", f.ViewModel.FileName);
        Assert.Equal(Text("ImportSummaryFormat", CultureInfo.CurrentUICulture, "1", "0", "1", "0"), f.ViewModel.SummaryText);
    }

    [Fact]
    public async Task Done_ShowsTheInsertedAndSkippedCounts()
    {
        var f = new Fixture();
        await Bounded(f.ChooseAsync());

        await Bounded(f.ViewModel.ConfirmCommand.ExecuteAsync(null));

        Assert.Equal(Text("ImportDoneFormat", CultureInfo.CurrentUICulture, "2", "0", "0", "0"), f.ViewModel.DoneMessage);
        Assert.True(f.ViewModel.IsDonePanelVisible);
    }

    // ---- S7 frontend-design review: action bar and retry visibility ----

    [Fact]
    public async Task ActionBar_IsVisibleOnlyWhileLoadingMappingOrPreviewed()
    {
        var mapping = new Fixture(TableWith(["When", "What", "How much"], null, ["2026-03-01", "Uno", "10"]));
        var previewed = new Fixture();
        var done = new Fixture();
        var loading = new Fixture();
        loading.Dialog.Gate = new TaskCompletionSource();

        Assert.False(previewed.ViewModel.IsActionBarVisible);

        Task pending = loading.ViewModel.ChooseFileCommand.ExecuteAsync(null);
        Assert.Equal(ImportPanelState.Loading, loading.ViewModel.State);
        Assert.True(loading.ViewModel.IsActionBarVisible);
        loading.ViewModel.CancelCommand.Execute(null);
        await Bounded(pending);
        Assert.False(loading.ViewModel.IsActionBarVisible);

        await Bounded(mapping.ChooseAsync());
        Assert.True(mapping.ViewModel.IsActionBarVisible);

        await Bounded(previewed.ChooseAsync());
        Assert.True(previewed.ViewModel.IsActionBarVisible);

        await Bounded(done.ChooseAsync());
        await Bounded(done.ViewModel.ConfirmCommand.ExecuteAsync(null));
        Assert.Equal(ImportPanelState.Done, done.ViewModel.State);
        Assert.False(done.ViewModel.IsActionBarVisible);

        var writer = new GatedWriter();
        var importing = new Fixture(writer: writer);
        await Bounded(importing.ChooseAsync());
        Task confirm = importing.ViewModel.ConfirmCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => writer.Calls == 1);
        Assert.False(importing.ViewModel.IsActionBarVisible);
        writer.Gate.SetResult(Result.Success(new ImportCommitResult(2, 0, 0, 0, 0)));
        await Bounded(confirm);
    }

    [Fact]
    public async Task ActionBar_RaisesPropertyChangedWhenTheStateChanges()
    {
        var f = new Fixture();
        var changed = new List<string?>();
        f.ViewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await Bounded(f.ChooseAsync());

        Assert.Contains(nameof(ImportViewModel.IsActionBarVisible), changed);
    }

    [Fact]
    public async Task Retry_IsVisibleOnlyInIdleAfterAFailure_AndChoosingAgainWorks()
    {
        var f = new Fixture(readerResult: Result.Failure(ImportErrorCodes.NoRows, "no rows"));
        Assert.False(f.ViewModel.IsRetryVisible);

        await Bounded(f.ChooseAsync());

        Assert.Equal(ImportPanelState.Idle, f.ViewModel.State);
        Assert.True(f.ViewModel.IsError);
        Assert.True(f.ViewModel.IsRetryVisible);
        Assert.True(f.ViewModel.ChooseFileCommand.CanExecute(null));

        await Bounded(f.ChooseAsync());

        Assert.Equal(2, f.Dialog.Calls);
        Assert.True(f.ViewModel.IsRetryVisible);
    }

    [Fact]
    public async Task Retry_IsHiddenAfterACancelledPicker_AndWhileLoading()
    {
        var f = new Fixture();
        f.Dialog.Result = null;

        await Bounded(f.ViewModel.ChooseFileCommand.ExecuteAsync(null));

        Assert.False(f.ViewModel.IsRetryVisible);

        f.Dialog.Gate = new TaskCompletionSource();
        Task pending = f.ViewModel.ChooseFileCommand.ExecuteAsync(null);
        Assert.False(f.ViewModel.IsRetryVisible);
        f.ViewModel.CancelCommand.Execute(null);
        await Bounded(pending);
    }

    private static IEnumerable<(string Name, string Value)> ConstantsOf(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (field.Name, (string)field.GetRawConstantValue()!));

    private static void AssertLocalized(string key)
    {
        foreach (string cultureName in new[] { "es-DO", "en" })
        {
            Assert.False(
                string.IsNullOrWhiteSpace(Strings.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(cultureName))),
                $"{key} ({cultureName})");
        }
    }
}
