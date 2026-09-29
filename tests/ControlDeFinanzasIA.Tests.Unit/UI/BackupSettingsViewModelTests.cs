using System.Globalization;
using System.Reflection;
using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Backup.CreateBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.InspectBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.RestoreBackup;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for <see cref="BackupSettingsViewModel"/> (F-019, S6b): real use cases over
/// <see cref="FakeBackupService"/>, fakes only for the ports around the view-model.
/// </summary>
public sealed class BackupSettingsViewModelTests
{
    private const string _appVersion = "9.9.9";

    private static readonly DateTime _exportedAtUtc = new(2026, 3, 14, 15, 30, 0, DateTimeKind.Utc);

    private static BackupInfo Info(bool older = false, bool keyLost = false) =>
        new(_exportedAtUtc, "1.2.3", "20260101000000_Init", older, keyLost, 4096);

    private static Result Failure(string code, IReadOnlyDictionary<string, string[]>? details = null) =>
        details is null
            ? Result.Failure(code, @"C:\secret\path\data.db exploded")
            : Result.Failure(code, @"C:\secret\path\data.db exploded", details);

    private static IReadOnlyDictionary<string, string[]> NewerDetails() => new Dictionary<string, string[]>
    {
        [BackupErrorCodes.DetailBackupSchema] = ["29990101000000_Future"],
        [BackupErrorCodes.DetailAppSchema] = ["20260101000000_Init"],
        [BackupErrorCodes.DetailBackupAppVersion] = ["7.7.7"],
    };

    private static IReadOnlyDictionary<string, string[]> Reason(string reason) => new Dictionary<string, string[]>
    {
        [BackupErrorCodes.DetailReason] = [reason],
    };

