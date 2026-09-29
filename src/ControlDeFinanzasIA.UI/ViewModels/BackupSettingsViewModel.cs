using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Backup.CreateBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.InspectBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.RestoreBackup;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.Services;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>The phases of the Backup section of Settings (F-019, DD-9).</summary>
public enum BackupPanelState
{
    /// <summary>Nothing is running; export and restore can be started.</summary>
    Idle,

    /// <summary>The save dialog is open or the backup is being written.</summary>
    Exporting,

    /// <summary>The open dialog is open or the chosen file is being validated.</summary>
    Inspecting,

    /// <summary>A valid backup was chosen and the destructive confirmation is shown.</summary>
    ConfirmPending,

    /// <summary>The restore is running.</summary>
    Restoring,

    /// <summary>The data was replaced; the app must restart. Terminal for this session.</summary>
    RestartRequired,
}

/// <summary>
/// View-model of the Backup section in Settings (F-019, DD-9): export to a ZIP, restore through an
/// inline confirmation panel, then a terminal "restart required" state. Every command guards its own
/// state synchronously before the first await because <c>IAsyncRelayCommand.ExecuteAsync</c> does not
/// consult <c>CanExecute</c>. No exception escapes a command and no busy state can get stuck.
/// </summary>
/// <remarks>
/// Registered as a singleton: <see cref="BackupPanelState.RestartRequired"/> must survive navigation
/// (a fresh instance per visit would allow a second restore, which would wait forever on the
/// notification suspension that is held until process exit).
/// </remarks>
public sealed partial class BackupSettingsViewModel : ViewModelBase
{
    private const string _zipExtension = "zip";
    private const string _zipMimeType = "application/zip";
    private const string _unknownValue = "?";

    private readonly CreateBackupUseCase _createBackup;
    private readonly InspectBackupUseCase _inspectBackup;
    private readonly RestoreBackupUseCase _restoreBackup;
    private readonly IFileSaveDialog _saveDialog;
    private readonly IFileOpenDialog _openDialog;
    private readonly INotificationCenter _notificationCenter;
    private readonly IAppRestartService _restartService;
    private readonly IClock _clock;
    private readonly ILogger<BackupSettingsViewModel> _logger;
    private readonly string _appVersion;

    private OpenTarget? _pendingSource;

    // Held until process exit after a successful restore (DD-7): nothing may write into the new file.
    private IDisposable? _suspension;

    /// <summary>Initializes a new instance of the <see cref="BackupSettingsViewModel"/> class.</summary>
    public BackupSettingsViewModel(
        CreateBackupUseCase createBackup,
        InspectBackupUseCase inspectBackup,
        RestoreBackupUseCase restoreBackup,
        IFileSaveDialog saveDialog,
        IFileOpenDialog openDialog,
        INotificationCenter notificationCenter,
        IAppRestartService restartService,
        IClock clock,
        ILogger<BackupSettingsViewModel> logger,
        string? appVersion = null)
    {
        ArgumentNullException.ThrowIfNull(createBackup);
        ArgumentNullException.ThrowIfNull(inspectBackup);
        ArgumentNullException.ThrowIfNull(restoreBackup);
        ArgumentNullException.ThrowIfNull(saveDialog);
        ArgumentNullException.ThrowIfNull(openDialog);
        ArgumentNullException.ThrowIfNull(notificationCenter);
        ArgumentNullException.ThrowIfNull(restartService);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _createBackup = createBackup;
        _inspectBackup = inspectBackup;
        _restoreBackup = restoreBackup;
        _saveDialog = saveDialog;
        _openDialog = openDialog;
        _notificationCenter = notificationCenter;
        _restartService = restartService;
        _clock = clock;
        _logger = logger;
        _appVersion = appVersion ?? ReadAppVersion();
    }

