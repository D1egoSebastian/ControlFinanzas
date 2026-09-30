using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Input;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S7a (R-12, R-16, S-24, D-26): the REAL <see cref="MainWindow"/> with the REAL <see cref="ShellViewModel"/>.
/// The window key bindings must equal the catalog for the current platform in both directions.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class ShortcutCatalogParityTests
{
    private sealed class Probe : ViewModelBase
    {
    }

    private static Task<T> OnUi<T>(Func<ShellViewModel, MainWindow, T> body) =>
        HeadlessUi.OnUiThread(() =>
        {
            var shell = new ShellViewModel(
                new FakeNavigationService(),
                new FakeStringLocalizer<Strings>(new Dictionary<string, string>()),
                new NotificationListViewModel(
                    new FakeNotificationCenter(),
                    new FakeClock { Today = new DateOnly(2026, 9, 20) },
                    NullLogger<NotificationListViewModel>.Instance),
                new ErrorOverlayViewModel(new FakeClipboardService(), "1.0.0"));
            var window = new MainWindow { DataContext = shell, Width = 1100, Height = 900 };
            window.Show();
            HeadlessUi.Flush();
            try
            {
                return Task.FromResult(body(shell, window));
            }
            finally
            {
                window.Close();
            }
        });

    [Fact]
    public async Task TheWindowKeyBindings_EqualTheCatalogForThisPlatform_InBothDirections()
    {
        (int Bindings, int Entries, List<string> Missing, List<string> Extra) result = await OnUi((shell, window) =>
        {
            IReadOnlyList<KeyboardShortcut> expected = KeyboardShortcutCatalog.ForPlatform(OperatingSystem.IsMacOS());
            var bindings = window.KeyBindings.OfType<KeyBinding>().ToList();
            var missing = new List<string>();
            var extra = new List<string>();

            foreach (KeyboardShortcut entry in expected)
            {
                KeyBinding? match = bindings.SingleOrDefault(b =>
                    b.Gesture.Key == entry.Key
                    && b.Gesture.KeyModifiers == entry.Modifiers
                    && ReferenceEquals(b.Command, shell.Resolve(entry.Action))
                    && Equals(b.CommandParameter, entry.SectionIndex));
                if (match is null)
                {
                    missing.Add($"{entry.Action} {entry.Modifiers}+{entry.Key}");
                }
            }

            foreach (KeyBinding binding in bindings)
            {
                if (!expected.Any(e => e.Key == binding.Gesture.Key && e.Modifiers == binding.Gesture.KeyModifiers))
                {
                    extra.Add($"{binding.Gesture.KeyModifiers}+{binding.Gesture.Key}");
                }
            }

            return (bindings.Count, expected.Count, missing, extra);
        });

        Assert.Equal(result.Entries, result.Bindings);
        Assert.Empty(result.Missing);
        Assert.Empty(result.Extra);
        Assert.True(result.Entries >= 15);
    }

    [Fact]
    public async Task RealKeyEvents_DriveTheShellCommands_ThroughTheGeneratedBindings()
    {
        (bool Popup1, bool Popup2, bool Popup3, bool Overlay1, bool Overlay2, string[] Navigated) result = await HeadlessUi.OnUiThread(() =>
        {
            var navigation = new FakeNavigationService(
                NavigationSections.All.ToDictionary(s => s.Route, _ => (ViewModelBase)new Probe(), StringComparer.Ordinal));
            var overlay = new ErrorOverlayViewModel(new FakeClipboardService(), "1.0.0");
            var shell = new ShellViewModel(
                navigation,
                new FakeStringLocalizer<Strings>(new Dictionary<string, string>()),
                new NotificationListViewModel(
                    new FakeNotificationCenter(),
                    new FakeClock { Today = new DateOnly(2026, 9, 20) },
                    NullLogger<NotificationListViewModel>.Instance),
                overlay);
            var window = new MainWindow { DataContext = shell, Width = 1100, Height = 900 };
            window.Show();
            HeadlessUi.Flush();
            try
            {
                window.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.Control | RawInputModifiers.Shift);
                HeadlessUi.Flush();
                bool popup1 = shell.Notifications.IsOpen;

                window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                HeadlessUi.Flush();
                bool popup2 = shell.Notifications.IsOpen;

                window.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.Control | RawInputModifiers.Shift);
                HeadlessUi.Flush();
                bool popup3 = shell.Notifications.IsOpen;

                window.KeyPressQwerty(PhysicalKey.Digit2, RawInputModifiers.Control);
                window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Control);
                window.KeyPressQwerty(PhysicalKey.Comma, RawInputModifiers.Control);
                HeadlessUi.Flush();

                overlay.Present(Guid.NewGuid(), DateTime.UtcNow);
                HeadlessUi.Flush();
                bool overlay1 = overlay.IsOpen;
                window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Control);
                window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                HeadlessUi.Flush();

                return Task.FromResult((popup1, popup2, popup3, overlay1, overlay.IsOpen, navigation.NavigateCalls.ToArray()));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(result.Popup1);
        Assert.False(result.Popup2);
        Assert.True(result.Popup3);
        Assert.True(result.Overlay1);
        Assert.False(result.Overlay2);
        Assert.Equal(
            new[] { "Expenses", "Settings", "Settings" },
            result.Navigated);
    }

    [Fact]
    public async Task EveryCatalogActionResolvesToADistinctShellCommand()
    {
        ShellViewModel shell = await OnUi((s, _) => s);

        var commands = Enum.GetValues<ShortcutAction>().Select(shell.Resolve).ToList();

        Assert.Equal(6, commands.Distinct().Count());
        Assert.All(commands, Assert.NotNull);
        Assert.Same(shell.Resolve(ShortcutAction.GoToSection), shell.Resolve(ShortcutAction.GoToSection));
    }

    [Fact]
    public async Task ThePopupHasItsOwnEscapeBinding_ToTheSameEscapeCommand()
    {
        (int Count, Key Key, KeyModifiers Modifiers, bool SameCommand) result = await OnUi((shell, window) =>
        {
            shell.Notifications.IsOpen = true;
            HeadlessUi.Flush();
            Popup popup = window.GetVisualDescendants().OfType<Popup>().Single();
            Border card = Assert.IsType<Border>(popup.Child);
            KeyBinding binding = card.KeyBindings.OfType<KeyBinding>().Single();
            return (card.KeyBindings.Count, binding.Gesture.Key, binding.Gesture.KeyModifiers, ReferenceEquals(binding.Command, shell.EscapeCommand));
        });

        Assert.Equal(1, result.Count);
        Assert.Equal(Key.Escape, result.Key);
        Assert.Equal(KeyModifiers.None, result.Modifiers);
        Assert.True(result.SameCommand);
    }
}
