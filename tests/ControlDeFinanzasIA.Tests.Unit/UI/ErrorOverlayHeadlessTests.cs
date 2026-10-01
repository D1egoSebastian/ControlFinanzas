using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S4 (R-10, R-11, S-19, S-21, S-22, T5, T8): the REAL <see cref="MainWindow"/> with the REAL
/// <see cref="ShellViewModel"/> and the REAL <see cref="ErrorOverlayViewModel"/> in the shared headless session.
/// Only the collaborators around them (navigation, notification center, clipboard, repositories) are doubles.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class ErrorOverlayHeadlessTests
{
    private static readonly Guid _firstId = Guid.Parse("3f9a21c7-0b5d-4e0a-9d1e-7c2b8a4f6e10");
    private static readonly Guid _secondId = Guid.Parse("a1b2c3d4-0000-4000-8000-00000000abcd");
    private static readonly DateTime _at = new(2026, 9, 30, 14, 3, 11, DateTimeKind.Utc);

    private static void Settle()
    {
        HeadlessUi.Flush();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
        HeadlessUi.Flush();
    }

    private static Task<T> OnUi<T>(string culture, Func<Rig, T> body) =>
        HeadlessUi.OnUiThread(() =>
        {
            CultureInfo previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            try
            {
                using var rig = new Rig();
                return Task.FromResult(body(rig));
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        });

    private static Button ButtonNamed(Control root, string caption) =>
        root.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, caption));

    private static string[] Texts(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty).ToArray();

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public async Task AReportedError_ShowsTheOverlayOverTheShell_WithTheLocalizedTitleAndTheShortId(string culture)
    {
        (bool Hidden, bool Shown, string[] Texts, string Title, string Hint, string Close, bool Enabled) result =
            await OnUi(culture, rig =>
            {
                bool hidden = !rig.Overlay.IsEffectivelyVisible;

                rig.Vm.Present(_firstId, _at);
                Settle();

                return (
                    hidden,
                    rig.Overlay.IsEffectivelyVisible,
                    Texts(rig.Overlay),
                    Strings.ErrorOverlayTitle,
                    Strings.ErrorOverlayRestartHint,
                    Strings.ErrorOverlayClose,
                    rig.Content.IsEffectivelyEnabled);
            });

        Assert.True(result.Hidden);
        Assert.True(result.Shown);
        Assert.Contains(result.Title, result.Texts);
        Assert.Contains(result.Hint, result.Texts);
        Assert.Contains("3f9a21c7", string.Join(' ', result.Texts), StringComparison.OrdinalIgnoreCase);
        Assert.False(result.Enabled);
        Assert.Equal(culture == "en" ? "Something went wrong" : "Algo salió mal", result.Title);
    }

    [Fact]
    public async Task TheCloseButtonHasFocusOnOpen_AndTabCyclesInsideTheCardOnly()
    {
        (bool CloseFocused, List<bool> Inside, bool VisitedCopy, bool VisitedClose) result = await OnUi("en", rig =>
        {
            rig.Vm.Present(_firstId, _at);
            Settle();
            Button close = ButtonNamed(rig.Overlay, Strings.ErrorOverlayClose);
            Button copy = ButtonNamed(rig.Overlay, Strings.ErrorOverlayCopy);
            bool closeFocused = close.IsFocused;

            var inside = new List<bool>();
            bool visitedCopy = false;
            bool visitedClose = false;
            for (int i = 0; i < 6; i++)
            {
                rig.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                Settle();
                IInputElement? focused = rig.Window.FocusManager!.GetFocusedElement();
                inside.Add(focused is Visual v && rig.Overlay.IsVisualAncestorOf(v));
                visitedCopy |= ReferenceEquals(focused, copy);
                visitedClose |= ReferenceEquals(focused, close);
            }

            return (closeFocused, inside, visitedCopy, visitedClose);
        });

        Assert.True(result.CloseFocused);
        Assert.Equal(6, result.Inside.Count);
        Assert.All(result.Inside, Assert.True);
        Assert.True(result.VisitedCopy);
        Assert.True(result.VisitedClose);
    }

    [Fact]
    public async Task EscapeAndTheCloseButtonDismissTheOverlay_AndTheShellBecomesInteractiveAgain()
    {
        (bool Open1, bool Enabled1, bool Open2, bool Visible2, bool Enabled2, bool Open3, bool Open4) result =
            await OnUi("en", rig =>
            {
                rig.Vm.Present(_firstId, _at);
                Settle();
                bool open1 = rig.Overlay.IsEffectivelyVisible;
                bool enabled1 = rig.Content.IsEffectivelyEnabled;

                rig.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                Settle();
                bool open2 = rig.Vm.IsOpen;
                bool visible2 = rig.Overlay.IsEffectivelyVisible;
                bool enabled2 = rig.Content.IsEffectivelyEnabled;

                rig.Vm.Present(_secondId, _at);
                Settle();
                bool open3 = rig.Vm.IsOpen && rig.Overlay.IsEffectivelyVisible;
                ButtonNamed(rig.Overlay, Strings.ErrorOverlayClose).Focus();
                rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Settle();
                bool open4 = rig.Vm.IsOpen;

                return (open1, enabled1, open2, visible2, enabled2, open3, open4);
            });

        Assert.True(result.Open1);
        Assert.False(result.Enabled1);
        Assert.False(result.Open2);
        Assert.False(result.Visible2);
        Assert.True(result.Enabled2);
        Assert.True(result.Open3);
        Assert.False(result.Open4);
    }

    [Theory]
    [InlineData("button")]
    [InlineData("escape")]
    public async Task ClosingTheOverlay_RestoresFocusToTheControlThatHadItBeforeItOpened(string how)
    {
        // S7b finding: the overlay moved focus to Close on open, then left it on a hidden button after closing.
        (bool BeforeOpen, bool OnClose, bool Restored, bool OverlayGone) result = await OnUi("en", rig =>
        {
            TextBox description = rig.Window.GetVisualDescendants().OfType<TextBox>()
                .Single(t => t.Watermark == Strings.ExpensesDescriptionPlaceholder);
            description.Focus();
            Settle();
            bool before = ReferenceEquals(rig.Window.FocusManager!.GetFocusedElement(), description);

            rig.Vm.Present(_firstId, _at);
            Settle();
            bool onClose = ButtonNamed(rig.Overlay, Strings.ErrorOverlayClose).IsFocused;

            if (how == "escape")
            {
                rig.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            }
            else
            {
                ButtonNamed(rig.Overlay, Strings.ErrorOverlayClose).Command!.Execute(null);
            }

            Settle();
            return (before, onClose, ReferenceEquals(rig.Window.FocusManager!.GetFocusedElement(), description), !rig.Overlay.IsEffectivelyVisible);
        });

        Assert.True(result.BeforeOpen);
        Assert.True(result.OnClose);
        Assert.True(result.OverlayGone);
        Assert.True(result.Restored, "Focus must return to the control that had it before the overlay opened.");
    }

    [Fact]
    public async Task ClosingTheOverlay_WhenTheOldFocusIsGone_LeavesFocusAloneWithoutThrowing()
    {
        (bool Closed, bool Hidden) result = await OnUi("en", rig =>
        {
            TextBox description = rig.Window.GetVisualDescendants().OfType<TextBox>()
                .Single(t => t.Watermark == Strings.ExpensesDescriptionPlaceholder);
            description.Focus();
            Settle();
            rig.Vm.Present(_firstId, _at);
            Settle();
            description.IsVisible = false;
            Settle();

            rig.Vm.Close();
            Settle();
            return (!rig.Vm.IsOpen, !rig.Overlay.IsEffectivelyVisible);
        });

        Assert.True(result.Closed);
        Assert.True(result.Hidden);
    }

    [Fact]
    public async Task CopyUsesTheClipboard_AndShowsTheCopiedCaption()
    {
        (string? Text, string[] Texts, string Copied) result = await OnUi("es-DO", rig =>
        {
            rig.Vm.Present(_firstId, _at);
            Settle();
            Assert.DoesNotContain(Strings.ErrorOverlayCopied, Texts(rig.Overlay));

            ButtonNamed(rig.Overlay, Strings.ErrorOverlayCopy).Focus();
            rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            Dispatcher.UIThread.RunJobs();

            return (rig.Clipboard.LastText, Texts(rig.Overlay), Strings.ErrorOverlayCopied);
        });

        Assert.Equal(
            "ControlDeFinanzasIA 1.0.0\nError ID: 3f9a21c7 (3f9a21c7-0b5d-4e0a-9d1e-7c2b8a4f6e10)\nTime (UTC): 2026-09-30T14:03:11Z",
            result.Text);
        Assert.Equal("Copiado", result.Copied);
        Assert.Contains(result.Copied, result.Texts);
    }

    [Fact]
    public async Task AdditionalErrorsWhileOpen_ShowPlusOneMore_AndKeepTheFirstId()
    {
        (string[] Texts, string ShortId) result = await OnUi("en", rig =>
        {
            rig.Vm.Present(_firstId, _at);
            rig.Vm.Present(_secondId, _at);
            Settle();
            return (Texts(rig.Overlay), rig.Vm.ShortId);
        });

        Assert.Contains("+1 more", result.Texts);
        Assert.Equal("3f9a21c7", result.ShortId);
        Assert.DoesNotContain("a1b2c3d4", string.Join(' ', result.Texts), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpeningTheOverlay_ClosesTheNotificationsPopup()
    {
        (bool BeforeOpen, bool AfterOpen, bool BellBefore, bool BellAfter) result = await OnUi("en", rig =>
        {
            rig.Shell.Notifications.IsOpen = true;
            Settle();
            ToggleButton bell = rig.Window.GetVisualDescendants().OfType<ToggleButton>().Single(t => t.Name == "BellToggle");
            bool bellBefore = bell.IsChecked == true;

            rig.Vm.Present(_firstId, _at);
            Settle();

            return (true, rig.Shell.Notifications.IsOpen, bellBefore, bell.IsChecked == true);
        });

        Assert.True(result.BellBefore);
        Assert.False(result.AfterOpen);
        Assert.False(result.BellAfter);
    }

    [Fact]
    public async Task WhileTheOverlayIsOpen_EnterDoesNotFireAnIsDefaultButtonBehindIt()
    {
        (int WithOverlay, int AfterClose) result = await OnUi("en", rig =>
        {
            rig.ArmDefaultButton();
            Settle();

            rig.Vm.Present(_firstId, _at);
            Settle();
            rig.Window.FocusManager!.ClearFocus();
            rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            int withOverlay = rig.Repository.AddedExpenses.Count;

            rig.Vm.Close();
            Settle();
            rig.Window.FocusManager!.ClearFocus();
            rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            HeadlessUi.Flush();
            return (withOverlay, rig.Repository.AddedExpenses.Count);
        });

        Assert.Equal(0, result.WithOverlay);
        Assert.Equal(1, result.AfterClose);
    }

    private sealed class Rig : IDisposable
    {
        private static readonly Guid _categoryId = new("11111111-1111-1111-1111-000000000001");

        public Rig()
        {
            Clipboard = new FakeClipboardService();
            Vm = new ErrorOverlayViewModel(Clipboard, "1.0.0");
            Repository = new FakeExpenseRepository();
            var categories = new FakeCategoryRepository();
            categories.SetCategories([ExpensesViewModelTests.CreateCategory(_categoryId, "Comida")]);
            Expenses = ExpensesViewModelTests.CreateViewModel(expenseRepo: Repository, categoryRepo: categories);
            Expenses.SelectedCategoryId = _categoryId;
            Expenses.Amount = 100m;
            Expenses.Description = "Almuerzo";

            var navigation = new FakeNavigationService { CurrentViewModel = Expenses };
            Shell = new ShellViewModel(
                navigation,
                new FakeStringLocalizer<Strings>(new Dictionary<string, string>()),
                new NotificationListViewModel(
                    new FakeNotificationCenter(),
                    new FakeClock { Today = new DateOnly(2026, 9, 20) },
                    NullLogger<NotificationListViewModel>.Instance),
                Vm);
            Shell.Current = Expenses;

            Window = new MainWindow { DataContext = Shell, Width = 1100, Height = 900 };
            Window.Show();
            Settle();
        }

        public FakeClipboardService Clipboard { get; }

        public ErrorOverlayViewModel Vm { get; }

        public FakeExpenseRepository Repository { get; }

        public ExpensesViewModel Expenses { get; }

        public ShellViewModel Shell { get; }

        public MainWindow Window { get; }

        public ErrorOverlayView Overlay => Window.GetVisualDescendants().OfType<ErrorOverlayView>().Single();

        /// <summary>Gets a control inside the shell grid (the first sidebar item) that must be disabled while the overlay is open.</summary>
        public Control Content => Window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("sidebar-item"));

        /// <summary>Makes the real "register expense" button the window default button (Enter).</summary>
        public void ArmDefaultButton()
        {
            Button register = Window.GetVisualDescendants().OfType<Button>()
                .Single(b => ReferenceEquals(b.Command, Expenses.RegisterExpenseCommand));
            register.IsDefault = true;
        }

        public void Dispose() => Window.Close();
    }
}