    /// <summary>Gets or sets the current phase of the section.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(IsConfirmVisible))]
    [NotifyPropertyChangedFor(nameof(IsRestartPanelVisible))]
    [NotifyPropertyChangedFor(nameof(AreActionsVisible))]
    [NotifyPropertyChangedFor(nameof(WorkingMessage))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChooseRestoreFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmRestoreCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelRestoreCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestartNowCommand))]
    private BackupPanelState _state = BackupPanelState.Idle;

    /// <summary>Gets or sets the outcome text of the last operation; empty after a cancel.</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Gets or sets a value indicating whether <see cref="StatusMessage"/> describes a failure.</summary>
    [ObservableProperty]
    private bool _isError;

    /// <summary>Gets or sets the facts of the backup awaiting confirmation.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingSummary))]
    private BackupInfo? _pendingInfo;

    /// <summary>Gets or sets a value indicating whether the automatic restart failed and the manual fallback text applies.</summary>
    [ObservableProperty]
    private bool _restartFailed;

    /// <summary>Gets a value indicating whether an operation is in flight (drives the indeterminate progress bar).</summary>
    public bool IsBusy => State is BackupPanelState.Exporting or BackupPanelState.Inspecting or BackupPanelState.Restoring;

    /// <summary>Gets a value indicating whether the destructive confirmation panel is shown.</summary>
    public bool IsConfirmVisible => State == BackupPanelState.ConfirmPending;

    /// <summary>Gets a value indicating whether the restart panel is shown.</summary>
    public bool IsRestartPanelVisible => State == BackupPanelState.RestartRequired;

    /// <summary>Gets a value indicating whether the export and restore buttons are shown.</summary>
    public bool AreActionsVisible => State != BackupPanelState.RestartRequired;

    /// <summary>Gets the localized progress caption of the running operation; empty when nothing runs.</summary>
    public string WorkingMessage => State switch
    {
        BackupPanelState.Exporting => Strings.BackupWorkingExport,
        BackupPanelState.Inspecting => Strings.BackupWorkingInspect,
        BackupPanelState.Restoring => Strings.BackupWorkingRestore,
        _ => string.Empty,
    };

    /// <summary>Gets the localized facts and notes about the backup awaiting confirmation.</summary>
    public string PendingSummary
    {
        get
        {
            BackupInfo? info = PendingInfo;
            if (info is null)
            {
                return string.Empty;
            }

            DateTime exportedLocal = DateTime.SpecifyKind(info.ExportedAtUtc, DateTimeKind.Utc).ToLocalTime();
            var lines = new List<string>
            {
                string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.BackupConfirmDetailsFormat,
                    exportedLocal.ToString("g", CultureInfo.CurrentCulture),
                    info.AppVersion),
            };

            if (info.IsOlderSchema)
            {
                lines.Add(Strings.BackupConfirmOlderSchemaNote);
            }

            if (info.LocalApiKeyWillBeLost)
            {
                lines.Add(Strings.BackupConfirmApiKeyLostNote);
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>Picks a destination and writes a backup ZIP there.</summary>
    [RelayCommand(CanExecute = nameof(CanStart), AllowConcurrentExecutions = true)]
    private async Task ExportAsync()
    {
        // The real guard: ExecuteAsync does not consult CanExecute.
        if (State != BackupPanelState.Idle)
        {
            return;
        }

        BeginOperation(BackupPanelState.Exporting);

        try
        {
            using SaveTarget? target = await _saveDialog.PickAsync(
                BackupFileName.Create(_clock.UtcNow, TimeZoneInfo.Local),
                BackupFilter(),
                Strings.BackupExportDialogTitle);
            if (target is null)
            {
                // Cancelled: nothing is written and no message is shown.
                return;
            }

            Result result = await _createBackup.ExecuteAsync(target);
            if (result.IsSuccess)
            {
                StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.BackupExportSucceeded, target.DisplayName);
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
            ShowUnexpectedFailure(ex, "Unable to export the backup.");
        }
        finally
        {
            State = BackupPanelState.Idle;
        }
    }

    /// <summary>Picks a backup file, validates it and shows the confirmation panel.</summary>
    [RelayCommand(CanExecute = nameof(CanStart), AllowConcurrentExecutions = true)]
    private async Task ChooseRestoreFileAsync()
    {
        if (State != BackupPanelState.Idle)
        {
            return;
        }

        BeginOperation(BackupPanelState.Inspecting);
        BackupPanelState next = BackupPanelState.Idle;
        OpenTarget? source = null;

        try
        {
            source = await _openDialog.PickAsync(BackupFilter(), Strings.BackupRestoreDialogTitle);
            if (source is null)
            {
                // Cancelled: no use case call and no message.
                return;
            }

            Result result = await _inspectBackup.ExecuteAsync(source);
            if (result.IsSuccess && result.Value is BackupInfo info)
            {
                _pendingSource = source;
                source = null;
                PendingInfo = info;
                next = BackupPanelState.ConfirmPending;
            }
            else
            {
                ShowFailure(result.IsSuccess ? null : result.Error);
            }
        }
        catch (OperationCanceledException)
        {
            // Not a failure: no message.
        }
        catch (Exception ex)
        {
            ShowUnexpectedFailure(ex, "Unable to inspect the backup file.");
        }
        finally
        {
            source?.Dispose();
            State = next;
        }
    }

    /// <summary>Replaces the live data with the confirmed backup.</summary>
    [RelayCommand(CanExecute = nameof(CanConfirm), AllowConcurrentExecutions = true)]
    private async Task ConfirmRestoreAsync()
    {
        if (State != BackupPanelState.ConfirmPending || _pendingSource is null)
        {
            return;
        }

        OpenTarget source = _pendingSource;
        _pendingSource = null;
        BeginOperation(BackupPanelState.Restoring);
        IDisposable? suspension = null;
        bool restored = false;

        try
        {
            // Background notification work must be quiet before the database file is swapped (DD-7).
            suspension = await _notificationCenter.SuspendAsync();

            Result result = await _restoreBackup.ExecuteAsync(source);
            if (result.IsSuccess)
            {
                restored = true;
                _suspension = suspension;
                StatusMessage = BuildRestoreSuccessMessage(result.Value as RestoreOutcome);
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
            ShowUnexpectedFailure(ex, "Unable to restore the backup.");
        }
        finally
        {
            if (!restored)
            {
                suspension?.Dispose();
            }

            source.Dispose();
            PendingInfo = null;
            State = restored ? BackupPanelState.RestartRequired : BackupPanelState.Idle;
        }
    }

    /// <summary>Discards the pending restore without touching any data.</summary>
    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void CancelRestore()
    {
        if (State != BackupPanelState.ConfirmPending)
        {
            return;
        }

        _pendingSource?.Dispose();
        _pendingSource = null;
        PendingInfo = null;
        ClearStatus();
        State = BackupPanelState.Idle;
    }

    /// <summary>Relaunches the application; on failure the manual fallback text is shown.</summary>
    [RelayCommand(CanExecute = nameof(CanRestart))]
    private void RestartNow()
    {
        if (State != BackupPanelState.RestartRequired)
        {
            return;
        }

        try
        {
            RestartFailed = !_restartService.TryRestart();
        }
        catch (Exception ex)
        {
            RestartFailed = true;
            _logger.LogError(ex, "Restarting the application after a restore failed.");
        }
    }

    /// <summary>
    /// Maps a use-case failure to its localized message. Unknown codes, unknown reasons and missing
    /// errors fall back to the generic I/O message, so no path or exception text is ever shown.
    /// </summary>
    internal string ErrorMessage(Error? error)
    {
        switch (error?.Code)
        {
            case BackupErrorCodes.NotABackup:
                return Strings.BackupErrorNotABackup;
            case BackupErrorCodes.Corrupt:
                return Strings.BackupErrorCorrupt;
            case BackupErrorCodes.NewerSchema:
                return string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.BackupErrorNewerSchema,
                    Detail(error, BackupErrorCodes.DetailBackupAppVersion),
                    Detail(error, BackupErrorCodes.DetailBackupSchema),
                    _appVersion,
                    Detail(error, BackupErrorCodes.DetailAppSchema));
            case BackupErrorCodes.IoError:
                return Detail(error, BackupErrorCodes.DetailReason) == BackupErrorCodes.ReasonInsufficientSpace
                    ? Strings.BackupErrorInsufficientSpace
                    : Strings.BackupErrorIoError;
            default:
                return Strings.BackupErrorIoError;
        }
    }

    private static string Detail(Error error, string key)
        => error.Details is not null
            && error.Details.TryGetValue(key, out string[]? values)
            && values.Length > 0
            && !string.IsNullOrWhiteSpace(values[0])
                ? values[0]
                : _unknownValue;

    private static FileTypeFilter BackupFilter() => new(Strings.BackupFileTypeName, _zipExtension, _zipMimeType);

    private static string ReadAppVersion()
        => typeof(BackupSettingsViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? "0.0.0";

    private bool CanStart() => State == BackupPanelState.Idle;

    private bool CanConfirm() => State == BackupPanelState.ConfirmPending;

    private bool CanRestart() => State == BackupPanelState.RestartRequired;

    private static string BuildRestoreSuccessMessage(RestoreOutcome? outcome)
    {
        string message = string.Format(
            CultureInfo.CurrentCulture,
            Strings.BackupRestoreSucceeded,
            outcome?.SafetyBackupFileName ?? string.Empty);

        if (outcome is null)
        {
            return message;
        }

        if (outcome.ApiKeyLost)
        {
            message += " " + Strings.BackupApiKeyNotRestoredNotice;
        }
        else if (outcome.ApiKeyCarriedOver)
        {
            message += " " + Strings.BackupApiKeyKeptNotice;
        }

        return message;
    }

    private void BeginOperation(BackupPanelState state)
    {
        ClearStatus();
        State = state;
    }

    private void ClearStatus()
    {
        StatusMessage = string.Empty;
        IsError = false;
        RestartFailed = false;
    }

    private void ShowFailure(Error? error)
    {
        IsError = true;
        StatusMessage = ErrorMessage(error);
        _logger.LogWarning(
            "Backup operation failed: {ErrorCode} {ErrorMessage}",
            error?.Code,
            error?.Message);
    }

    private void ShowUnexpectedFailure(Exception ex, string logMessage)
    {
        IsError = true;
        StatusMessage = Strings.BackupErrorIoError;
        _logger.LogError(ex, "{Message}", logMessage);
    }
}