    private static void UseCulture(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5);
        }

        Assert.True(condition(), "Condition was not reached in time.");
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Clock = new FakeClock { UtcNow = new DateTime(2026, 5, 6, 7, 8, 0, DateTimeKind.Utc) };
            ViewModel = new BackupSettingsViewModel(
                new CreateBackupUseCase(Service, NullLogger<CreateBackupUseCase>.Instance),
                new InspectBackupUseCase(Service, NullLogger<InspectBackupUseCase>.Instance),
                new RestoreBackupUseCase(Service, NullLogger<RestoreBackupUseCase>.Instance),
                SaveDialog,
                OpenDialog,
                Center,
                Restart,
                Clock,
                Logger,
                _appVersion);
        }

        public FakeBackupService Service { get; } = new();

        public FakeFileSaveDialog SaveDialog { get; } = new();

        public FakeFileOpenDialog OpenDialog { get; } = new();

        public FakeNotificationCenter Center { get; } = new();

        public FakeAppRestartService Restart { get; } = new();

        public FakeClock Clock { get; }

        public RecordingLogger<BackupSettingsViewModel> Logger { get; } = new();

        public BackupSettingsViewModel ViewModel { get; }

        /// <summary>Drives the restore flow to <see cref="BackupPanelState.ConfirmPending"/>.</summary>
        public async Task<OpenTargets.RecordingOpenTarget> ReachConfirmPendingAsync(BackupInfo? info = null)
        {
            OpenTargets.RecordingOpenTarget source = OpenTargets.Recording();
            OpenDialog.Result = source.Target;
            Service.InspectResult = Result.Success(info ?? Info());
            await ViewModel.ChooseRestoreFileCommand.ExecuteAsync(null);
            Assert.Equal(BackupPanelState.ConfirmPending, ViewModel.State);
            return source;
        }

        /// <summary>Drives the flow through a successful restore to <see cref="BackupPanelState.RestartRequired"/>.</summary>
        public async Task ReachRestartRequiredAsync(RestoreOutcome? outcome = null)
        {
            await ReachConfirmPendingAsync();
            Service.RestoreResult = Result.Success(outcome ?? new RestoreOutcome("pre-restore-x.zip", false, false, false));
            await ViewModel.ConfirmRestoreCommand.ExecuteAsync(null);
            Assert.Equal(BackupPanelState.RestartRequired, ViewModel.State);
        }
    }

    // ---------------------------------------------------------------- export

    [Fact]
    public async Task Export_Cancel_CallsNoUseCase_ShowsNoMessage_StaysIdle()
    {
        var f = new Fixture();
        f.SaveDialog.Result = null;

        await f.ViewModel.ExportCommand.ExecuteAsync(null);

        Assert.Equal(1, f.SaveDialog.CallCount);
        Assert.Equal(0, f.Service.CreateCalls);
        Assert.Equal(string.Empty, f.ViewModel.StatusMessage);
        Assert.False(f.ViewModel.IsError);
        Assert.Equal(BackupPanelState.Idle, f.ViewModel.State);
        Assert.False(f.ViewModel.IsBusy);
    }

    [Fact]
    public async Task Export_Success_ShowsFileName_AndPassesSuggestedNameFilterAndTitle()
    {
        UseCulture("es-DO");
        var f = new Fixture();
        using SaveTargets.RecordingSaveTarget target = SaveTargets.Recording();
        f.SaveDialog.Result = target;

        await f.ViewModel.ExportCommand.ExecuteAsync(null);

        Assert.Equal(1, f.Service.CreateCalls);
        Assert.Equal(BackupFileName.Create(f.Clock.UtcNow, TimeZoneInfo.Local), f.SaveDialog.LastSuggestedFileName);
        Assert.NotNull(f.SaveDialog.LastFilter);
        Assert.Equal("zip", f.SaveDialog.LastFilter!.Extension);
        Assert.Equal("application/zip", f.SaveDialog.LastFilter.MimeType);
        Assert.Equal(Strings.BackupFileTypeName, f.SaveDialog.LastFilter.DisplayName);
        Assert.Equal(Strings.BackupExportDialogTitle, f.SaveDialog.LastTitle);
        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, Strings.BackupExportSucceeded, "recorded.bin"),
            f.ViewModel.StatusMessage);
        Assert.False(f.ViewModel.IsError);
        Assert.Equal(BackupPanelState.Idle, f.ViewModel.State);
    }

    [Fact]
    public async Task Export_Failure_ShowsMappedLocalizedMessage_AndFlagsError()
    {
        UseCulture("es-DO");
        var f = new Fixture();
        using SaveTargets.RecordingSaveTarget target = SaveTargets.Recording();
        f.SaveDialog.Result = target;
        f.Service.CreateResult = Failure(BackupErrorCodes.IoError, Reason(BackupErrorCodes.ReasonInsufficientSpace));

        await f.ViewModel.ExportCommand.ExecuteAsync(null);

        Assert.Equal(Strings.BackupErrorInsufficientSpace, f.ViewModel.StatusMessage);
        Assert.True(f.ViewModel.IsError);
        Assert.DoesNotContain("secret", f.ViewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(BackupPanelState.Idle, f.ViewModel.State);
    }

    // --------------------------------------------------------- restore: pick

    [Fact]
    public async Task ChooseRestoreFile_Cancel_CallsNoInspect_ShowsNoMessage_StaysIdle()
    {
        var f = new Fixture();
        f.OpenDialog.Result = null;

        await f.ViewModel.ChooseRestoreFileCommand.ExecuteAsync(null);

        Assert.Equal(1, f.OpenDialog.Calls);
        Assert.Equal(0, f.Service.InspectCalls);
        Assert.Equal(string.Empty, f.ViewModel.StatusMessage);
        Assert.False(f.ViewModel.IsError);
        Assert.Equal(BackupPanelState.Idle, f.ViewModel.State);
        Assert.Null(f.ViewModel.PendingInfo);
    }

    [Fact]
    public async Task ChooseRestoreFile_InspectSuccess_ShowsConfirmation_WithoutRestoring()
    {
        UseCulture("es-DO");
        var f = new Fixture();
        BackupInfo info = Info();

        using OpenTargets.RecordingOpenTarget source = await f.ReachConfirmPendingAsync(info);

        Assert.Equal(1, f.Service.InspectCalls);
        Assert.Equal(0, f.Service.RestoreCalls);
        Assert.Equal(0, f.Center.SuspendCalls);
        Assert.Same(info, f.ViewModel.PendingInfo);
        Assert.True(f.ViewModel.IsConfirmVisible);
        Assert.Equal(0, source.DisposeCount);
        Assert.Equal(Strings.BackupRestoreDialogTitle, f.OpenDialog.LastTitle);
        Assert.Equal("zip", f.OpenDialog.LastFilter!.Extension);

        string date = _exportedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        Assert.Contains(date, f.ViewModel.PendingSummary, StringComparison.Ordinal);
        Assert.Contains("1.2.3", f.ViewModel.PendingSummary, StringComparison.Ordinal);
        Assert.DoesNotContain(Strings.BackupConfirmOlderSchemaNote, f.ViewModel.PendingSummary, StringComparison.Ordinal);
        Assert.DoesNotContain(Strings.BackupConfirmApiKeyLostNote, f.ViewModel.PendingSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChooseRestoreFile_OlderSchemaAndKeyLoss_AddTheirNotesToTheSummary()
    {
        UseCulture("en");
        var f = new Fixture();

        using OpenTargets.RecordingOpenTarget source =
            await f.ReachConfirmPendingAsync(Info(older: true, keyLost: true));

        Assert.Contains(Strings.BackupConfirmOlderSchemaNote, f.ViewModel.PendingSummary, StringComparison.Ordinal);
        Assert.Contains(Strings.BackupConfirmApiKeyLostNote, f.ViewModel.PendingSummary, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> InspectFailureCases()
    {
        foreach (string culture in new[] { "es-DO", "en" })
        {
            yield return [culture, "NotABackup"];
            yield return [culture, "Corrupt"];
            yield return [culture, "NewerSchema"];
            yield return [culture, "IoError"];
            yield return [culture, "InsufficientSpace"];
            yield return [culture, "Unknown"];
        }
    }

    [Theory]
    [MemberData(nameof(InspectFailureCases))]
    public async Task ChooseRestoreFile_InspectFailure_ShowsLocalizedMessage_NoPanel_DisposesSource(string culture, string caseId)
    {
        UseCulture(culture);
        var f = new Fixture();
        using OpenTargets.RecordingOpenTarget source = OpenTargets.Recording();
        f.OpenDialog.Result = source.Target;
        f.Service.InspectResult = caseId switch
        {
            "NotABackup" => Failure(BackupErrorCodes.NotABackup),
            "Corrupt" => Failure(BackupErrorCodes.Corrupt),
            "NewerSchema" => Failure(BackupErrorCodes.NewerSchema, NewerDetails()),
            "IoError" => Failure(BackupErrorCodes.IoError),
            "InsufficientSpace" => Failure(BackupErrorCodes.IoError, Reason(BackupErrorCodes.ReasonInsufficientSpace)),
            _ => Failure("SomethingBrandNew"),
        };

        await f.ViewModel.ChooseRestoreFileCommand.ExecuteAsync(null);

        string expected = caseId switch
        {
            "NotABackup" => Strings.BackupErrorNotABackup,
            "Corrupt" => Strings.BackupErrorCorrupt,
            "NewerSchema" => string.Format(
                CultureInfo.CurrentCulture,
                Strings.BackupErrorNewerSchema,
                "7.7.7",
                "29990101000000_Future",
                _appVersion,
                "20260101000000_Init"),
            "InsufficientSpace" => Strings.BackupErrorInsufficientSpace,
            _ => Strings.BackupErrorIoError,
        };
        Assert.Equal(expected, f.ViewModel.StatusMessage);
        Assert.True(f.ViewModel.IsError);
        Assert.False(f.ViewModel.IsConfirmVisible);
        Assert.Null(f.ViewModel.PendingInfo);
        Assert.Equal(BackupPanelState.Idle, f.ViewModel.State);
        Assert.Equal(1, source.DisposeCount);
        Assert.DoesNotContain("secret", f.ViewModel.StatusMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("exploded", f.ViewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NewerSchemaMessage_ContainsAllFourValues()
    {
        UseCulture("en");
        var f = new Fixture();
        using OpenTargets.RecordingOpenTarget source = OpenTargets.Recording();
        f.OpenDialog.Result = source.Target;
        f.Service.InspectResult = Failure(BackupErrorCodes.NewerSchema, NewerDetails());

        await f.ViewModel.ChooseRestoreFileCommand.ExecuteAsync(null);

        foreach (string value in new[] { "7.7.7", "29990101000000_Future", _appVersion, "20260101000000_Init" })
        {
            Assert.Contains(value, f.ViewModel.StatusMessage, StringComparison.Ordinal);
        }
    }

    // -------------------------------------------------- restore: cancel/confirm

    [Fact]
    public async Task CancelRestore_DisposesSource_ClearsPending_WritesNothing_ReturnsToIdle()
    {
        var f = new Fixture();
        using OpenTargets.RecordingOpenTarget source = await f.ReachConfirmPendingAsync();

        f.ViewModel.CancelRestoreCommand.Execute(null);

        Assert.Equal(1, source.DisposeCount);
        Assert.Null(f.ViewModel.PendingInfo);
        Assert.Equal(BackupPanelState.Idle, f.ViewModel.State);
        Assert.Equal(0, f.Service.RestoreCalls);
        Assert.Equal(0, f.Center.SuspendCalls);
        Assert.Equal(string.Empty, f.ViewModel.StatusMessage);
    }

    [Fact]
    public async Task ConfirmRestore_SuspendsBeforeRestore_AndKeepsSuspensionOnSuccess()
    {
        var f = new Fixture();
        using OpenTargets.RecordingOpenTarget source = await f.ReachConfirmPendingAsync();
        int suspendCallsWhenRestoreStarted = -1;
        f.Service.OnRestoreEntered = () => suspendCallsWhenRestoreStarted = f.Center.SuspendCalls;
        f.Service.RestoreResult = Result.Success(new RestoreOutcome("pre-restore-x.zip", false, false, false));

        await f.ViewModel.ConfirmRestoreCommand.ExecuteAsync(null);

        Assert.Equal(1, suspendCallsWhenRestoreStarted);
        Assert.Equal(1, f.Service.RestoreCalls);
        Assert.Equal(0, f.Center.SuspensionsDisposed);
        Assert.True(f.Center.IsSuspended);
        Assert.Equal(BackupPanelState.RestartRequired, f.ViewModel.State);
        Assert.Equal(1, source.DisposeCount);
    }

    [Fact]
    public async Task ConfirmRestore_Failure_DisposesSuspension_ShowsMappedError_ReturnsToIdle()
    {
        UseCulture("es-DO");
        var f = new Fixture();
        using OpenTargets.RecordingOpenTarget source = await f.ReachConfirmPendingAsync();
        f.Service.RestoreResult = Failure(BackupErrorCodes.IoError);

        await f.ViewModel.ConfirmRestoreCommand.ExecuteAsync(null);

        Assert.Equal(1, f.Center.SuspendCalls);
        Assert.Equal(1, f.Center.SuspensionsDisposed);
        Assert.False(f.Center.IsSuspended);
        Assert.Equal(Strings.BackupErrorIoError, f.ViewModel.StatusMessage);
        Assert.True(f.ViewModel.IsError);
        Assert.Equal(BackupPanelState.Idle, f.ViewModel.State);
        Assert.Null(f.ViewModel.PendingInfo);
        Assert.Equal(1, source.DisposeCount);
    }

    [Fact]
    public async Task ConfirmRestore_SuspendFails_NeverRestores_ShowsGenericError_AndLogs()
    {
        UseCulture("es-DO");
        var f = new Fixture();
        using OpenTargets.RecordingOpenTarget source = await f.ReachConfirmPendingAsync();
        f.Center.SuspendException = new InvalidOperationException("gate broken");

        await f.ViewModel.ConfirmRestoreCommand.ExecuteAsync(null);

        Assert.Equal(0, f.Service.RestoreCalls);
        Assert.Equal(Strings.BackupErrorIoError, f.ViewModel.StatusMessage);
        Assert.True(f.ViewModel.IsError);
        Assert.True(f.Logger.HasError);
        Assert.Equal(BackupPanelState.Idle, f.ViewModel.State);
        Assert.Equal(1, source.DisposeCount);
    }

    [Fact]
    public async Task ConfirmRestore_Success_ShowsSafetyFileName_AndKeyLostNotice()
    {
        UseCulture("es-DO");
        var f = new Fixture();

        await f.ReachRestartRequiredAsync(new RestoreOutcome("pre-restore-20260101-000000.zip", false, true, false));

        string expected = string.Format(CultureInfo.CurrentCulture, Strings.BackupRestoreSucceeded, "pre-restore-20260101-000000.zip");
        Assert.Contains(expected, f.ViewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Contains(Strings.BackupApiKeyNotRestoredNotice, f.ViewModel.StatusMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(Strings.BackupApiKeyKeptNotice, f.ViewModel.StatusMessage, StringComparison.Ordinal);
        Assert.False(f.ViewModel.IsError);
        Assert.True(f.ViewModel.IsRestartPanelVisible);
    }

    [Fact]
    public async Task ConfirmRestore_Success_WithCarriedKey_ShowsKeptNotice()
    {
        UseCulture("en");
        var f = new Fixture();

        await f.ReachRestartRequiredAsync(new RestoreOutcome("pre-restore-x.zip", true, false, false));

        Assert.Contains(Strings.BackupApiKeyKeptNotice, f.ViewModel.StatusMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(Strings.BackupApiKeyNotRestoredNotice, f.ViewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmRestore_Success_WithoutKeyFacts_ShowsNoKeyNotice()
    {
        UseCulture("en");
        var f = new Fixture();

        await f.ReachRestartRequiredAsync();

        Assert.DoesNotContain(Strings.BackupApiKeyKeptNotice, f.ViewModel.StatusMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(Strings.BackupApiKeyNotRestoredNotice, f.ViewModel.StatusMessage, StringComparison.Ordinal);
    }

    // --------------------------------------------------------------- restart

    [Fact]
    public async Task RestartNow_WhenRestartFails_ShowsFallback()
    {
        var f = new Fixture();
        await f.ReachRestartRequiredAsync();
        f.Restart.Result = false;

        f.ViewModel.RestartNowCommand.Execute(null);

        Assert.Equal(1, f.Restart.Calls);
        Assert.True(f.ViewModel.RestartFailed);
        Assert.Equal(BackupPanelState.RestartRequired, f.ViewModel.State);
    }

    [Fact]
    public async Task RestartNow_WhenRestartSucceeds_ShowsNoFallback()
    {
        var f = new Fixture();
        await f.ReachRestartRequiredAsync();
        f.Restart.Result = true;

        f.ViewModel.RestartNowCommand.Execute(null);

        Assert.Equal(1, f.Restart.Calls);
        Assert.False(f.ViewModel.RestartFailed);
    }

    [Fact]
    public void RestartNow_OutsideRestartRequired_DoesNothing()
    {
        var f = new Fixture();

        f.ViewModel.RestartNowCommand.Execute(null);

        Assert.Equal(0, f.Restart.Calls);
        Assert.False(f.ViewModel.RestartNowCommand.CanExecute(null));
    }

    [Fact]
    public async Task RestartRequired_IsTerminal_NoOtherBackupCommandCanRun()
    {
        var f = new Fixture();
        await f.ReachRestartRequiredAsync();
        int saveCalls = f.SaveDialog.CallCount;
        int openCalls = f.OpenDialog.Calls;
        int suspendCalls = f.Center.SuspendCalls;

        Assert.False(f.ViewModel.ExportCommand.CanExecute(null));
        Assert.False(f.ViewModel.ChooseRestoreFileCommand.CanExecute(null));
        Assert.False(f.ViewModel.ConfirmRestoreCommand.CanExecute(null));
        Assert.False(f.ViewModel.CancelRestoreCommand.CanExecute(null));

        // ExecuteAsync bypasses CanExecute, so the body guard must hold as well.
        await f.ViewModel.ExportCommand.ExecuteAsync(null);
        await f.ViewModel.ChooseRestoreFileCommand.ExecuteAsync(null);
        await f.ViewModel.ConfirmRestoreCommand.ExecuteAsync(null);

        Assert.Equal(saveCalls, f.SaveDialog.CallCount);
        Assert.Equal(openCalls, f.OpenDialog.Calls);
        Assert.Equal(suspendCalls, f.Center.SuspendCalls);
        Assert.Equal(1, f.Service.RestoreCalls);
        Assert.Equal(BackupPanelState.RestartRequired, f.ViewModel.State);
    }

    // ---------------------------------------------------------- double click

    [Fact]
    public async Task Export_DoubleInvocation_OpensOneDialogAndRunsOneUseCase()
    {
        var f = new Fixture();
        using SaveTargets.RecordingSaveTarget target = SaveTargets.Recording();
        var gate = new TaskCompletionSource();
        f.SaveDialog.Gate = gate.Task;
        f.SaveDialog.Result = target;

        Task first = f.ViewModel.ExportCommand.ExecuteAsync(null);
        Task second = f.ViewModel.ExportCommand.ExecuteAsync(null);
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, f.SaveDialog.CallCount);
        Assert.Equal(1, f.Service.CreateCalls);
        Assert.Equal(BackupPanelState.Idle, f.ViewModel.State);
    }

    [Fact]
    public async Task ChooseRestoreFile_DoubleInvocation_OpensOneDialog()
    {
        var f = new Fixture();
        var gate = new TaskCompletionSource();
        f.OpenDialog.Gate = gate;
        f.OpenDialog.Result = null;

        Task first = f.ViewModel.ChooseRestoreFileCommand.ExecuteAsync(null);
        Task second = f.ViewModel.ChooseRestoreFileCommand.ExecuteAsync(null);
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, f.OpenDialog.Calls);
    }

    [Fact]
    public async Task ConfirmRestore_DoubleInvocation_SuspendsAndRestoresOnce()
    {
        var f = new Fixture();
        using OpenTargets.RecordingOpenTarget source = await f.ReachConfirmPendingAsync();
        var gate = new TaskCompletionSource();
        f.Service.Gate = gate;
        f.Service.RestoreResult = Result.Success(new RestoreOutcome("pre-restore-x.zip", false, false, false));

        Task first = f.ViewModel.ConfirmRestoreCommand.ExecuteAsync(null);
        Task second = f.ViewModel.ConfirmRestoreCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => f.Service.RestoreCalls == 1);
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, f.Service.RestoreCalls);
        Assert.Equal(1, f.Center.SuspendCalls);
    }

    // ------------------------------------------------------- CanExecute / busy

    private static void AssertMatrix(BackupSettingsViewModel vm, bool export, bool choose, bool confirm, bool cancel, bool restart)
    {
        Assert.Equal(export, vm.ExportCommand.CanExecute(null));
        Assert.Equal(choose, vm.ChooseRestoreFileCommand.CanExecute(null));
        Assert.Equal(confirm, vm.ConfirmRestoreCommand.CanExecute(null));
        Assert.Equal(cancel, vm.CancelRestoreCommand.CanExecute(null));
        Assert.Equal(restart, vm.RestartNowCommand.CanExecute(null));
    }

    [Fact]
    public async Task CanExecuteMatrix_FollowsTheState_AndIsBusyOnlyWhileWorking()
    {
        var f = new Fixture();
        BackupSettingsViewModel vm = f.ViewModel;

        AssertMatrix(vm, export: true, choose: true, confirm: false, cancel: false, restart: false);
        Assert.False(vm.IsBusy);

        // Exporting (save dialog open).
        var exportGate = new TaskCompletionSource();
        f.SaveDialog.Gate = exportGate.Task;
        Task exporting = vm.ExportCommand.ExecuteAsync(null);
        Assert.Equal(BackupPanelState.Exporting, vm.State);
        Assert.True(vm.IsBusy);
        Assert.Equal(Strings.BackupWorkingExport, vm.WorkingMessage);
        AssertMatrix(vm, false, false, false, false, false);
        exportGate.SetResult();
        await exporting;
        Assert.False(vm.IsBusy);

        // Inspecting (open dialog open).
        var inspectGate = new TaskCompletionSource();
        f.OpenDialog.Gate = inspectGate;
        Task inspecting = vm.ChooseRestoreFileCommand.ExecuteAsync(null);
        Assert.Equal(BackupPanelState.Inspecting, vm.State);
        Assert.True(vm.IsBusy);
        Assert.Equal(Strings.BackupWorkingInspect, vm.WorkingMessage);
        AssertMatrix(vm, false, false, false, false, false);
        inspectGate.SetResult();
        await inspecting;
        Assert.False(vm.IsBusy);

        // ConfirmPending.
        f.OpenDialog.Gate = null;
        using OpenTargets.RecordingOpenTarget source = await f.ReachConfirmPendingAsync();
        AssertMatrix(vm, false, false, true, true, false);
        Assert.False(vm.IsBusy);

        // Restoring.
        var restoreGate = new TaskCompletionSource();
        f.Service.Gate = restoreGate;
        f.Service.RestoreResult = Result.Success(new RestoreOutcome("pre-restore-x.zip", false, false, false));
        Task restoring = vm.ConfirmRestoreCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => f.Service.RestoreCalls == 1);
        Assert.Equal(BackupPanelState.Restoring, vm.State);
        Assert.True(vm.IsBusy);
        Assert.Equal(Strings.BackupWorkingRestore, vm.WorkingMessage);
        AssertMatrix(vm, false, false, false, false, false);
        restoreGate.SetResult();
        await restoring;

        // RestartRequired.
        Assert.False(vm.IsBusy);
        AssertMatrix(vm, false, false, false, false, true);
    }

    [Fact]
    public async Task StateChange_RaisesCanExecuteChanged_OnEveryCommand()
    {
        var f = new Fixture();
        int raised = 0;
        f.ViewModel.ExportCommand.CanExecuteChanged += (_, _) => raised++;
        f.ViewModel.RestartNowCommand.CanExecuteChanged += (_, _) => raised++;
        f.SaveDialog.Result = null;

        await f.ViewModel.ExportCommand.ExecuteAsync(null);

        Assert.True(raised >= 2);
    }

    // ------------------------------------------------------ unexpected errors

    [Fact]
    public async Task Export_UnexpectedException_MapsToGenericError_Logs_AndReturnsToIdle()
    {
        UseCulture("es-DO");
        var f = new Fixture();
        f.SaveDialog.ThrowWith = new InvalidOperationException(@"C:\secret\dialog crashed");

        await f.ViewModel.ExportCommand.ExecuteAsync(null);

        Assert.Equal(Strings.BackupErrorIoError, f.ViewModel.StatusMessage);
        Assert.True(f.ViewModel.IsError);
        Assert.True(f.Logger.HasError);
        Assert.Equal(BackupPanelState.Idle, f.ViewModel.State);
        Assert.False(f.ViewModel.IsBusy);
    }

    [Fact]
    public async Task ChooseRestoreFile_UnexpectedException_MapsToGenericError_AndReturnsToIdle()
    {
        UseCulture("es-DO");
        var f = new Fixture();
        var gate = new TaskCompletionSource();
        gate.SetException(new InvalidOperationException("picker crashed"));
        f.OpenDialog.Gate = gate;

        await f.ViewModel.ChooseRestoreFileCommand.ExecuteAsync(null);

        Assert.Equal(Strings.BackupErrorIoError, f.ViewModel.StatusMessage);
        Assert.True(f.ViewModel.IsError);
        Assert.True(f.Logger.HasError);
        Assert.Equal(BackupPanelState.Idle, f.ViewModel.State);
    }

    [Fact]
    public async Task Export_OperationCanceled_ShowsNoFailureMessage_AndReturnsToIdle()
    {
        var f = new Fixture();
        f.SaveDialog.ThrowWith = new OperationCanceledException();

        await f.ViewModel.ExportCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, f.ViewModel.StatusMessage);
        Assert.False(f.ViewModel.IsError);
        Assert.Equal(BackupPanelState.Idle, f.ViewModel.State);
    }

    [Fact]
    public async Task ConfirmRestore_OperationCanceled_DisposesSuspension_ShowsNoFailureMessage()
    {
        var f = new Fixture();
        using OpenTargets.RecordingOpenTarget source = await f.ReachConfirmPendingAsync();
        f.Service.RestoreException = new OperationCanceledException();

        await f.ViewModel.ConfirmRestoreCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, f.ViewModel.StatusMessage);
        Assert.False(f.ViewModel.IsError);
        Assert.Equal(1, f.Center.SuspensionsDisposed);
        Assert.Equal(BackupPanelState.Idle, f.ViewModel.State);
    }

    [Fact]
    public async Task ANewOperation_ClearsThePreviousStatusAndError()
    {
        UseCulture("en");
        var f = new Fixture();
        using SaveTargets.RecordingSaveTarget target = SaveTargets.Recording();
        f.SaveDialog.Result = target;
        f.Service.CreateResult = Failure(BackupErrorCodes.IoError);
        await f.ViewModel.ExportCommand.ExecuteAsync(null);
        Assert.True(f.ViewModel.IsError);

        f.SaveDialog.Result = null;
        await f.ViewModel.ExportCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, f.ViewModel.StatusMessage);
        Assert.False(f.ViewModel.IsError);
    }

    // -------------------------------------------------------- error mapping

    private static IEnumerable<string> ErrorCodes() => typeof(BackupErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral
            && !field.Name.StartsWith("Detail", StringComparison.Ordinal)
            && !field.Name.StartsWith("Reason", StringComparison.Ordinal))
        .Select(field => (string)field.GetRawConstantValue()!);

    private static IEnumerable<string> ErrorReasons() => typeof(BackupErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.Name.StartsWith("Reason", StringComparison.Ordinal))
        .Select(field => (string)field.GetRawConstantValue()!);

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public void ErrorMessage_MapsEveryErrorCodeAndReason_ToANonBlankLocalizedMessage(string culture)
    {
        UseCulture(culture);
        var f = new Fixture();

        // Guard: adding a code or reason to BackupErrorCodes forces a decision here.
        Assert.Equal(
            new[] { "Corrupt", "IoError", "NewerSchema", "NotABackup" },
            ErrorCodes().OrderBy(code => code, StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "insufficientSpace" }, ErrorReasons().ToArray());

        var expected = new Dictionary<string, string>
        {
            [BackupErrorCodes.NotABackup] = Strings.BackupErrorNotABackup,
            [BackupErrorCodes.Corrupt] = Strings.BackupErrorCorrupt,
            [BackupErrorCodes.IoError] = Strings.BackupErrorIoError,
            [BackupErrorCodes.NewerSchema] = string.Format(
                CultureInfo.CurrentCulture, Strings.BackupErrorNewerSchema, "7.7.7", "29990101000000_Future", _appVersion, "20260101000000_Init"),
        };

        foreach (string code in ErrorCodes())
        {
            string message = f.ViewModel.ErrorMessage(new Error(code, "raw", NewerDetails()));
            Assert.False(string.IsNullOrWhiteSpace(message), $"Code {code} has no message.");
            Assert.Equal(expected[code], message);
        }

        foreach (string reason in ErrorReasons())
        {
            string message = f.ViewModel.ErrorMessage(new Error(BackupErrorCodes.IoError, "raw", Reason(reason)));
            Assert.False(string.IsNullOrWhiteSpace(message), $"Reason {reason} has no message.");
            Assert.Equal(Strings.BackupErrorInsufficientSpace, message);
        }
    }

    [Fact]
    public void ErrorMessage_UnknownCodeUnknownReasonOrNull_FallsBackToTheGenericMessage()
    {
        UseCulture("en");
        var f = new Fixture();

        Assert.Equal(Strings.BackupErrorIoError, f.ViewModel.ErrorMessage(null));
        Assert.Equal(Strings.BackupErrorIoError, f.ViewModel.ErrorMessage(new Error("Nope", "raw")));
        Assert.Equal(Strings.BackupErrorIoError, f.ViewModel.ErrorMessage(new Error(BackupErrorCodes.IoError, "raw", Reason("weird"))));
    }

    [Fact]
    public void ErrorMessage_NewerSchemaWithMissingDetails_StillReturnsAMessage()
    {
        UseCulture("en");
        var f = new Fixture();

        string message = f.ViewModel.ErrorMessage(new Error(BackupErrorCodes.NewerSchema, "raw"));

        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.Contains(_appVersion, message, StringComparison.Ordinal);
    }
}
