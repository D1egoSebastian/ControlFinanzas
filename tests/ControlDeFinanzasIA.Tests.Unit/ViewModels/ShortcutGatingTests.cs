using ControlDeFinanzasIA.Application.UseCases.Backup.CreateBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.InspectBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.RestoreBackup;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotificationSettings;
using ControlDeFinanzasIA.Application.UseCases.Notifications.UpdateNotificationSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.GetAppSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.TestAiConnection;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateAiSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateLocale;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateTheme;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.Tests.Unit.UI;
using ControlDeFinanzasIA.UI.Input;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.ViewModels;

/// <summary>
/// F-021 S7a (R-13, S-29, S-30, T9): the gate of the shell shortcut commands over REAL view-models. The gate lives in
/// the command bodies (Phase 18/19 lesson): every test also proves the command is still executable while gated and
/// that the same command acts once the gate is released, so a no-op can never be a vacuous pass.
/// </summary>
public sealed class ShortcutGatingTests
{
    private sealed class ProbeViewModel : ViewModelBase
    {
    }

    private sealed class Rig
    {
        public Rig(ViewModelBase? current = null)
        {
            Income = IncomeViewModelTests.CreateViewModel();
            ImportRig = new ExpensesViewModelImportTests.Rig();
            Settings = CreateSettings(out BackupSettingsViewModel backup);
            Backup = backup;

            var routes = NavigationSections.All.ToDictionary(
                s => s.Route,
                _ => (ViewModelBase)new ProbeViewModel(),
                StringComparer.Ordinal);
            routes[RouteNames.Expenses] = ImportRig.ViewModel;
            routes[RouteNames.Incomes] = Income;
            routes[RouteNames.Settings] = Settings;
            Navigation = new FakeNavigationService(routes) { CurrentViewModel = current ?? new ProbeViewModel() };
            Overlay = new ErrorOverlayViewModel(new FakeClipboardService(), "1.0.0");
            Shell = new ShellViewModel(
                Navigation,
                new FakeStringLocalizer<Strings>(),
                new NotificationListViewModel(
                    new FakeNotificationCenter(),
                    new FakeClock { Today = new DateOnly(2026, 9, 20) },
                    NullLogger<NotificationListViewModel>.Instance),
                Overlay);
        }

        public IncomeViewModel Income { get; }

        public ExpensesViewModelImportTests.Rig ImportRig { get; }

        public ExpensesViewModel Expenses => ImportRig.ViewModel;

        public SettingsViewModel Settings { get; }

        public BackupSettingsViewModel Backup { get; }

        public FakeNavigationService Navigation { get; }

        public ErrorOverlayViewModel Overlay { get; }

        public ShellViewModel Shell { get; }

        public int Navigations => Navigation.NavigateCalls.Count + Navigation.NavigateToTypedCalls.Count;

        /// <summary>Runs the four gated commands once; returns how many navigations or new-entry requests happened.</summary>
        public int RunTheFourGatedCommands()
        {
            int before = Navigations;
            FocusRequestToken? expensesBefore = Expenses.DescriptionFocusRequest;
            FocusRequestToken? incomeBefore = Income.DescriptionFocusRequest;

            Shell.NewExpenseCommand.Execute(null);
            Shell.NewIncomeCommand.Execute(null);
            Shell.GoToSectionCommand.Execute(3);
            Shell.OpenSettingsCommand.Execute(null);

            int effects = Navigations - before;
            if (!ReferenceEquals(expensesBefore, Expenses.DescriptionFocusRequest))
            {
                effects++;
            }

            if (!ReferenceEquals(incomeBefore, Income.DescriptionFocusRequest))
            {
                effects++;
            }

            return effects;
        }

        public void AssertTheFourCommandsStayExecutable()
        {
            Assert.True(Shell.NewExpenseCommand.CanExecute(null));
            Assert.True(Shell.NewIncomeCommand.CanExecute(null));
            Assert.True(Shell.GoToSectionCommand.CanExecute(3));
            Assert.True(Shell.OpenSettingsCommand.CanExecute(null));
        }

        private static SettingsViewModel CreateSettings(out BackupSettingsViewModel backup)
        {
            var backupService = new FakeBackupService();
            backup = new BackupSettingsViewModel(
                new CreateBackupUseCase(backupService, NullLogger<CreateBackupUseCase>.Instance),
                new InspectBackupUseCase(backupService, NullLogger<InspectBackupUseCase>.Instance),
                new RestoreBackupUseCase(backupService, NullLogger<RestoreBackupUseCase>.Instance),
                new FakeFileSaveDialog(),
                new FakeFileOpenDialog(),
                new FakeNotificationCenter(),
                new FakeAppRestartService(),
                new FakeClock(),
                NullLogger<BackupSettingsViewModel>.Instance);
            var store = new FakeSettingsStore();
            return new SettingsViewModel(
                new GetAppSettingsQuery(store),
                new UpdateThemeUseCase(store, NullLogger<UpdateThemeUseCase>.Instance),
                new UpdateLocaleUseCase(store, NullLogger<UpdateLocaleUseCase>.Instance),
                new UpdateAiSettingsUseCase(store, new UpdateAiSettingsValidator(), NullLogger<UpdateAiSettingsUseCase>.Instance),
                new TestAiConnectionUseCase(store, new FakeLlmClientFactory(), NullLogger<TestAiConnectionUseCase>.Instance),
                new GetNotificationSettingsQuery(store),
                new UpdateNotificationSettingsUseCase(store),
                new FakeNotificationCenter(),
                NullLogger<SettingsViewModel>.Instance,
                backup);
        }
    }

