using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Input;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S7b (R-16, S-33, DD-6): the read-only Settings shortcut list is generated from the catalog, and the tooltips
/// and accelerator keys of the controls that trigger a catalog action show <c>KeyboardShortcutCatalog.Format</c>
/// output. REAL <see cref="SettingsViewModel"/>/<see cref="SettingsView"/>, REAL <see cref="MainWindow"/>.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class SettingsShortcutListTests
{
    private static string Gesture(ShortcutAction action) =>
        KeyboardShortcutCatalog.Format(KeyboardShortcutCatalog.All.First(s => s.Action == action), OperatingSystem.IsMacOS());

    private static Task<T> InCulture<T>(string culture, Func<Task<T>> body) =>
        HeadlessUi.OnUiThread(async () =>
        {
            CultureInfo previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            try
            {
                return await body();
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        });

    private static string Description(KeyboardShortcut entry) =>
        entry.Action == ShortcutAction.GoToSection
            ? string.Format(CultureInfo.CurrentUICulture, Strings.ShortcutGoToSectionNamed, Strings.ResourceManager.GetString(NavigationSections.All[(entry.SectionIndex!.Value + 9) % 10].LabelKey, CultureInfo.CurrentUICulture))
            : Strings.ResourceManager.GetString(entry.DescriptionKey, CultureInfo.CurrentUICulture)!;

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public async Task EveryCatalogEntry_IsListed_WithItsLocalizedActionNameAndGesture_ThenTheEnterLine(string culture)
    {
        (ShortcutListItem[] Actual, ShortcutListItem[] Expected) result = await InCulture(culture, () =>
        {
            SettingsViewModel vm = SettingsViewModelTests.CreateViewModel(new FakeSettingsStore());
            ShortcutListItem[] expected =
            [
                .. KeyboardShortcutCatalog.All.Select(e => new ShortcutListItem(Description(e), KeyboardShortcutCatalog.Format(e, OperatingSystem.IsMacOS()))),
                new ShortcutListItem(Strings.ShortcutEnterSubmitsAction, Strings.ShortcutKeyEnter),
            ];
            return Task.FromResult((vm.Shortcuts.ToArray(), expected));
        });

        Assert.Equal(KeyboardShortcutCatalog.All.Count + 1, result.Actual.Length);
        Assert.Equal(result.Expected, result.Actual);
        Assert.All(result.Actual, row => Assert.False(string.IsNullOrWhiteSpace(row.ActionName) || string.IsNullOrWhiteSpace(row.Gesture)));
        Assert.Equal(result.Actual.Length, result.Actual.Select(r => r.ActionName + "|" + r.Gesture).Distinct().Count());
    }

    [Fact]
    public async Task TheListTexts_DifferBetweenCultures_AndTheEnterLineIsThere()
    {
        ShortcutListItem[] es = await InCulture("es-DO", () => Task.FromResult(SettingsViewModelTests.CreateViewModel(new FakeSettingsStore()).Shortcuts.ToArray()));
        ShortcutListItem[] en = await InCulture("en", () => Task.FromResult(SettingsViewModelTests.CreateViewModel(new FakeSettingsStore()).Shortcuts.ToArray()));
        (ShortcutListItem[] Es, ShortcutListItem[] En) result = (es, en);
        Assert.Equal("Enviar el formulario que está en pantalla", result.Es[^1].ActionName);
        Assert.Equal("Submit the form that is on screen", result.En[^1].ActionName);
        Assert.Equal("Ctrl+N", result.En[0].Gesture);
        Assert.Equal("Ir a " + Strings.ResourceManager.GetString("SidebarDashboard", CultureInfo.GetCultureInfo("es-DO")), result.Es[2].ActionName);
        Assert.Equal("Go to " + Strings.ResourceManager.GetString("SidebarDashboard", CultureInfo.GetCultureInfo("en")), result.En[2].ActionName);
        Assert.NotEqual(result.Es[0].ActionName, result.En[0].ActionName);
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public async Task TheSettingsView_RendersTheListAsReadOnlyText_BetweenBackupAndAbout(string culture)
    {
        (string[] Heading, string[] Texts, int Interactive, int BeforeAbout, int Rows) result = await InCulture(culture, async () =>
        {
            SettingsViewModel vm = SettingsViewModelTests.CreateViewModel(new FakeSettingsStore());
            var view = new SettingsView();
            Window window = HeadlessUi.Show(view, vm);
            try
            {
                await Task.Delay(50);
                HeadlessUi.Flush();

                Border block = view.GetVisualDescendants().OfType<Border>()
                    .Single(b => b.Classes.Contains("shortcuts-block"));
                string[] texts = [.. block.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty)];
                int interactive = block.GetVisualDescendants().Count(v => v is Button or TextBox or ComboBox or CheckBox or ToggleButton or Slider);

                var all = view.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("settings-card")).ToList();
                int blockIndex = all.IndexOf(block);
                int aboutIndex = all.FindIndex(b => b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == Strings.SettingsAboutTitle));
                return (new[] { Strings.ShortcutsSectionTitle, Strings.ShortcutsSectionDescription }, texts, interactive, aboutIndex - blockIndex, vm.Shortcuts.Count);
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.Equal(culture == "en" ? "Keyboard shortcuts" : "Atajos de teclado", result.Heading[0]);
        Assert.All(result.Heading, h => Assert.Contains(h, result.Texts));
        Assert.Contains(Gesture(ShortcutAction.NewExpense), result.Texts);
        Assert.Contains(Gesture(ShortcutAction.ToggleNotifications), result.Texts);
        Assert.Equal(0, result.Interactive);
        Assert.Equal(1, result.BeforeAbout);
        Assert.Equal(KeyboardShortcutCatalog.All.Count + 1, result.Rows);
        Assert.Equal(result.Rows * 2 + 2, result.Texts.Length);
    }

    [Fact]
    public async Task TheBell_TheSidebarSettingsEntry_AndTheEmptyStateActions_ShowTheCatalogGesture()
    {
        (string? BellTip, string? BellKey, string? SettingsTip, string? SettingsKey, string? ExpenseTip, string? ExpenseKey, string? IncomeTip, string? IncomeKey) result =
            await InCulture("en", async () =>
            {
                using ServiceProvider provider = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
                var shell = new ShellViewModel(
                    new FakeNavigationService(),
                    provider.GetRequiredService<IStringLocalizer<Strings>>(),
                    new NotificationListViewModel(
                        new FakeNotificationCenter(),
                        new FakeClock { Today = new DateOnly(2026, 9, 20) },
                        NullLogger<NotificationListViewModel>.Instance));
                var window = new MainWindow { DataContext = shell, Width = 1100, Height = 900 };
                window.Show();
                HeadlessUi.Flush();

                ExpensesViewModel expenses = ExpensesViewModelTests.CreateViewModel();
                IncomeViewModel income = IncomeViewModelTests.CreateViewModel();
                var expensesView = new ExpensesView();
                var incomeView = new IncomeView();
                Window expensesWindow = HeadlessUi.Show(expensesView, expenses);
                Window incomeWindow = HeadlessUi.Show(incomeView, income);
                try
                {
                    await Task.Delay(150);
                    HeadlessUi.Flush();
                    ToggleButton bell = window.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "BellToggle");
                    Button settings = window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("sidebar-item") && ((SidebarItemViewModel)b.DataContext!).Route == RouteNames.Settings);
                    Button expenseAction = expensesView.GetVisualDescendants().OfType<Button>().Single(b => ReferenceEquals(b.Command, expenses.BeginNewEntryCommand));
                    Button incomeAction = incomeView.GetVisualDescendants().OfType<Button>().Single(b => ReferenceEquals(b.Command, income.BeginNewEntryCommand));
                    return (
                        ToolTip.GetTip(bell) as string,
                        AutomationProperties.GetAcceleratorKey(bell),
                        ToolTip.GetTip(settings) as string,
                        AutomationProperties.GetAcceleratorKey(settings),
                        ToolTip.GetTip(expenseAction) as string,
                        AutomationProperties.GetAcceleratorKey(expenseAction),
                        ToolTip.GetTip(incomeAction) as string,
                        AutomationProperties.GetAcceleratorKey(incomeAction));
                }
                finally
                {
                    window.Close();
                    expensesWindow.Close();
                    incomeWindow.Close();
                    HeadlessUi.Flush();
                }
            });

        string bellGesture = Gesture(ShortcutAction.ToggleNotifications);
        Assert.Equal("Ctrl+Shift+B", bellGesture);
        Assert.Equal($"Notifications ({bellGesture})", result.BellTip);
        Assert.Equal(bellGesture, result.BellKey);
        Assert.Equal($"Ctrl+0, {Gesture(ShortcutAction.OpenSettings)}", result.SettingsTip);
        Assert.Equal(result.SettingsTip, result.SettingsKey);
        Assert.Equal(Gesture(ShortcutAction.NewExpense), result.ExpenseTip);
        Assert.Equal(result.ExpenseTip, result.ExpenseKey);
        Assert.Equal(Gesture(ShortcutAction.NewIncome), result.IncomeTip);
        Assert.Equal(result.IncomeTip, result.IncomeKey);
    }

    [Fact]
    public void NoView_TypesAGestureByHand()
    {
        foreach (string file in new[] { "MainWindow.axaml", "SettingsView.axaml", "ExpensesView.axaml", "IncomeView.axaml" })
        {
            string text = File.ReadAllText(Packaging.RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Views", file));
            Assert.DoesNotContain("Ctrl+", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Ctrl +", text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Ctrl+", File.ReadAllText(Packaging.RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Controls", "EmptyState.axaml")), StringComparison.Ordinal);
    }
}
