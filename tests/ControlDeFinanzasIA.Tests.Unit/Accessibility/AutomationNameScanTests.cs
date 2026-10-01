using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Tests.Unit.UI;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.Tests.Unit.Accessibility;

/// <summary>
/// F-021 S8 (R-17, S-34, DD-11 scan 2): on the REAL <c>MainWindow</c> (real composition root, migrated temp SQLite
/// database, real <c>ShellViewModel</c>) with every section opened through the shell, each focusable, enabled, visible
/// control exposes a non-empty accessible name through its REAL automation peer. This catches names lost through
/// templates (date picker flyout button, numeric spin buttons, calendar day buttons, empty-state action).
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class AutomationNameScanTests
{
    private static readonly string[] _sections =
        [.. NavigationSections.All.Select(s => s.Route)];

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public async Task EveryFocusableControl_OfEverySection_HasAMeaningfulPeerName(string culture)
    {
        (List<string> Unnamed, Dictionary<string, int> Categories, int Sections) result = await HeadlessUi.OnUiThread(async () =>
        {
            await using RealShellRig rig = await RealShellRig.CreateAsync(culture);
            var unnamed = new List<string>();
            var categories = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < NavigationSections.All.Count; i++)
            {
                await rig.OpenSectionAsync(i);
                await SeedRowsAsync(rig);
                Assert.Equal(NavigationSections.All[i].ViewModelType, rig.Shell.Current!.GetType());

                foreach (Control control in rig.Window.GetVisualDescendants().OfType<Control>().Where(IsInteractive))
                {
                    string name = ControlAutomationPeer.CreatePeerForElement(control).GetName();
                    string kind = Classify(control);
                    categories[kind] = categories.GetValueOrDefault(kind) + 1;
                    if (string.IsNullOrWhiteSpace(name) || name.Contains("Avalonia.", StringComparison.Ordinal))
                    {
                        unnamed.Add($"{NavigationSections.All[i].Route}: {control.GetType().Name}#{control.Name} ({kind}) name='{name}'");
                    }
                }
            }

            return (unnamed, categories, NavigationSections.All.Count);
        });

        Assert.Equal(10, result.Sections);
        Assert.True(result.Unnamed.Count == 0, "Controls without a meaningful accessible name:" + Environment.NewLine + string.Join(Environment.NewLine, result.Unnamed));

        // The walk must really include the categories the spec names (a walk that finds nothing proves nothing).
        foreach (string kind in new[] { "bell", "sidebar", "date-picker", "combo", "text-input", "toggle", "spin-button", "calendar-day", "row-button" })
        {
            Assert.True(result.Categories.GetValueOrDefault(kind) > 0, $"The walk found no '{kind}' control.");
        }
    }

    private static bool IsInteractive(Control control) =>
        control.Focusable && control.IsEnabled && control.IsEffectivelyVisible;

    private static string Classify(Control control) => control switch
    {
        ToggleButton { Name: "BellToggle" } => "bell",
        Button b when b.Classes.Contains("sidebar-item") => "sidebar",
        Button { Name: "PART_FlyoutButton" } => "date-picker",
        RepeatButton => "spin-button",
        Button b when b.Classes.Contains("calendar-day") => "calendar-day",
        CheckBox => "toggle",
        ComboBox => "combo",
        TextBox => "text-input",
        Button { Command: not null } when control.FindAncestorOfType<ItemsControl>() is { Name: not "Sidebar" } => "row-button",
        _ => "other",
    };

    /// <summary>Adds one real row where a section lists rows (debt with its schedule, expense) so row templates are walked.</summary>
    private static async Task SeedRowsAsync(RealShellRig rig)
    {
        switch (rig.Shell.Current)
        {
            case DebtsViewModel { Debts.Count: 0 } debts:
                debts.Description = "Car loan";
                debts.OriginalAmount = 1200m;
                debts.TotalInstallments = 3;
                debts.InstallmentAmount = 400m;
                debts.StartDate = new DateTime(2026, 9, 1);
                debts.CreateCommand.Execute(null);
                await rig.SettleAsync(() => debts.Debts.Count > 0);
                debts.SelectedDebt = debts.Debts[0];
                await rig.SettleAsync(() => debts.Schedule.Count > 0);
                break;
            case ExpensesViewModel { Expenses.Count: 0 } expenses:
                await rig.SettleAsync(() => expenses.Categories.Count > 0);
                expenses.Description = "Lunch";
                expenses.Amount = 250m;
                expenses.SelectedCategoryId = expenses.Categories[0].Id;
                expenses.RegisterExpenseCommand.Execute(null);
                await rig.SettleAsync(() => expenses.Expenses.Count > 0);
                break;
        }
    }
}
