using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.UI;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>Entry point of the headless Avalonia session: the REAL <see cref="App"/> resources and styles, no window system.</summary>
public static class ImportHeadlessEntry
{
    /// <summary>Builds the application for the headless session.</summary>
    /// <returns>The configured builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}

/// <summary>
/// Headless Avalonia checks of the REAL <see cref="ImportView"/> and <see cref="ExpensesView"/> over the REAL view-models
/// (F-020, S7). They prove what the string scans in <see cref="ImportViewXamlTests"/> cannot: that the
/// <c>{ReflectionBinding}</c> ComboBoxes resolve and write back through generic option choices, that the buttons are
/// actually visible per state, and that the host "Close" button reaches an idle panel left by a failed load. The
/// native file dialog is NOT exercised (it is a platform dialog).
/// </summary>
public sealed class ImportViewHeadlessTests
{
    private static readonly Lazy<HeadlessUnitTestSession> _session =
        new(() => HeadlessUnitTestSession.StartNew(typeof(ImportHeadlessEntry)));

    private static Task<T> OnUiThread<T>(Func<Task<T>> body) =>
        _session.Value.Dispatch(body, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(60));

    private static void Flush() => Dispatcher.UIThread.RunJobs();

    private static Window Show(Control content, object dataContext)
    {
        var window = new Window { Width = 1100, Height = 900, Content = content, DataContext = dataContext };
        window.Show();
        Flush();
        return window;
    }

    private static ComboBox ComboNamed(Control root, string name) =>
        root.GetVisualDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == name);

