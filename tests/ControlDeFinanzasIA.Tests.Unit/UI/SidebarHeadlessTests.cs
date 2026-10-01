using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S7b (R-15, S-32, T11): the REAL <see cref="MainWindow"/> with the REAL <see cref="ShellViewModel"/> and the
/// real localizer. The screens behind Expenses, Income and Recurring are the real view-models over the existing
/// repository doubles; only the navigation service and the notification center around the shell are doubles.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class SidebarHeadlessTests
{
    private sealed class Rig : IDisposable
    {
        private readonly ServiceProvider _provider;

        public Rig(IDictionary<string, ViewModelBase>? screens = null)
        {
            _provider = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
            Navigation = new FakeNavigationService(screens is null ? null : new Dictionary<string, ViewModelBase>(screens));
            Shell = new ShellViewModel(
                Navigation,
                _provider.GetRequiredService<IStringLocalizer<Strings>>(),
                new NotificationListViewModel(
                    new FakeNotificationCenter(),
                    new FakeClock { Today = new DateOnly(2026, 9, 20) },
                    NullLogger<NotificationListViewModel>.Instance));
            Window = new MainWindow { DataContext = Shell, Width = 1100, Height = 900 };
            Window.Show();
            HeadlessUi.Flush();
        }

        public FakeNavigationService Navigation { get; }

        public ShellViewModel Shell { get; }

        public MainWindow Window { get; }

        public List<Button> Items => [.. Window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("sidebar-item"))];

        public void Dispose()
        {
            Window.Close();
            HeadlessUi.Flush();
            _provider.Dispose();
        }
    }

    private static Task<T> OnUi<T>(string culture, Func<T> body) =>
        HeadlessUi.OnUiThread(() =>
        {
            CultureInfo previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            try
            {
                return Task.FromResult(body());
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        });

    private static Dictionary<string, ViewModelBase> RealScreens() => new(StringComparer.Ordinal)
    {
        [RouteNames.Expenses] = ExpensesViewModelTests.CreateViewModel(),
        [RouteNames.Incomes] = IncomeViewModelTests.CreateViewModel(),
        [RouteNames.Recurring] = DebtsRecurringLocalizationTests.NewRecurringRig(delay: false).GetAwaiter().GetResult().Vm,
    };

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        HeadlessUi.Flush();
    }

    private static IInputElement? Focused(Window window) => window.FocusManager!.GetFocusedElement();

    [Fact]
    public async Task TheSidebar_IsTenFlatButtons_AndNoListBoxItemContainsAButton()
    {
        (int Items, int ListBoxItems, int NestedButtons, bool AllBound) result = await OnUi("en", () =>
        {
            using var rig = new Rig();
            List<Button> items = rig.Items;
            var listBoxItems = rig.Window.GetVisualDescendants().OfType<ListBoxItem>().ToList();
            int nested = listBoxItems.Sum(i => i.GetVisualDescendants().OfType<Button>().Count());
            return (items.Count, listBoxItems.Count, nested, items.Zip(rig.Shell.SidebarItems).All(p => ReferenceEquals(p.First.DataContext, p.Second)));
        });

        Assert.Equal(10, result.Items);
        Assert.Equal(0, result.NestedButtons);
        Assert.Equal(0, result.ListBoxItems);
        Assert.True(result.AllBound);
    }

    [Fact]
    public async Task EachItem_IsASingleTabStop()
    {
        List<int> stops = await OnUi("en", () =>
        {
            using var rig = new Rig();
            return rig.Items
                .Select(item => item.GetVisualDescendants().OfType<InputElement>().Count(e => e.Focusable && e.IsTabStop && e.IsEffectivelyVisible)
                    + (item.Focusable && item.IsTabStop ? 1 : 0))
                .ToList();
        });

        Assert.Equal(10, stops.Count);
        Assert.All(stops, count => Assert.Equal(1, count));
    }

    [Fact]
    public async Task TabAndTheArrowKeys_ReachEveryItem_InOrder()
    {
        (List<int> ByTab, List<int> ByArrowDown, int UpLandsOn) result = await OnUi("en", () =>
        {
            using var rig = new Rig();
            List<Button> items = rig.Items;

            var byTab = new List<int>();
            items[0].Focus(NavigationMethod.Tab);
            HeadlessUi.Flush();
            byTab.Add(items.IndexOf((Button)Focused(rig.Window)!));
            for (int i = 1; i < 10; i++)
            {
                Press(rig.Window, PhysicalKey.Tab);
                byTab.Add(items.IndexOf((Button)Focused(rig.Window)!));
            }

            var byArrow = new List<int>();
            items[0].Focus(NavigationMethod.Tab);
            HeadlessUi.Flush();
            byArrow.Add(0);
            for (int i = 1; i < 10; i++)
            {
                Press(rig.Window, PhysicalKey.ArrowDown);
                byArrow.Add(Focused(rig.Window) is Button b ? items.IndexOf(b) : -1);
            }

            Press(rig.Window, PhysicalKey.ArrowUp);
            return (byTab, byArrow, Focused(rig.Window) is Button up ? items.IndexOf(up) : -1);
        });

        Assert.Equal(Enumerable.Range(0, 10), result.ByTab);
        Assert.Equal(Enumerable.Range(0, 10), result.ByArrowDown);
        Assert.Equal(8, result.UpLandsOn);
    }

    [Fact]
    public async Task EnterAndSpace_OnAFocusedItem_NavigateToItsRoute()
    {
        string[] navigated = await OnUi("en", () =>
        {
            using var rig = new Rig(RealScreens());
            List<Button> items = rig.Items;

            items[2].Focus(NavigationMethod.Tab);
            HeadlessUi.Flush();
            Press(rig.Window, PhysicalKey.Enter);

            items[3].Focus(NavigationMethod.Tab);
            HeadlessUi.Flush();
            Press(rig.Window, PhysicalKey.Space);
            rig.Window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            HeadlessUi.Flush();

            return rig.Navigation.NavigateCalls.ToArray();
        });

        Assert.Equal(new[] { RouteNames.Incomes, RouteNames.Recurring }, navigated);
    }

    [Theory]
    [InlineData("es-DO", RouteNames.Incomes, 2)]
    [InlineData("en", RouteNames.Recurring, 3)]
    [InlineData("en", RouteNames.Expenses, 1)]
    public async Task TheCurrentItem_HasTheCurrentClass_TheLabelAsName_AndTheLocalizedStatus(string culture, string route, int index)
    {
        (List<bool> CurrentFlags, string? Name, string? Status, List<string?> OtherStatuses, string Expected, string Label, int Position, int Size, string? Tip, string? Accelerator) result =
            await OnUi(culture, () =>
            {
                using var rig = new Rig(RealScreens());
                rig.Navigation.NavigateTo(route);
                HeadlessUi.Flush();
                List<Button> items = rig.Items;
                Button current = items[index];
                return (
                    items.Select(b => b.Classes.Contains("current")).ToList(),
                    AutomationProperties.GetName(current),
                    AutomationProperties.GetItemStatus(current),
                    items.Where(b => b != current).Select(AutomationProperties.GetItemStatus).ToList(),
                    Strings.SidebarCurrentSection,
                    rig.Shell.SidebarItems[index].Label,
                    AutomationProperties.GetPositionInSet(current),
                    AutomationProperties.GetSizeOfSet(current),
                    ToolTip.GetTip(current) as string,
                    AutomationProperties.GetAcceleratorKey(current));
            });

        Assert.Equal(index, Assert.Single(Enumerable.Range(0, 10), i => result.CurrentFlags[i]));
        Assert.Equal(result.Label, result.Name);
        Assert.Equal(result.Expected, result.Status);
        Assert.All(result.OtherStatuses, s => Assert.True(string.IsNullOrEmpty(s)));
        Assert.Equal(index + 1, result.Position);
        Assert.Equal(10, result.Size);
        Assert.Equal($"Ctrl+{(index + 1) % 10}", result.Tip);
        Assert.Equal(result.Tip, result.Accelerator);
        Assert.Equal(culture == "en" ? "Current section" : "Sección actual", result.Expected);
    }

    [Fact]
    public async Task TheDigitHint_ShowsOneToNineThenZero()
    {
        List<string?> hints = await OnUi("en", () =>
        {
            using var rig = new Rig();
            return rig.Items
                .Select(b => b.GetVisualDescendants().OfType<TextBlock>().SingleOrDefault(t => t.Classes.Contains("sidebar-hint"))?.Text)
                .ToList();
        });

        Assert.Equal(new string?[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" }, hints);
    }

    [Theory]
    [InlineData("es-DO", "Recurrentes", "Deudas", "Calendario")]
    [InlineData("en", "Recurring", "Debts", "Calendar")]
    public async Task TheThreeFormerlyHardcodedLabels_RenderInTheActiveCulture(string culture, string recurring, string debts, string calendar)
    {
        List<string?> texts = await OnUi(culture, () =>
        {
            using var rig = new Rig();
            return rig.Items
                .Select(b => b.GetVisualDescendants().OfType<TextBlock>().First(t => !t.Classes.Contains("sidebar-hint")).Text)
                .ToList();
        });

        Assert.Equal(recurring, texts[3]);
        Assert.Equal(debts, texts[4]);
        Assert.Equal(calendar, texts[5]);
        Assert.Equal(10, texts.Distinct().Count());
    }
}
