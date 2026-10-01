using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S7b (R-17, S-35, DD-8, design open question 1): the shared keyboard focus style, asserted on the resolved
/// template part of REAL controls carrying the REAL application styles in the headless session. The first test pins
/// whether Avalonia 11.2.7 supports <c>:focus-visible</c>; the rest rely on it.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class FocusStyleTests
{
    private static readonly Color _amber = Color.Parse("#E8A33D");

    /// <summary>Gets the case names of the style theory.</summary>
    public static IEnumerable<object[]> Cases =>
    [
        ["ledger-btn"],
        ["ledger-btn-primary"],
        ["ledger-btn-danger"],
        ["list-row"],
        ["check-box"],
        ["combo-ledger"],
        ["combo-input"],
    ];

    private static Control Build(string name) => name switch
    {
        "ledger-btn" => new Button { Content = "Save", Classes = { "ledger-btn" } },
        "ledger-btn-primary" => new Button { Content = "Save", Classes = { "ledger-btn-primary" } },
        "ledger-btn-danger" => new Button { Content = "Delete", Classes = { "ledger-btn-danger" } },
        "list-row" => new ListBoxItem { Content = "Row", Classes = { "ledger-row" } },
        "check-box" => new CheckBox { Content = "Option" },
        "combo-ledger" => new ComboBox { Classes = { "ledger-combo" }, ItemsSource = new[] { "A", "B" }, Width = 200 },
        "combo-input" => new ComboBox { Classes = { "ledger-input" }, ItemsSource = new[] { "A", "B" }, Width = 200 },
        "text-input" => new TextBox { Classes = { "ledger-input" }, Width = 200 },
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static (Color? Color, Thickness Thickness) Ring(Control control)
    {
        string part = control switch
        {
            TextBox => "PART_BorderElement",
            CheckBox => "NormalRectangle",
            ComboBox => "Background",
            _ => "PART_ContentPresenter",
        };

        Control? host = control.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == part);
        return host switch
        {
            ContentPresenter presenter => ((presenter.BorderBrush as ISolidColorBrush)?.Color, presenter.BorderThickness),
            Border border => ((border.BorderBrush as ISolidColorBrush)?.Color, border.BorderThickness),
            _ => throw new InvalidOperationException($"Template part {part} not found on {control.GetType().Name}."),
        };
    }

    private static bool ShowsAmberRing(Control control)
    {
        (Color? color, Thickness thickness) = Ring(control);
        return color == _amber && thickness.Left >= 1.99 && thickness.Top >= 1.99 && thickness.Right >= 1.99 && thickness.Bottom >= 1.99;
    }

    [Fact]
    public async Task Avalonia_11_2_7_SupportsFocusVisible_OnKeyboardFocusOnly()
    {
        (string Version, bool Tab, bool TabFocus, bool Pointer, bool PointerFocus, bool Unfocused) result = await HeadlessUi.OnUiThread(() =>
        {
            var button = new Button { Content = "Probe" };
            var other = new Button { Content = "Other" };
            Window window = HeadlessUi.Show(new StackPanel { Children = { button, other } }, null!);
            try
            {
                bool unfocused = button.Classes.Contains(":focus-visible");

                button.Focus(NavigationMethod.Tab);
                HeadlessUi.Flush();
                bool tab = button.Classes.Contains(":focus-visible");
                bool tabFocus = button.Classes.Contains(":focus");

                other.Focus(NavigationMethod.Pointer);
                button.Focus(NavigationMethod.Pointer);
                HeadlessUi.Flush();
                bool pointer = button.Classes.Contains(":focus-visible");
                bool pointerFocus = button.Classes.Contains(":focus");

                return Task.FromResult((typeof(Button).Assembly.GetName().Version!.ToString(), tab, tabFocus, pointer, pointerFocus, unfocused));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.StartsWith("11.2.", result.Version, StringComparison.Ordinal);
        Assert.False(result.Unfocused);
        Assert.True(result.Tab, ":focus-visible is set after keyboard (Tab) focus.");
        Assert.True(result.TabFocus);
        Assert.False(result.Pointer, ":focus-visible is NOT set after pointer focus.");
        Assert.True(result.PointerFocus, ":focus is still set after pointer focus.");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task KeyboardFocus_ShowsTheSharedAmberRing_AndPointerFocusDoesNot(string name)
    {
        (bool Before, bool AfterTab, bool AfterPointer) result = await HeadlessUi.OnUiThread(() =>
        {
            Control control = Build(name);
            var other = new Button { Content = "Other" };
            Window window = HeadlessUi.Show(new StackPanel { Children = { control, other } }, null!);
            try
            {
                bool before = ShowsAmberRing(control);

                control.Focus(NavigationMethod.Tab);
                HeadlessUi.Flush();
                bool afterTab = ShowsAmberRing(control);

                other.Focus(NavigationMethod.Pointer);
                control.Focus(NavigationMethod.Pointer);
                HeadlessUi.Flush();
                bool afterPointer = ShowsAmberRing(control);

                return Task.FromResult((before, afterTab, afterPointer));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.False(result.Before, $"{name} shows the ring while unfocused.");
        Assert.True(result.AfterTab, $"{name} does not show the amber ring after keyboard focus.");
        Assert.False(result.AfterPointer, $"{name} shows the ring after a mouse focus.");
    }

    [Theory]
    [InlineData(NavigationMethod.Tab)]
    [InlineData(NavigationMethod.Pointer)]
    public async Task ATextInput_ShowsTheAmberBorder_OnAnyFocus(NavigationMethod method)
    {
        bool amber = await HeadlessUi.OnUiThread(() =>
        {
            Control input = Build("text-input");
            Window window = HeadlessUi.Show(new StackPanel { Children = { input } }, null!);
            try
            {
                input.Focus(method);
                HeadlessUi.Flush();
                return Task.FromResult(Ring(input).Color == _amber);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(amber);
    }

    [Fact]
    public async Task TheSidebarItem_AndTheBell_ShowTheRingOnKeyboardFocus_OnTheRealWindow()
    {
        (bool ItemBefore, bool ItemTab, bool ItemPointer, bool BellBefore, bool BellTab, bool BellPointer) result = await HeadlessUi.OnUiThread(() =>
        {
            var shell = new ShellViewModel(
                new FakeNavigationService(),
                new FakeStringLocalizer<Strings>(new Dictionary<string, string>()),
                new NotificationListViewModel(
                    new FakeNotificationCenter(),
                    new FakeClock { Today = new DateOnly(2026, 9, 20) },
                    NullLogger<NotificationListViewModel>.Instance));
            var window = new MainWindow { DataContext = shell, Width = 1100, Height = 900 };
            window.Show();
            HeadlessUi.Flush();
            try
            {
                Button item = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("sidebar-item"));
                ToggleButton bell = window.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "BellToggle");

                bool itemBefore = ShowsAmberRing(item);
                bool bellBefore = ShowsAmberRing(bell);

                item.Focus(NavigationMethod.Tab);
                HeadlessUi.Flush();
                bool itemTab = ShowsAmberRing(item);
                bell.Focus(NavigationMethod.Tab);
                HeadlessUi.Flush();
                bool bellTab = ShowsAmberRing(bell);

                item.Focus(NavigationMethod.Pointer);
                HeadlessUi.Flush();
                bool itemPointer = ShowsAmberRing(item);
                bell.Focus(NavigationMethod.Pointer);
                HeadlessUi.Flush();
                bool bellPointer = ShowsAmberRing(bell);

                return Task.FromResult((itemBefore, itemTab, itemPointer, bellBefore, bellTab, bellPointer));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.False(result.ItemBefore);
        Assert.True(result.ItemTab, "The sidebar item shows the ring after keyboard focus.");
        Assert.False(result.ItemPointer);
        Assert.False(result.BellBefore);
        Assert.True(result.BellTab, "The bell shows the ring after keyboard focus.");
        Assert.False(result.BellPointer);
    }
}
