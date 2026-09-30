using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.CommitImport;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using ControlDeFinanzasIA.Application.UseCases.Import.PreviewImport;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.Services;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>The phases of the import panel (F-020, DD-14).</summary>
public enum ImportPanelState
{
    /// <summary>Nothing is loaded; a file can be chosen.</summary>
    Idle,

    /// <summary>The picker is open, the file is being read or a preview is being computed.</summary>
    Loading,

    /// <summary>The columns could not be mapped automatically; the user must map the required ones.</summary>
    Mapping,

    /// <summary>A fresh dry run is shown and can be confirmed.</summary>
    Previewed,

    /// <summary>The import is being written.</summary>
    Importing,

    /// <summary>The import succeeded; the result is shown until the panel is closed.</summary>
    Done,
}

/// <summary>
/// View-model of the import panel hosted by the Expenses screen (F-020, DD-14, R-22). It drives the picker,
/// the dry run and the commit through the real use cases, keeps the picked bytes only in memory for the
/// commit, and never shows raw exception text or file paths. Every command guards its own state
/// synchronously before its first await because <c>IAsyncRelayCommand.ExecuteAsync</c> does not consult
/// <c>CanExecute</c>. No exception escapes a command and no busy state can get stuck.
/// </summary>
public sealed partial class ImportViewModel : ViewModelBase
{
    private const string _unknownValue = "?";
    private const string _pointExample = "1,234.56";
    private const string _commaExample = "1.234,56";
    private const string _csvMimeType = "text/csv";
    private const string _textMimeType = "text/plain";
    private const string _excelMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly HashSet<string> _optionProperties =
    [
        nameof(SelectedDelimiter),
        nameof(DecimalStyle),
        nameof(DateFormat),
        nameof(DefaultType),
        nameof(DefaultCurrency),
        nameof(SignMode),
        nameof(SkipDuplicates),
        nameof(CreateMissingCategories),
    ];

    private static readonly HashSet<string> _columnProperties =
    [
        nameof(SelectedDateColumn),
        nameof(SelectedTypeColumn),
        nameof(SelectedDescriptionColumn),
        nameof(SelectedCategoryColumn),
        nameof(SelectedAmountColumn),
        nameof(SelectedCurrencyColumn),
        nameof(SelectedPaymentMethodColumn),
    ];

    private readonly PreviewImportUseCase _previewImport;
    private readonly CommitImportUseCase _commitImport;
    private readonly IFileOpenDialog _openDialog;
    private readonly ILogger<ImportViewModel> _logger;
    private readonly INotificationCenter? _notificationCenter;

    // The retained bytes of the picked file: only the commit and the re-previews read them (D4).
    private ImportSource? _source;

    // The exact instance used for the last successful preview; it is the one handed to the commit.
    private ImportOptions? _previewOptions;

    private CancellationTokenSource? _previewCts;
    private Task _refreshTask = Task.CompletedTask;
    private bool _suppressChanges;
    private bool _mappingTouched;

    /// <summary>Initializes a new instance of the <see cref="ImportViewModel"/> class.</summary>
    /// <param name="previewImport">Dry-run use case.</param>
    /// <param name="commitImport">Commit use case.</param>
    /// <param name="openDialog">File picker.</param>
    /// <param name="logger">Logger for failures.</param>
    /// <param name="notificationCenter">Optional notification centre refreshed after a successful import.</param>
    public ImportViewModel(
        PreviewImportUseCase previewImport,
        CommitImportUseCase commitImport,
        IFileOpenDialog openDialog,
        ILogger<ImportViewModel> logger,
        INotificationCenter? notificationCenter = null)
    {
        ArgumentNullException.ThrowIfNull(previewImport);
        ArgumentNullException.ThrowIfNull(commitImport);
        ArgumentNullException.ThrowIfNull(openDialog);
        ArgumentNullException.ThrowIfNull(logger);

        _previewImport = previewImport;
        _commitImport = commitImport;
        _openDialog = openDialog;
        _logger = logger;
        _notificationCenter = notificationCenter;
        _dateFormat = DefaultDateFormat();
        RebuildChoices();
    }

    /// <summary>Raised after a successful import, before the panel is closed, so the host can reload its data.</summary>
    public event EventHandler<ImportCompletedEventArgs>? Completed;