    private static Button ButtonWithContent(Control root, string content) =>
        root.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, content));

    [Fact]
    public async Task ImportView_Previewed_OptionAndMappingComboBoxesResolveAndWriteBack()
    {
        bool ok = await OnUiThread(async () =>
        {
            var rig = new ExpensesViewModelImportTests.Rig();
            var view = new ImportView();
            using var _ = new DisposableWindow(Show(view, rig.Import));

            await rig.Import.ChooseFileCommand.ExecuteAsync(null);
            Flush();

            Assert.Equal(ImportPanelState.Previewed, rig.Import.State);

            // Six option pickers (delimiter shows for CSV); the seven mapping pickers are collapsed while previewed.
            Assert.Equal(6, view.GetVisualDescendants().OfType<ComboBox>().Count(c => c.IsEffectivelyVisible));

            ComboBox decimalStyle = ComboNamed(view, Strings.ImportDecimalStyleLabel);
            Assert.Equal(DecimalStyle.Auto, decimalStyle.SelectedValue);
            ImportOptionChoice<DecimalStyle> selected = Assert.IsType<ImportOptionChoice<DecimalStyle>>(decimalStyle.SelectedItem);
            Assert.False(string.IsNullOrWhiteSpace(selected.Label));
            Assert.NotNull(decimalStyle.DisplayMemberBinding);
            Assert.Equal(3, decimalStyle.ItemCount);

            // Write-back through the generic ReflectionBinding: picking a choice updates the view-model.
            decimalStyle.SelectedValue = DecimalStyle.Comma;
            Flush();
            Assert.Equal(DecimalStyle.Comma, rig.Import.DecimalStyle);

            // The other way round: the view-model drives the picker.
            rig.Import.DefaultCurrency = Currency.USD;
            Flush();
            Assert.Equal(Currency.USD, ComboNamed(view, Strings.ImportDefaultCurrencyLabel).SelectedValue);

            ComboBox dateFormat = ComboNamed(view, Strings.ImportDateFormatLabel);
            Assert.Equal(rig.Import.DateFormat, dateFormat.SelectedValue);

            // Expanding the mapping shows the seven column pickers bound to the ColumnChoice items.
            rig.Import.ToggleMappingCommand.Execute(null);
            Flush();
            ComboBox date = ComboNamed(view, Strings.ImportFieldDate);
            Assert.True(date.IsEffectivelyVisible);
            Assert.Same(rig.Import.SelectedDateColumn, date.SelectedItem);
            Assert.Equal("Date", rig.Import.SelectedDateColumn!.Label);
            Assert.Equal(13, view.GetVisualDescendants().OfType<ComboBox>().Count(c => c.IsEffectivelyVisible));

            date.SelectedItem = rig.Import.ColumnChoices[0];
            Flush();
            Assert.Null(rig.Import.SelectedDateColumn!.Index);
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task ImportView_ButtonsFollowTheState_FromLoadingToDone()
    {
        bool ok = await OnUiThread(async () =>
        {
            var rig = new ExpensesViewModelImportTests.Rig();
            var view = new ImportView();
            using var _ = new DisposableWindow(Show(view, rig.Import));
            string cancel = Strings.ImportCancelButton;
            string confirm = Strings.ImportConfirmButton;
            string close = Strings.ImportCloseButton;

            // Idle, nothing loaded: no action buttons at all.
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible);

            // Loading before any preview exists: Cancel must already be reachable.
            var gate = new TaskCompletionSource();
            rig.Dialog.Gate = gate;
            Task pending = rig.Import.ChooseFileCommand.ExecuteAsync(null);
            Flush();
            Assert.Equal(ImportPanelState.Loading, rig.Import.State);
            Assert.True(ButtonWithContent(view, cancel).IsEffectivelyVisible);
            Assert.True(view.GetVisualDescendants().OfType<ProgressBar>().Single().IsEffectivelyVisible);
            gate.SetResult();
            await pending;
            Flush();

            // Previewed: Cancel and Confirm visible and enabled, Close hidden.
            Assert.Equal(ImportPanelState.Previewed, rig.Import.State);
            Assert.True(ButtonWithContent(view, cancel).IsEffectivelyEnabled);
            Assert.True(ButtonWithContent(view, confirm).IsEffectivelyVisible);
            Assert.True(ButtonWithContent(view, confirm).IsEffectivelyEnabled);
            Assert.False(ButtonWithContent(view, close).IsEffectivelyVisible);
            Assert.False(view.GetVisualDescendants().OfType<ProgressBar>().Single().IsEffectivelyVisible);

            await rig.Import.ConfirmCommand.ExecuteAsync(null);
            Flush();

            // Done: only Close.
            Assert.Equal(ImportPanelState.Done, rig.Import.State);
            Assert.True(ButtonWithContent(view, close).IsEffectivelyVisible);
            Assert.False(ButtonWithContent(view, cancel).IsEffectivelyVisible);
            Assert.False(ButtonWithContent(view, confirm).IsEffectivelyVisible);
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task ExpensesView_ImportButtonOpensThePanel_AndAFailedLoadCanBeRetriedOrClosed()
    {
        bool ok = await OnUiThread(async () =>
        {
            var rig = new ExpensesViewModelImportTests.Rig();
            var view = new ExpensesView();
            using var _ = new DisposableWindow(Show(view, rig.ViewModel));

            Button import = ButtonWithContent(view, Strings.ImportButton);
            Assert.True(import.IsEffectivelyVisible);
            Assert.Same(rig.ViewModel.OpenImportCommand, import.Command);
            ImportView panel = view.GetVisualDescendants().OfType<ImportView>().Single();
            Assert.False(panel.IsEffectivelyVisible);

            // A picked file that cannot be read leaves the panel idle with an error.
            rig.Dialog.Result = new OpenTarget("broken.csv", _ => throw new IOException("unreadable"));
            await rig.ViewModel.OpenImportCommand.ExecuteAsync(null);
            Flush();

            Assert.True(rig.ViewModel.IsImportOpen);
            Assert.Equal(ImportPanelState.Idle, rig.Import.State);
            Assert.True(rig.Import.IsError);
            Assert.True(panel.IsEffectivelyVisible);
            Assert.False(rig.ViewModel.OpenImportCommand.CanExecute(null));

            // The host Close button dismisses the idle panel; "Choose another file" is offered in place.
            Button hostClose = view.GetVisualDescendants().OfType<Button>()
                .Single(b => Equals(b.Content, Strings.ImportCloseButton) && Equals(b.Command, rig.ViewModel.CloseImportCommand));
            Assert.True(hostClose.IsEffectivelyVisible);
            Button retry = ButtonWithContent(panel, Strings.ImportChooseAnotherButton);
            Assert.True(retry.IsEffectivelyVisible);
            Assert.Same(rig.Import.ChooseFileCommand, retry.Command);

            rig.Dialog.Result = OpenTargets_Recording();
            await rig.Import.ChooseFileCommand.ExecuteAsync(null);
            Flush();
            Assert.Equal(ImportPanelState.Previewed, rig.Import.State);
            Assert.False(retry.IsEffectivelyVisible);

            hostClose.Command!.Execute(null);
            Flush();
            Assert.False(rig.ViewModel.IsImportOpen);
            Assert.False(panel.IsEffectivelyVisible);
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task ExpensesView_WithARow_RendersItAndResolvesTheEditCommand()
    {
        bool ok = await OnUiThread(async () =>
        {
            var rig = new ExpensesViewModelImportTests.Rig();
            var expense = Expense.Create(Guid.NewGuid(), new DateOnly(2026, 3, 1), Money.Create(10m, Currency.DOP), "Uno", null, new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc));
            rig.Expenses.SetFilterResult([expense], 1);
            var view = new ExpensesView();
            using var _ = new DisposableWindow(Show(view, rig.ViewModel));

            await rig.ViewModel.ApplyFilterCommand.ExecuteAsync(null);
            Flush();

            Assert.Single(rig.ViewModel.Expenses);
            Button edit = ButtonWithContent(view, "Editar");
            Assert.True(edit.IsEffectivelyVisible);
            Assert.Same(rig.ViewModel.BeginEditCommand, edit.Command);
            return true;
        });

        Assert.True(ok);
    }

    private static OpenTarget OpenTargets_Recording() =>
        TestDoubles.OpenTargets.Recording([1, 2, 3]).Target;

    private sealed class DisposableWindow(Window window) : IDisposable
    {
        public void Dispose() => window.Close();
    }
}
