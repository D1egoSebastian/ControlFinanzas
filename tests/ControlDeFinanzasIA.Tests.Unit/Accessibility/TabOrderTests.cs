using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Tests.Unit.UI;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.Tests.Unit.Accessibility;

/// <summary>
/// F-021 S8 (R-14, R-17): after navigating with a shortcut, keyboard focus lands in the new screen's first field, and
/// pressing Tab walks the form fields in visual order (rows top to bottom, left to right inside a row). The REAL window,
/// shell and view-models are used (real composition root over a temp SQLite database).
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class TabOrderTests
{
    private const double _sameRowTolerance = 12;

    /// <summary>Sidebar position (0-based) and the view-model type of the four primary forms.</summary>
    public static TheoryData<int, string> Forms() => new()
    {
        { 1, nameof(ExpensesViewModel) },
        { 2, nameof(IncomeViewModel) },
        { 3, nameof(RecurringRulesViewModel) },
        { 4, nameof(DebtsViewModel) },
    };

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task AfterNavigating_FocusLandsInTheFirstField_AndTabFollowsTheVisualOrder(int position, string viewModel)
    {
        (string Section, bool InContent, bool FirstIsFirstField, string[] Expected, string[] Actual) result = await HeadlessUi.OnUiThread(async () =>
        {
            await using RealShellRig rig = await RealShellRig.CreateAsync("en");
            await rig.OpenSectionAsync(position);

            IInputElement? first = rig.Window.FocusManager!.GetFocusedElement();
            List<Control> fields = FormFields(rig.Window);
            List<Control> expected = InVisualOrder(rig.Window, fields);

            var visited = new List<Control>();
            for (int i = 0; i < 40; i++)
            {
                if (rig.Window.FocusManager!.GetFocusedElement() is Control focused && fields.Contains(focused))
                {
                    if (visited.Contains(focused))
                    {
                        break;
                    }

                    visited.Add(focused);
                }

                rig.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                HeadlessUi.Flush();
            }

            return (
                rig.Shell.Current!.GetType().Name,
                first is Control control && !IsInSidebar(control),
                ReferenceEquals(first, expected[0]),
                expected.Select(Describe).ToArray(),
                visited.Select(Describe).ToArray());
        });

        Assert.Equal(viewModel, result.Section);
        Assert.True(result.InContent, "After navigating, focus must land in the new screen, not stay in the sidebar.");
        Assert.True(result.FirstIsFirstField, "After navigating, focus must land on the first form field.");
        Assert.True(result.Expected.Length >= 6, $"Only {result.Expected.Length} form fields found.");
        Assert.Equal(result.Expected, result.Actual);
    }

    [Fact]
    public void TheOrderHelper_CanDetectAMisorderedSequence()
    {
        // Control case on the helper itself: a row-major order is not column-major.
        Assert.True(IsBefore((0, 0), (0, 200)));
        Assert.True(IsBefore((0, 200), (40, 0)));
        Assert.False(IsBefore((40, 0), (0, 200)));
    }

    private static bool IsBefore((double Y, double X) a, (double Y, double X) b) =>
        Math.Abs(a.Y - b.Y) <= _sameRowTolerance ? a.X < b.X : a.Y < b.Y;

    /// <summary>The text inputs, selectors and date-picker buttons of the screen on display (not the sidebar or the bell).</summary>
    private static List<Control> FormFields(Window window) =>
    [
        .. window.GetVisualDescendants().OfType<Control>()
            .Where(c => c.Focusable && c.IsEnabled && c.IsEffectivelyVisible && !IsInSidebar(c))
            .Where(c => c is TextBox or ComboBox || c is Button { Name: "PART_FlyoutButton" }),
    ];

    private static List<Control> InVisualOrder(Window window, List<Control> fields)
    {
        var placed = fields
            .Select(f => (Control: f, Origin: f.TranslatePoint(default, window) ?? default))
            .OrderBy(p => p.Origin.Y)
            .ToList();

        var ordered = new List<Control>();
        while (placed.Count > 0)
        {
            double rowTop = placed[0].Origin.Y;
            var row = placed.Where(p => Math.Abs(p.Origin.Y - rowTop) <= _sameRowTolerance).OrderBy(p => p.Origin.X).ToList();
            ordered.AddRange(row.Select(p => p.Control));
            placed.RemoveAll(p => row.Contains(p));
        }

        return ordered;
    }

    private static string Describe(Control control) => $"{control.GetType().Name}:{control.AutomationName()}";

    private static bool IsInSidebar(Control control) =>
        control.Classes.Contains("sidebar-item")
        || control.GetVisualAncestors().OfType<Control>().Any(a => a.Classes.Contains("sidebar-item"));
}

internal static class ControlNameExtensions
{
    public static string AutomationName(this Control control) =>
        Avalonia.Automation.AutomationProperties.GetName(control) ?? control.Name ?? string.Empty;
}