    /// <summary>Raised when the panel should be closed (cancelled picker, cancel, or close after Done).</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Gets or sets how long option changes wait before the preview is recomputed (test seam).</summary>
    public TimeSpan PreviewDebounce { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Gets the current phase of the panel.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(IsWorkbenchVisible))]
    [NotifyPropertyChangedFor(nameof(IsMappingVisible))]
    [NotifyPropertyChangedFor(nameof(IsConfirmVisible))]
    [NotifyPropertyChangedFor(nameof(IsDonePanelVisible))]
    [NotifyCanExecuteChangedFor(nameof(ChooseFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    private ImportPanelState _state = ImportPanelState.Idle;

    /// <summary>Gets the localized outcome or error text of the last operation; empty when there is none.</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Gets a value indicating whether <see cref="StatusMessage"/> describes a failure.</summary>
    [ObservableProperty]
    private bool _isError;

    /// <summary>Gets the localized progress caption of the running operation; empty when nothing runs.</summary>
    [ObservableProperty]
    private string _workingMessage = string.Empty;

    /// <summary>Gets a value indicating whether the shown preview no longer matches the options.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private bool _isPreviewStale;

    /// <summary>Gets the last dry run.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    [NotifyPropertyChangedFor(nameof(IsWorkbenchVisible))]
    [NotifyPropertyChangedFor(nameof(IsMappingVisible))]
    [NotifyPropertyChangedFor(nameof(IsDelimiterVisible))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private ImportPreview? _preview;

    /// <summary>Gets the name of the picked file.</summary>
    [ObservableProperty]
    private string _fileName = string.Empty;

    /// <summary>Gets the localized facts about how the file was read.</summary>
    [ObservableProperty]
    private string _sourceFacts = string.Empty;

    /// <summary>Gets the localized one-line summary of the preview counts.</summary>
    [ObservableProperty]
    private string _summaryText = string.Empty;

    /// <summary>Gets the localized result of the finished import.</summary>
    [ObservableProperty]
    private string _doneMessage = string.Empty;

    /// <summary>Gets the localized hint showing the decimal style detected in the file; empty when nothing was detected.</summary>
    [ObservableProperty]
    private string _decimalDetectionNote = string.Empty;

    /// <summary>Gets the localized banners that currently apply to the file (encoding, decimal and date warnings).</summary>
    [ObservableProperty]
    private IReadOnlyList<string> _banners = Array.Empty<string>();

    /// <summary>Gets or sets a value indicating whether the mapping choices are expanded while previewed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMappingVisible))]
    private bool _isMappingExpanded;

    /// <summary>Gets or sets a value indicating whether only problem rows are listed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleRows))]
    private bool _onlyProblems;

    /// <summary>Gets the first preview rows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleRows))]
    private IReadOnlyList<ImportRowItem> _sampleRows = Array.Empty<ImportRowItem>();

    /// <summary>Gets the first rows that are rejected, duplicated or carry a warning.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleRows))]
    private IReadOnlyList<ImportRowItem> _problemRows = Array.Empty<ImportRowItem>();

    /// <summary>Gets the localized count of rows per reason.</summary>
    [ObservableProperty]
    private IReadOnlyList<ImportReasonSummaryItem> _reasonSummary = Array.Empty<ImportReasonSummaryItem>();

    /// <summary>Gets the names of the categories the import would create.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCategoriesToCreate))]
    private IReadOnlyList<string> _categoriesToCreate = Array.Empty<string>();

    /// <summary>Gets the header choices shared by the seven column pickers ("(none)" first).</summary>
    [ObservableProperty]
    private IReadOnlyList<ColumnChoice> _columnChoices = Array.Empty<ColumnChoice>();

    /// <summary>Gets the delimiter choices.</summary>
    [ObservableProperty]
    private IReadOnlyList<ImportOptionChoice<char?>> _delimiterChoices = Array.Empty<ImportOptionChoice<char?>>();

    /// <summary>Gets the decimal style choices.</summary>
    [ObservableProperty]
    private IReadOnlyList<ImportOptionChoice<DecimalStyle>> _decimalStyleChoices = Array.Empty<ImportOptionChoice<DecimalStyle>>();

    /// <summary>Gets the date format choices.</summary>
    [ObservableProperty]
    private IReadOnlyList<ImportOptionChoice<ImportDateFormat>> _dateFormatChoices = Array.Empty<ImportOptionChoice<ImportDateFormat>>();

    /// <summary>Gets the default type choices.</summary>
    [ObservableProperty]
    private IReadOnlyList<ImportOptionChoice<TransactionType>> _typeChoices = Array.Empty<ImportOptionChoice<TransactionType>>();

    /// <summary>Gets the default currency choices.</summary>
    [ObservableProperty]
    private IReadOnlyList<ImportOptionChoice<Currency>> _currencyChoices = Array.Empty<ImportOptionChoice<Currency>>();

    /// <summary>Gets the sign mode choices.</summary>
    [ObservableProperty]
    private IReadOnlyList<ImportOptionChoice<SignMode>> _signModeChoices = Array.Empty<ImportOptionChoice<SignMode>>();

    /// <summary>Gets or sets the CSV delimiter override; <c>null</c> sniffs it.</summary>
    [ObservableProperty]
    private char? _selectedDelimiter;

    /// <summary>Gets or sets how decimal and grouping separators are read.</summary>
    [ObservableProperty]
    private DecimalStyle _decimalStyle = DecimalStyle.Auto;

    /// <summary>Gets or sets the order of non-ISO text dates.</summary>
    [ObservableProperty]
    private ImportDateFormat _dateFormat;

    /// <summary>Gets or sets the type used when the file does not say.</summary>
    [ObservableProperty]
    private TransactionType _defaultType = TransactionType.Expense;

    /// <summary>Gets or sets the currency used when the file does not say.</summary>
    [ObservableProperty]
    private Currency _defaultCurrency = Currency.DOP;

    /// <summary>Gets or sets whether the amount sign decides the transaction type.</summary>
    [ObservableProperty]
    private SignMode _signMode = SignMode.Unsigned;

    /// <summary>Gets or sets a value indicating whether rows that already exist are skipped.</summary>
    [ObservableProperty]
    private bool _skipDuplicates = true;

    /// <summary>Gets or sets a value indicating whether missing categories are created.</summary>
    [ObservableProperty]
    private bool _createMissingCategories = true;

    /// <summary>Gets or sets the column mapped to the date.</summary>
    [ObservableProperty]
    private ColumnChoice? _selectedDateColumn;

    /// <summary>Gets or sets the column mapped to the type.</summary>
    [ObservableProperty]
    private ColumnChoice? _selectedTypeColumn;

    /// <summary>Gets or sets the column mapped to the description.</summary>
    [ObservableProperty]
    private ColumnChoice? _selectedDescriptionColumn;

    /// <summary>Gets or sets the column mapped to the category.</summary>
    [ObservableProperty]
    private ColumnChoice? _selectedCategoryColumn;

    /// <summary>Gets or sets the column mapped to the amount.</summary>
    [ObservableProperty]
    private ColumnChoice? _selectedAmountColumn;

    /// <summary>Gets or sets the column mapped to the currency.</summary>
    [ObservableProperty]
    private ColumnChoice? _selectedCurrencyColumn;

    /// <summary>Gets or sets the column mapped to the payment method.</summary>
    [ObservableProperty]
    private ColumnChoice? _selectedPaymentMethodColumn;

    /// <summary>Gets a value indicating whether a file is loading or an import is running (drives the progress bar).</summary>
    public bool IsBusy => State is ImportPanelState.Loading or ImportPanelState.Importing;

    /// <summary>Gets a value indicating whether the import would create categories.</summary>
    public bool HasCategoriesToCreate => CategoriesToCreate.Count > 0;

    /// <summary>Gets a value indicating whether a preview exists.</summary>
    public bool HasPreview => Preview is not null;

    /// <summary>Gets a value indicating whether the file bytes are retained (false in every state without a picked file).</summary>
    public bool HasSource => _source is not null;

    /// <summary>Gets a value indicating whether the options, banners and preview list are shown.</summary>
    public bool IsWorkbenchVisible =>
        Preview is not null && State is ImportPanelState.Loading or ImportPanelState.Mapping or ImportPanelState.Previewed;

    /// <summary>Gets a value indicating whether the column pickers are shown.</summary>
    public bool IsMappingVisible =>
        IsWorkbenchVisible && (State == ImportPanelState.Mapping || IsMappingExpanded);

    /// <summary>Gets a value indicating whether the delimiter option applies (CSV files only).</summary>
    public bool IsDelimiterVisible => Preview?.Source.Kind == ImportFileKind.Csv;

    /// <summary>Gets a value indicating whether the confirmation panel is shown.</summary>
    public bool IsConfirmVisible => State == ImportPanelState.Previewed;

    /// <summary>Gets a value indicating whether the result panel is shown.</summary>
    public bool IsDonePanelVisible => State == ImportPanelState.Done;

    /// <summary>Gets the rows the preview list shows.</summary>
    public IReadOnlyList<ImportRowItem> VisibleRows => OnlyProblems ? ProblemRows : SampleRows;

    /// <summary>Gets the localized recommendation to back up before importing (copy only, never enforced).</summary>
    public string BackupRecommendation => Strings.ImportBackupRecommendation;

    /// <summary>Gets the task of the latest scheduled preview refresh (test seam).</summary>
    internal Task RefreshTask => _refreshTask;

    /// <summary>Picks a file, reads it and shows its preview.</summary>
    [RelayCommand(CanExecute = nameof(CanChooseFile), AllowConcurrentExecutions = true)]
    private async Task ChooseFileAsync()
    {
        // The real guard: ExecuteAsync does not consult CanExecute.
        if (State != ImportPanelState.Idle)
        {
            return;
        }

        DropSession();
        ClearStatus();
        RebuildChoices();
        CancellationToken ct = NewPreviewCts().Token;
        BeginBusy(ImportPanelState.Loading, Strings.ImportWorkingRead);
        OpenTarget? target = null;

        try
        {
            target = await _openDialog.PickAsync(PickerFilters(), Strings.ImportDialogTitle, ct);
            if (target is null)
            {
                // Cancelled: no message and no use case call.
                if (!ct.IsCancellationRequested)
                {
                    State = ImportPanelState.Idle;
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            // The loader takes ownership of the target and disposes it.
            OpenTarget picked = target;
            target = null;
            Result loaded = await ImportSourceLoader.LoadAsync(picked, ct);
            ct.ThrowIfCancellationRequested();

            if (!loaded.IsSuccess || loaded.Value is not ImportSource source)
            {
                ShowFailure(loaded.Error);
                State = ImportPanelState.Idle;
                return;
            }

            _source = source;
            FileName = source.DisplayName;
            OnPropertyChanged(nameof(HasSource));
            ResetOptions();

            if (!await RunPreviewAsync(source, BuildOptions(), ct))
            {
                DropSession();
                State = ImportPanelState.Idle;
            }
        }
        catch (OperationCanceledException)
        {
            // Not a failure: whoever cancelled owns the state.
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                ShowUnexpectedFailure(ex, "Unable to load the import file.");
                DropSession();
                State = ImportPanelState.Idle;
            }
        }
        finally
        {
            target?.Dispose();
            if (!ct.IsCancellationRequested && State == ImportPanelState.Loading)
            {
                State = ImportPanelState.Idle;
            }
        }
    }

    /// <summary>Writes the previewed rows.</summary>
    [RelayCommand(CanExecute = nameof(CanConfirm), AllowConcurrentExecutions = true)]
    private async Task ConfirmAsync()
    {
        if (!CanConfirm() || _source is null || _previewOptions is null || Preview is null)
        {
            return;
        }

        ImportSource source = _source;
        ImportOptions options = _previewOptions;
        string planHash = Preview.PlanHash;
        ClearStatus();
        BeginBusy(ImportPanelState.Importing, Strings.ImportWorkingCommit);
        CancellationToken active = CancellationToken.None;
        bool handled = false;

        try
        {
            Result result = await _commitImport.ExecuteAsync(source, options, planHash);

            if (result.IsSuccess && result.Value is ImportCommitResult committed)
            {
                handled = true;
                FinishImport(committed);
                RaiseCompleted(committed);
                RefreshNotificationsInBackground();
            }
            else if (result.Error?.Code == ImportErrorCodes.PreviewStale)
            {
                // The data changed after the preview: show the fresh plan, write nothing.
                handled = true;
                IsPreviewStale = true;
                active = NewPreviewCts().Token;
                if (await RunPreviewAsync(source, options, active))
                {
                    IsError = false;
                    StatusMessage = Strings.ImportStaleMessage;
                }
                else
                {
                    EnterFailedRefreshState();
                }
            }
            else
            {
                ShowFailure(result.Error);
            }
        }
        catch (OperationCanceledException)
        {
            // Not a failure: no message.
        }
        catch (Exception ex)
        {
            ShowUnexpectedFailure(ex, "Unable to import the file.");
        }
        finally
        {
            if (!handled && !active.IsCancellationRequested)
            {
                State = ImportPanelState.Previewed;
            }
        }
    }

    /// <summary>Abandons the import: cancels pending work, drops the file and asks the host to close the panel.</summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        if (!CanCancel())
        {
            return;
        }

        CancelPreview();
        DropSession();
        ClearStatus();
        State = ImportPanelState.Idle;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Closes the result panel.</summary>
    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Close()
    {
        if (!CanClose())
        {
            return;
        }

        DropSession();
        ClearStatus();
        State = ImportPanelState.Idle;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows or hides the column pickers while previewed.</summary>
    [RelayCommand]
    private void ToggleMapping() => IsMappingExpanded = !IsMappingExpanded;

    /// <summary>
    /// Maps a file-level failure to the name of its localized message. Unknown codes and missing codes fall
    /// back to the generic message, so no path or exception text is ever shown.
    /// </summary>
    /// <param name="code">The <see cref="ImportErrorCodes"/> code.</param>
    /// <param name="reason">The <c>reason</c> detail, when the code has one.</param>
    /// <returns>The resource key.</returns>
    internal static string ImportErrorKey(string? code, string? reason) => code switch
    {
        ImportErrorCodes.UnreadableFile => reason switch
        {
            ImportErrorCodes.ReasonMalformedCsv => "ImportErrorMalformedCsv",
            ImportErrorCodes.ReasonLegacyXls => "ImportErrorLegacyXls",
            ImportErrorCodes.ReasonBinary => "ImportErrorBinary",
            _ => "ImportErrorUnreadable",
        },
        ImportErrorCodes.TooLarge => "ImportErrorTooLarge",
        ImportErrorCodes.NoRows => "ImportErrorNoRows",
        ImportErrorCodes.PreviewStale => "ImportErrorPreviewStale",
        ImportErrorCodes.LookupFailed => "ImportErrorLookupFailed",
        ImportErrorCodes.WriteFailed => "ImportErrorWriteFailed",
        ImportErrorCodes.MappingIncomplete => "ImportErrorMappingIncomplete",
        _ => "ImportErrorFailed",
    };

    /// <summary>Maps a use-case failure to its localized message.</summary>
    /// <param name="error">The failure; may be <c>null</c>.</param>
    /// <returns>The localized message.</returns>
    internal static string ErrorMessage(Error? error)
    {
        string? reason = Detail(error, ImportErrorCodes.DetailReason);
        string key = ImportErrorKey(error?.Code, reason);
        string template = Strings.ResourceManager.GetString(key, Strings.Culture)
            ?? Strings.ImportErrorFailed;
        CultureInfo culture = Strings.Culture ?? CultureInfo.CurrentUICulture;

        return key switch
        {
            "ImportErrorMalformedCsv" => string.Format(culture, template, Detail(error, ImportErrorCodes.DetailLine) ?? _unknownValue),
            "ImportErrorTooLarge" => string.Format(
                culture,
                template,
                (ImportLimits.MaxFileBytes / (1024 * 1024)).ToString("N0", culture),
                ImportLimits.MaxDataRows.ToString("N0", culture),
                ImportLimits.MaxColumns.ToString("N0", culture),
                ImportLimits.MaxFieldChars.ToString("N0", culture)),
            _ => template,
        };
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_suppressChanges || e.PropertyName is null)
        {
            return;
        }

        if (_optionProperties.Contains(e.PropertyName))
        {
            if (e.PropertyName == nameof(SelectedDelimiter))
            {
                // A different parse can change the headers: go back to the automatic mapping.
                _mappingTouched = false;
            }

            ScheduleRefresh();
        }
        else if (_columnProperties.Contains(e.PropertyName))
        {
            _mappingTouched = true;
            ScheduleRefresh();
        }
    }

    private static string? Detail(Error? error, string key)
        => error?.Details is not null
            && error.Details.TryGetValue(key, out string[]? values)
            && values.Length > 0
            && !string.IsNullOrWhiteSpace(values[0])
                ? values[0]
                : null;

    private static ImportDateFormat DefaultDateFormat() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es"
            ? ImportDateFormat.DayMonthYear
            : ImportDateFormat.MonthDayYear;

    private static IReadOnlyList<FileTypeFilter> PickerFilters() =>
    [
        new FileTypeFilter(Strings.ImportFileTypeCsv, "csv", _csvMimeType),
        new FileTypeFilter(Strings.ImportFileTypeText, "txt", _textMimeType),
        new FileTypeFilter(Strings.ImportFileTypeExcel, "xlsx", _excelMimeType),
    ];

    private static string DelimiterLabel(char? delimiter) => delimiter switch
    {
        '\t' => Strings.ImportDelimiterTab,
        { } c => c.ToString(),
        _ => _unknownValue,
    };

    private bool CanChooseFile() => State == ImportPanelState.Idle;

    private bool CanConfirm() =>
        State == ImportPanelState.Previewed
        && !IsPreviewStale
        && Preview is { ToImportCount: > 0 }
        && _source is not null;

    private bool CanCancel() => State is ImportPanelState.Loading or ImportPanelState.Mapping or ImportPanelState.Previewed;

    private bool CanClose() => State == ImportPanelState.Done;

    private CancellationTokenSource NewPreviewCts()
    {
        CancelPreview();
        var cts = new CancellationTokenSource();
        _previewCts = cts;
        return cts;
    }

    private void CancelPreview()
    {
        CancellationTokenSource? previous = _previewCts;
        _previewCts = null;
        if (previous is null)
        {
            return;
        }

        try
        {
            previous.Cancel();
        }
        finally
        {
            previous.Dispose();
        }
    }

    private void BeginBusy(ImportPanelState state, string message)
    {
        WorkingMessage = message;
        State = state;
    }

    private void ClearStatus()
    {
        StatusMessage = string.Empty;
        IsError = false;
    }

    private void ScheduleRefresh()
    {
        if (_source is null
            || State is ImportPanelState.Idle or ImportPanelState.Importing or ImportPanelState.Done)
        {
            return;
        }

        IsPreviewStale = true;
        ClearStatus();
        CancellationToken ct = NewPreviewCts().Token;
        _refreshTask = RefreshAsync(_source, ct);
    }

    private async Task RefreshAsync(ImportSource source, CancellationToken ct)
    {
        try
        {
            if (PreviewDebounce > TimeSpan.Zero)
            {
                await Task.Delay(PreviewDebounce, ct);
            }
            else
            {
                await Task.Yield();
            }

            ct.ThrowIfCancellationRequested();

            if (!await RunPreviewAsync(source, BuildOptions(), ct))
            {
                EnterFailedRefreshState();
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded or cancelled: whoever did it owns the state.
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                ShowUnexpectedFailure(ex, "Unable to refresh the import preview.");
                EnterFailedRefreshState();
            }
        }
    }

    // Runs the dry run and applies it. Returns false (with the message set) on a file-level failure.
    private async Task<bool> RunPreviewAsync(ImportSource source, ImportOptions options, CancellationToken ct)
    {
        BeginBusy(ImportPanelState.Loading, Strings.ImportWorkingPreview);
        Result result = await _previewImport.ExecuteAsync(source, options, ct);
        ct.ThrowIfCancellationRequested();

        if (result.IsSuccess && result.Value is ImportPreview preview)
        {
            ApplyPreview(preview, options);
            return true;
        }

        ShowFailure(result.Error);
        return false;
    }

    // A failed re-preview keeps the last preview on screen (stale) so the user can fix the options.
    private void EnterFailedRefreshState()
    {
        IsPreviewStale = true;
        State = ImportPanelState.Mapping;
    }

    private void ApplyPreview(ImportPreview preview, ImportOptions options)
    {
        CultureInfo culture = CultureInfo.CurrentUICulture;
        _previewOptions = options;

        WithoutRefresh(() => UpdateColumns(preview));

        Preview = preview;
        IsPreviewStale = false;
        SampleRows = preview.SampleRows.Select(row => new ImportRowItem(row)).ToArray();
        ProblemRows = preview.ProblemRows.Select(row => new ImportRowItem(row)).ToArray();
        ReasonSummary = preview.ReasonCounts
            .Select(rc => new ImportReasonSummaryItem(ImportRowItem.ReasonText(rc.Reason), rc.Count, ImportReasons.IsRejection(rc.Reason)))
            .ToArray();
        CategoriesToCreate = preview.CategoriesToCreate;
        SourceFacts = BuildSourceFacts(preview, culture);
        SummaryText = string.Format(
            culture,
            Strings.ImportSummaryFormat,
            preview.ToImportCount.ToString("N0", culture),
            preview.DuplicateCount.ToString("N0", culture),
            preview.RejectedCount.ToString("N0", culture),
            preview.WarningCount.ToString("N0", culture));
        DecimalDetectionNote = preview.Decimal.IsApplicable
            ? string.Format(
                culture,
                Strings.ImportDecimalStyleAutoDetectedFormat,
                preview.Decimal.Resolved == DecimalStyle.Comma ? _commaExample : _pointExample)
            : string.Empty;
        Banners = BuildBanners(preview, options, culture);
        State = preview.Mapping.IsComplete ? ImportPanelState.Previewed : ImportPanelState.Mapping;
    }

    private void UpdateColumns(ImportPreview preview)
    {
        if (ColumnChoices.Count != preview.Headers.Count + 1
            || !ColumnChoices.Skip(1).Select(c => c.Index).SequenceEqual(Enumerable.Range(0, preview.Headers.Count).Select(i => (int?)i)))
        {
            var choices = new List<ColumnChoice>(preview.Headers.Count + 1) { new(null, Strings.ImportColumnNone) };
            for (int i = 0; i < preview.Headers.Count; i++)
            {
                string header = preview.Headers[i];
                choices.Add(new ColumnChoice(i, string.IsNullOrWhiteSpace(header) ? (i + 1).ToString(CultureInfo.CurrentUICulture) : header));
            }

            ColumnChoices = choices;
        }

        ColumnMapping mapping = preview.Mapping;
        SelectedDateColumn = ChoiceFor(mapping.Date);
        SelectedTypeColumn = ChoiceFor(mapping.Type);
        SelectedDescriptionColumn = ChoiceFor(mapping.Description);
        SelectedCategoryColumn = ChoiceFor(mapping.Category);
        SelectedAmountColumn = ChoiceFor(mapping.Amount);
        SelectedCurrencyColumn = ChoiceFor(mapping.Currency);
        SelectedPaymentMethodColumn = ChoiceFor(mapping.PaymentMethod);
    }

    private ColumnChoice? ChoiceFor(int? index) =>
        ColumnChoices.FirstOrDefault(c => c.Index == index) ?? ColumnChoices.FirstOrDefault();

    private ColumnMapping BuildMapping() => new(
        SelectedDateColumn?.Index,
        SelectedTypeColumn?.Index,
        SelectedDescriptionColumn?.Index,
        SelectedCategoryColumn?.Index,
        SelectedAmountColumn?.Index,
        SelectedCurrencyColumn?.Index,
        SelectedPaymentMethodColumn?.Index);

    private ImportOptions BuildOptions() => new(
        _mappingTouched ? BuildMapping() : null,
        SelectedDelimiter,
        DecimalStyle,
        DateFormat,
        DefaultType,
        DefaultCurrency,
        SignMode,
        SkipDuplicates,
        CreateMissingCategories);

    private void ResetOptions() => WithoutRefresh(() =>
    {
        _mappingTouched = false;
        SelectedDelimiter = null;
        DecimalStyle = DecimalStyle.Auto;
        DateFormat = DefaultDateFormat();
        DefaultType = TransactionType.Expense;
        DefaultCurrency = Currency.DOP;
        SignMode = SignMode.Unsigned;
        SkipDuplicates = true;
        CreateMissingCategories = true;
    });

    private void WithoutRefresh(Action action)
    {
        bool previous = _suppressChanges;
        _suppressChanges = true;
        try
        {
            action();
        }
        finally
        {
            _suppressChanges = previous;
        }
    }

    // Forgets the file and everything derived from it; the status message is left to the caller.
    private void DropSession()
    {
        _source = null;
        _previewOptions = null;
        OnPropertyChanged(nameof(HasSource));
        Preview = null;
        FileName = string.Empty;
        SourceFacts = string.Empty;
        SummaryText = string.Empty;
        DoneMessage = string.Empty;
        DecimalDetectionNote = string.Empty;
        Banners = Array.Empty<string>();
        SampleRows = Array.Empty<ImportRowItem>();
        ProblemRows = Array.Empty<ImportRowItem>();
        ReasonSummary = Array.Empty<ImportReasonSummaryItem>();
        CategoriesToCreate = Array.Empty<string>();
        IsPreviewStale = false;
        IsMappingExpanded = false;
        OnlyProblems = false;
        WithoutRefresh(() =>
        {
            ColumnChoices = [new ColumnChoice(null, Strings.ImportColumnNone)];
            SelectedDateColumn = null;
            SelectedTypeColumn = null;
            SelectedDescriptionColumn = null;
            SelectedCategoryColumn = null;
            SelectedAmountColumn = null;
            SelectedCurrencyColumn = null;
            SelectedPaymentMethodColumn = null;
        });
    }

    private void RebuildChoices() => WithoutRefresh(() =>
    {
        ColumnChoices = [new ColumnChoice(null, Strings.ImportColumnNone)];
        DelimiterChoices =
        [
            new ImportOptionChoice<char?>(null, Strings.ImportDelimiterAuto),
            new ImportOptionChoice<char?>(',', Strings.ImportDelimiterComma),
            new ImportOptionChoice<char?>(';', Strings.ImportDelimiterSemicolon),
            new ImportOptionChoice<char?>('\t', Strings.ImportDelimiterTab),
        ];
        DecimalStyleChoices =
        [
            new ImportOptionChoice<DecimalStyle>(DecimalStyle.Auto, Strings.ImportDecimalStyleAuto),
            new ImportOptionChoice<DecimalStyle>(DecimalStyle.Point, Strings.ImportDecimalStylePoint),
            new ImportOptionChoice<DecimalStyle>(DecimalStyle.Comma, Strings.ImportDecimalStyleComma),
        ];
        DateFormatChoices =
        [
            new ImportOptionChoice<ImportDateFormat>(ImportDateFormat.DayMonthYear, Strings.ImportDateFormatDayMonthYear),
            new ImportOptionChoice<ImportDateFormat>(ImportDateFormat.MonthDayYear, Strings.ImportDateFormatMonthDayYear),
            new ImportOptionChoice<ImportDateFormat>(ImportDateFormat.DayMonthYearDash, Strings.ImportDateFormatDayMonthYearDash),
        ];
        TypeChoices =
        [
            new ImportOptionChoice<TransactionType>(TransactionType.Expense, Strings.ImportTypeExpense),
            new ImportOptionChoice<TransactionType>(TransactionType.Income, Strings.ImportTypeIncome),
        ];
        CurrencyChoices = Enum.GetValues<Currency>()
            .Select(currency => new ImportOptionChoice<Currency>(currency, currency.ToString()))
            .ToArray();
        SignModeChoices =
        [
            new ImportOptionChoice<SignMode>(SignMode.Unsigned, Strings.ImportSignModeUnsigned),
            new ImportOptionChoice<SignMode>(SignMode.NegativeIsExpense, Strings.ImportSignModeNegativeIsExpense),
        ];
    });

    private static string BuildSourceFacts(ImportPreview preview, CultureInfo culture)
    {
        string rows = preview.TotalRows.ToString("N0", culture);
        TableSourceInfo source = preview.Source;
        return source.Kind == ImportFileKind.Xlsx
            ? string.Format(culture, Strings.ImportSourceFactsXlsxFormat, source.SheetName ?? _unknownValue, rows)
            : string.Format(culture, Strings.ImportSourceFactsCsvFormat, source.EncodingName ?? _unknownValue, DelimiterLabel(source.Delimiter), rows);
    }

    private static IReadOnlyList<string> BuildBanners(ImportPreview preview, ImportOptions options, CultureInfo culture)
    {
        var banners = new List<string>();

        if (preview.Source.EncodingFallback)
        {
            banners.Add(string.Format(culture, Strings.ImportEncodingFallbackBanner, preview.Source.SampleText));
        }

        if (preview.Decimal.IsConflict)
        {
            banners.Add(Strings.ImportDecimalConflictBanner);
        }
        else if (preview.Decimal.Ambiguous > 0 && options.DecimalStyle == DecimalStyle.Auto)
        {
            banners.Add(Strings.ImportDecimalAmbiguousBanner);
        }

        switch (preview.Dates)
        {
            case DateEvidence.AmbiguousOnly:
                banners.Add(Strings.ImportDateAmbiguousBanner);
                break;
            case DateEvidence.Contradicts:
                banners.Add(Strings.ImportDateContradictsBanner);
                break;
        }

        return banners;
    }

    private void FinishImport(ImportCommitResult result)
    {
        CultureInfo culture = CultureInfo.CurrentUICulture;
        CancelPreview();
        DropSession();
        DoneMessage = string.Format(
            culture,
            Strings.ImportDoneFormat,
            (result.ExpensesInserted + result.IncomesInserted).ToString("N0", culture),
            result.DuplicatesSkipped.ToString("N0", culture),
            result.Rejected.ToString("N0", culture),
            result.CategoriesCreated.ToString("N0", culture));
        State = ImportPanelState.Done;
    }

    private void RaiseCompleted(ImportCommitResult result)
    {
        try
        {
            Completed?.Invoke(this, new ImportCompletedEventArgs(result));
        }
        catch (Exception ex)
        {
            // The rows are already written: a failing subscriber must not turn success into a failure.
            _logger.LogError(ex, "A subscriber of the import Completed event failed.");
        }
    }

    private void RefreshNotificationsInBackground()
    {
        if (_notificationCenter is null)
        {
            return;
        }

        _ = RefreshNotificationsAsync(_notificationCenter);
    }

    private async Task RefreshNotificationsAsync(INotificationCenter center)
    {
        try
        {
            await center.TryRefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Refreshing the notifications after an import failed.");
        }
    }

    private void ShowFailure(Error? error)
    {
        IsError = true;
        StatusMessage = ErrorMessage(error);
        _logger.LogWarning(
            "Import operation failed: {ErrorCode} {Reason}",
            error?.Code,
            Detail(error, ImportErrorCodes.DetailReason));
    }

    private void ShowUnexpectedFailure(Exception ex, string logMessage)
    {
        IsError = true;
        StatusMessage = Strings.ImportErrorFailed;
        _logger.LogError(ex, "{Message}", logMessage);
    }
}
