using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using ControlDeFinanzasIA.Tests.Unit.UI;
using ControlDeFinanzasIA.UI.Input;

namespace ControlDeFinanzasIA.Tests.Unit.Input;

/// <summary>
/// F-021 S7a (R-12, DD-6): the REAL <see cref="KeyboardShortcuts"/> behavior on a real headless <see cref="Window"/>.
/// Only the command source is a recording double (a collaborator around the behavior, never the behavior itself).
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class KeyboardShortcutsBehaviorTests
{
    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = [];

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    private sealed class RecordingSource : IShortcutCommandSource
    {
        private readonly Dictionary<ShortcutAction, RecordingCommand> _commands =
            Enum.GetValues<ShortcutAction>().ToDictionary(a => a, _ => new RecordingCommand());

        public RecordingCommand For(ShortcutAction action) => _commands[action];

        public ICommand Resolve(ShortcutAction action) => _commands[action];
    }

    private static Task<T> OnUi<T>(Func<Window, T> body) =>
        HeadlessUi.OnUiThread(() =>
        {
            var window = new Window { Width = 400, Height = 300 };
            window.Show();
            HeadlessUi.Flush();
            try
            {
                return Task.FromResult(body(window));
            }
            finally
            {
                window.Close();
            }
        });

    private static List<KeyBinding> Bindings(InputElement element) => element.KeyBindings.OfType<KeyBinding>().ToList();

    [Fact]
    public async Task Enabling_GeneratesOneBindingPerCatalogEntry_BoundToTheDataContextSource()
    {
        (int Count, int Mismatched, int Unbound) result = await OnUi(window =>
        {
            var source = new RecordingSource();
            window.DataContext = source;
            KeyboardShortcuts.SetEnabled(window, true);

            List<KeyBinding> bindings = Bindings(window);
            IReadOnlyList<KeyboardShortcut> expected = KeyboardShortcutCatalog.ForPlatform(OperatingSystem.IsMacOS());
            int mismatched = expected.Count(e => !bindings.Any(b =>
                b.Gesture.Key == e.Key
                && b.Gesture.KeyModifiers == e.Modifiers
                && ReferenceEquals(b.Command, source.Resolve(e.Action))
                && Equals(b.CommandParameter, e.SectionIndex)));
            return (bindings.Count, mismatched, bindings.Count(b => b.Command is null));
        });

        Assert.Equal(KeyboardShortcutCatalog.ForPlatform(OperatingSystem.IsMacOS()).Count, result.Count);
        Assert.Equal(0, result.Mismatched);
        Assert.Equal(0, result.Unbound);
    }

    [Fact]
    public async Task ADataContextSetAfterEnabling_BindsTheCommands_AndChangingItRebindsWithoutDuplicates()
    {
        (int Before, int Null, int After, bool FirstSource, bool SecondSource, int Cleared, int NullAfter) result = await OnUi(window =>
        {
            KeyboardShortcuts.SetEnabled(window, true);
            int before = Bindings(window).Count;
            int unbound = Bindings(window).Count(b => b.Command is null);

            var first = new RecordingSource();
            window.DataContext = first;
            bool firstBound = Bindings(window).Single(b => b.Gesture.Key == Key.N && b.Gesture.KeyModifiers == KeyModifiers.Control).Command == first.For(ShortcutAction.NewExpense);

            var second = new RecordingSource();
            window.DataContext = second;
            List<KeyBinding> after = Bindings(window);
            bool secondBound = after.Single(b => b.Gesture.Key == Key.N && b.Gesture.KeyModifiers == KeyModifiers.Control).Command == second.For(ShortcutAction.NewExpense);

            window.DataContext = null;
            return (before, unbound, after.Count, firstBound, secondBound, Bindings(window).Count, Bindings(window).Count(b => b.Command is null));
        });

        int expected = KeyboardShortcutCatalog.ForPlatform(OperatingSystem.IsMacOS()).Count;
        Assert.Equal(expected, result.Before);
        Assert.Equal(expected, result.Null);
        Assert.True(result.FirstSource);
        Assert.True(result.SecondSource);
        Assert.Equal(expected, result.After);
        Assert.Equal(expected, result.Cleared);
        Assert.Equal(expected, result.NullAfter);
    }

    [Fact]
    public async Task AnExplicitSource_WinsOverTheDataContext()
    {
        bool usesExplicit = await OnUi(window =>
        {
            var context = new RecordingSource();
            var explicitSource = new RecordingSource();
            window.DataContext = context;
            KeyboardShortcuts.SetSource(window, explicitSource);
            KeyboardShortcuts.SetEnabled(window, true);

            return Bindings(window).All(b => b.Command is not null)
                && ReferenceEquals(
                    Bindings(window).Single(b => b.Gesture.Key == Key.B).Command,
                    explicitSource.For(ShortcutAction.ToggleNotifications));
        });

        Assert.True(usesExplicit);
    }

    [Fact]
    public async Task EscapeOnly_GeneratesASingleEscapeBinding()
    {
        (int Count, Key Key, KeyModifiers Modifiers) result = await OnUi(window =>
        {
            window.DataContext = new RecordingSource();
            KeyboardShortcuts.SetEscapeOnly(window, true);
            KeyboardShortcuts.SetEnabled(window, true);

            KeyBinding only = Bindings(window).Single();
            return (window.KeyBindings.Count, only.Gesture.Key, only.Gesture.KeyModifiers);
        });

        Assert.Equal(1, result.Count);
        Assert.Equal(Key.Escape, result.Key);
        Assert.Equal(KeyModifiers.None, result.Modifiers);
    }

    [Fact]
    public async Task Disabling_RemovesEveryGeneratedBinding_ButKeepsHandWrittenOnes()
    {
        (int WithHandWritten, int AfterDisable) result = await OnUi(window =>
        {
            window.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F9) });
            window.DataContext = new RecordingSource();
            KeyboardShortcuts.SetEnabled(window, true);
            int withAll = window.KeyBindings.Count;

            KeyboardShortcuts.SetEnabled(window, false);
            return (withAll, window.KeyBindings.Count);
        });

        Assert.Equal(1 + KeyboardShortcutCatalog.ForPlatform(OperatingSystem.IsMacOS()).Count, result.WithHandWritten);
        Assert.Equal(1, result.AfterDisable);
    }

    [Fact]
    public async Task RealKeyEvents_RunTheResolvedCommands_WithTheSectionDigitAsParameter()
    {
        (int NewExpense, object? Digit, int Escape, int Ignored) result = await OnUi(window =>
        {
            var source = new RecordingSource();
            window.DataContext = source;
            KeyboardShortcuts.SetEnabled(window, true);

            window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.Control);
            window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Control);
            window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Control);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.None);

            RecordingCommand sections = source.For(ShortcutAction.GoToSection);
            return (
                source.For(ShortcutAction.NewExpense).Parameters.Count,
                sections.Parameters.Count == 2 ? $"{sections.Parameters[0]},{sections.Parameters[1]}" : "wrong",
                source.For(ShortcutAction.Escape).Parameters.Count,
                source.For(ShortcutAction.NewIncome).Parameters.Count);
        });

        Assert.Equal(1, result.NewExpense);
        Assert.Equal("3,0", result.Digit);
        Assert.Equal(1, result.Escape);
        Assert.Equal(0, result.Ignored);
    }
}