    [Fact]
    public void TheIBusyAwareFlags_ReadTheExistingImportAndRestoreState()
    {
        var rig = new Rig();
        IBusyAware expenses = rig.Expenses;
        IBusyAware settings = rig.Settings;

        Assert.False(expenses.IsBusy);
        Assert.False(settings.IsBusy);

        rig.ImportRig.Import.State = ImportPanelState.Importing;
        rig.Backup.State = BackupPanelState.Restoring;
        Assert.True(expenses.IsBusy);
        Assert.True(settings.IsBusy);

        rig.ImportRig.Import.State = ImportPanelState.Loading;
        Assert.True(expenses.IsBusy);

        rig.ImportRig.Import.State = ImportPanelState.Idle;
        rig.Backup.State = BackupPanelState.ConfirmPending;
        Assert.False(expenses.IsBusy);
        Assert.False(settings.IsBusy);
    }

    [Fact]
    public void WithTheErrorOverlayOpen_TheFourCommandsDoNothing_AndEscapeClosesTheOverlay()
    {
        var rig = new Rig(current: null);
        Assert.True(rig.RunTheFourGatedCommands() > 0, "Control: the commands act while the overlay is closed.");

        var gated = new Rig(current: null);
        gated.Overlay.Present(Guid.NewGuid(), DateTime.UtcNow);

        int effects = gated.RunTheFourGatedCommands();

        Assert.Equal(0, effects);
        gated.AssertTheFourCommandsStayExecutable();
        Assert.True(gated.Overlay.IsOpen);

        gated.Shell.EscapeCommand.Execute(null);

        Assert.False(gated.Overlay.IsOpen);
    }

    [Fact]
    public void WithAnOverlayOpenOnExpenses_NewExpenseLeavesTheFormUntouched()
    {
        var rig = new Rig();
        rig.Navigation.CurrentViewModel = rig.Expenses;
        rig.Shell.NewExpenseCommand.Execute(null);
        Assert.NotNull(rig.Expenses.DescriptionFocusRequest);

        var gated = new Rig();
        gated.Navigation.CurrentViewModel = gated.Expenses;
        gated.Overlay.Present(Guid.NewGuid(), DateTime.UtcNow);
        gated.Shell.NewExpenseCommand.Execute(null);

        Assert.Null(gated.Expenses.DescriptionFocusRequest);
    }

    [Fact]
    public void WhileAnImportCommitRuns_TheFourCommandsAreNoOps_AndTheImportIsUninterrupted()
    {
        var control = new Rig();
        control.Navigation.CurrentViewModel = control.Expenses;
        Assert.True(control.RunTheFourGatedCommands() > 0);

        var rig = new Rig();
        rig.Navigation.CurrentViewModel = rig.Expenses;
        rig.ImportRig.Import.State = ImportPanelState.Importing;

        int effects = rig.RunTheFourGatedCommands();

        Assert.Equal(0, effects);
        rig.AssertTheFourCommandsStayExecutable();
        Assert.Equal(ImportPanelState.Importing, rig.ImportRig.Import.State);
        Assert.Same(rig.Expenses, rig.Navigation.CurrentViewModel);
    }

    [Fact]
    public void WhileARestoreRuns_TheFourCommandsAreNoOps_AndTheRestoreIsUninterrupted()
    {
        var control = new Rig();
        control.Navigation.CurrentViewModel = control.Settings;
        Assert.True(control.RunTheFourGatedCommands() > 0);

        var rig = new Rig();
        rig.Navigation.CurrentViewModel = rig.Settings;
        rig.Backup.State = BackupPanelState.Restoring;

        int effects = rig.RunTheFourGatedCommands();

        Assert.Equal(0, effects);
        rig.AssertTheFourCommandsStayExecutable();
        Assert.Equal(BackupPanelState.Restoring, rig.Backup.State);
        Assert.Same(rig.Settings, rig.Navigation.CurrentViewModel);
    }

    [Fact]
    public void OverlayOverRestore_EscapeStillClosesTheOverlay()
    {
        var rig = new Rig();
        rig.Navigation.CurrentViewModel = rig.Settings;
        rig.Backup.State = BackupPanelState.Restoring;
        rig.Overlay.Present(Guid.NewGuid(), DateTime.UtcNow);

        rig.Shell.EscapeCommand.Execute(null);

        Assert.False(rig.Overlay.IsOpen);
        Assert.Equal(BackupPanelState.Restoring, rig.Backup.State);
    }

    [Fact]
    public void NoOverlayAndARestoreBusy_EscapeDoesNotCloseThePopup_ButTheBellToggleStaysAllowed()
    {
        var rig = new Rig();
        rig.Navigation.CurrentViewModel = rig.Settings;
        rig.Backup.State = BackupPanelState.Restoring;
        rig.Shell.Notifications.IsOpen = true;

        rig.Shell.EscapeCommand.Execute(null);
        Assert.True(rig.Shell.Notifications.IsOpen);

        rig.Shell.ToggleNotificationsCommand.Execute(null);
        Assert.False(rig.Shell.Notifications.IsOpen);

        rig.Shell.ToggleNotificationsCommand.Execute(null);
        Assert.True(rig.Shell.Notifications.IsOpen);

        // Released gate: Escape closes the popup again (control).
        rig.Backup.State = BackupPanelState.Idle;
        rig.Shell.EscapeCommand.Execute(null);
        Assert.False(rig.Shell.Notifications.IsOpen);
    }

    [Fact]
    public void WithTheOverlayOpen_TheBellToggleIsAlsoSuppressed()
    {
        var rig = new Rig();
        rig.Overlay.Present(Guid.NewGuid(), DateTime.UtcNow);

        rig.Shell.ToggleNotificationsCommand.Execute(null);

        Assert.False(rig.Shell.Notifications.IsOpen);
    }
}
