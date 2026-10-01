using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using ControlDeFinanzasIA.UI.Views.Charts;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Pre-1.0 slice E (E2): the REAL <see cref="DashboardView"/> resolves the design system on real content. Summary
/// figures are large and semantically colored, the cards use the large radius with the soft shadow, the category chart
/// has a legend whose dots are the palette colors, expense amounts are right aligned in the expense color and the
/// screen has exactly one primary action.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class DashboardDesignHeadlessTests
{
    private static readonly Guid _food = Guid.NewGuid();
    private static readonly Guid _fun = Guid.NewGuid();
    private static readonly DateOnly _today = new(2026, 3, 15);

    private static async Task<T> InRig<T>(Func<DashboardView, DashboardViewModel, FakeNavigationService, T> read)
    {
        return await HeadlessUi.OnUiThread(async () =>
        {
            var expenses = new FakeExpenseRepository { PageAddedItems = true };
            expenses.CategoryNames[_food] = "Comida";
            expenses.CategoryNames[_fun] = "Ocio";
            await expenses.AddAsync(Expense.Create(_food, new DateOnly(2026, 3, 2), Money.Create(300m, Currency.DOP), "Supermercado", null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
            await expenses.AddAsync(Expense.Create(_fun, new DateOnly(2026, 3, 3), Money.Create(100m, Currency.DOP), "Cine", null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
            var categories = new FakeCategoryRepository();
            categories.SetCategories([new Category { Id = _food, Name = "Comida" }, new Category { Id = _fun, Name = "Ocio" }]);
            var navigation = new FakeNavigationService(new Dictionary<string, ViewModelBase>(StringComparer.Ordinal) { { RouteNames.Expenses, ExpensesViewModelTests.CreateViewModel() } });
            DashboardViewModel vm = DashboardViewModelTests.CreateViewModel(expenses, new FakeIncomeRepository(), _today, categoryRepo: categories, navigation: navigation);
            var view = new DashboardView();
            Window window = HeadlessUi.Show(view, vm);
            try
            {
                for (int i = 0; i < 300 && vm.IsLoading; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(10);
                }

                HeadlessUi.Flush();
                return read(view, vm, navigation);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static TextBlock Block(Control root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == text);

    [Fact]
    public async Task TheSummaryFigures_AreLarge_AndCarryTheirSemanticColor()
    {
        (double expenseSize, Color? expenseColor, Color? incomeColor, Color? netColor, double netSize) = await InRig((view, vm, _) =>
        {
            TextBlock expense = Block(view, vm.TotalExpensesDisplay);
            TextBlock income = Block(view, vm.TotalIncomesDisplay);
            TextBlock net = Block(view, vm.NetBalanceDisplay);
            return (expense.FontSize, (expense.Foreground as ISolidColorBrush)?.Color, (income.Foreground as ISolidColorBrush)?.Color, (net.Foreground as ISolidColorBrush)?.Color, net.FontSize);
        });

        Assert.Equal(30, expenseSize);
        Assert.Equal(30, netSize);
        Assert.Equal(Color.Parse(ChartPalette.ExpenseHex), expenseColor);
        Assert.Equal(Color.Parse(ChartPalette.IncomeHex), incomeColor);
        Assert.NotEqual(expenseColor, netColor);
        Assert.NotEqual(incomeColor, netColor);
    }

    [Fact]
    public async Task TheCards_UseTheLargeRadius_AndTheSoftShadow()
    {
        (CornerRadius radius, int shadows, int cards) = await InRig((view, _, _) =>
        {
            Border[] cards = [.. view.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("card"))];
            return (cards[0].CornerRadius, cards[0].BoxShadow.Count, cards.Length);
        });

        Assert.Equal(new CornerRadius(16), radius);
        Assert.True(shadows > 0, "a card is lifted by a soft shadow, not a hard rule");
        Assert.True(cards >= 6, "summary x3, month expenses, charts x2 and the rest are cards");
    }

    [Fact]
    public async Task TheCategoryChart_HasALegend_WhoseDotsArePaletteColors()
    {
        (string[] dotColors, string[] labels) = await InRig((view, vm, _) =>
        {
            string[] dots = [.. view.GetVisualDescendants().OfType<Ellipse>().Where(e => e.Classes.Contains("legend-dot") && (e.Fill as ISolidColorBrush) is not null && e.DataContext is CategoryLegendItem).Select(e => ((ISolidColorBrush)e.Fill!).Color.ToString().ToUpperInvariant())];
            string[] texts = [.. vm.CategoryLegend.Select(l => l.Label).Where(l => view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == l))];
            return (dots, texts);
        });

        Assert.Equal(2, dotColors.Length);
        Assert.Equal(ChartPalette.SliceHexes[0], "#" + dotColors[0][3..], ignoreCase: true);
        Assert.Equal(ChartPalette.SliceHexes[1], "#" + dotColors[1][3..], ignoreCase: true);
        Assert.Equal(2, labels.Length);
    }

    [Fact]
    public async Task TheMonthExpenseAmounts_AreRightAligned_InTheExpenseColor()
    {
        (HorizontalAlignment alignment, Color? color, string family) = await InRig((view, _, _) =>
        {
            TextBlock amount = Block(view, $"{300m:N2} DOP");
            return (amount.HorizontalAlignment, (amount.Foreground as ISolidColorBrush)?.Color, amount.FontFeatures?.ToString() ?? string.Empty);
        });

        Assert.Equal(HorizontalAlignment.Right, alignment);
        Assert.Equal(Color.Parse(ChartPalette.ExpenseHex), color);
        Assert.NotNull(family);
    }

    [Fact]
    public async Task TheScreen_HasExactlyOnePrimaryAction_AndItOpensANewExpense()
    {
        (int primaries, string? label, string[] routes) = await InRig((view, vm, navigation) =>
        {
            Button[] primary = [.. view.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("ledger-btn-primary") && b.IsEffectivelyVisible)];
            vm.NewExpenseCommand.Execute(null);
            return (primary.Length, primary.Single().Content as string, navigation.NavigateToTypedCalls.ToArray());
        });

        Assert.Equal(1, primaries);
        Assert.Equal(Strings.ExpensesFormTitle, label);
        Assert.Equal(new[] { RouteNames.Expenses }, routes);
    }
}
