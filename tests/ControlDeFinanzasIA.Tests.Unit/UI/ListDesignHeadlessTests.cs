using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using ControlDeFinanzasIA.UI.Views.Charts;
using static ControlDeFinanzasIA.Tests.Unit.UI.LedgerViewKit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Pre-1.0 slice E (E2): the Expenses and Income lists resolve the design system on real rows. The list is one rounded
/// card, rows have a hairline and a hover wash (the <c>ledger-row</c> style), amounts are right aligned in the
/// semantic color (expense rose, income green), and the New / Register button is the one primary action of the screen.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class ListDesignHeadlessTests
{
    private static readonly Guid _food = new("11111111-1111-1111-1111-000000000001");

    private static TextBlock Amount(Control view, string text) =>
        view.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == text);

    [Fact]
    public async Task Expenses_TheListIsARoundedCard_WithHairlineRows_AndRoseRightAlignedAmounts()
    {
        (CornerRadius radius, int rows, HorizontalAlignment align, Color? color, int primaries) = await OnUi(async () =>
        {
            var repo = new FakeExpenseRepository { PageAddedItems = true };
            await repo.AddAsync(Expense.Create(_food, new DateOnly(2026, 1, 2), Money.Create(15.99m, Currency.DOP), "Almuerzo", null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
            var categories = new FakeCategoryRepository();
            categories.SetCategories([new Category { Id = _food, Name = "Comida" }]);
            ExpensesViewModel vm = ExpensesViewModelTests.CreateViewModel(expenseRepo: repo, categoryRepo: categories, localizerOverride: new ResourceStringLocalizer());
            var view = new ExpensesView();
            Window window = HeadlessUi.Show(view, vm);
            try
            {
                await WaitUntil(() => vm.HasLoaded && vm.Expenses.Count == 1);
                Border card = view.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("card") && b.GetVisualDescendants().OfType<Border>().Any(r => r.Classes.Contains("ledger-row")));
                int rows = card.GetVisualDescendants().OfType<Border>().Count(r => r.Classes.Contains("ledger-row"));
                TextBlock amount = Amount(view, "15.99 DOP");
                int primaries = view.GetVisualDescendants().OfType<Button>().Count(b => b.Classes.Contains("ledger-btn-primary") && b.IsEffectivelyVisible);
                return (card.CornerRadius, rows, amount.HorizontalAlignment, (amount.Foreground as ISolidColorBrush)?.Color, primaries);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(new CornerRadius(16), radius);
        Assert.Equal(1, rows);
        Assert.Equal(HorizontalAlignment.Right, align);
        Assert.Equal(Color.Parse(ChartPalette.ExpenseHex), color);
        Assert.Equal(1, primaries);
    }

    [Fact]
    public async Task Income_TheAmountsAreGreen_AndRightAligned_AndTheScreenHasOnePrimaryAction()
    {
        (HorizontalAlignment align, Color? color, int primaries) = await OnUi(async () =>
        {
            var repo = new FakeIncomeRepository { PageAddedItems = true };
            await repo.AddAsync(Income.Create(null, new DateOnly(2026, 1, 2), Money.Create(1500m, Currency.DOP), "Salario", null, null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
            IncomeViewModel vm = IncomeViewModelTests.CreateViewModel(incomeRepo: repo, localizerOverride: new ResourceStringLocalizer());
            var view = new IncomeView();
            Window window = HeadlessUi.Show(view, vm);
            try
            {
                await WaitUntil(() => vm.HasLoaded && vm.Incomes.Count == 1);
                TextBlock amount = Amount(view, "1,500.00 DOP");
                int primaries = view.GetVisualDescendants().OfType<Button>().Count(b => b.Classes.Contains("ledger-btn-primary") && b.IsEffectivelyVisible);
                return (amount.HorizontalAlignment, (amount.Foreground as ISolidColorBrush)?.Color, primaries);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal(HorizontalAlignment.Right, align);
        Assert.Equal(Color.Parse(ChartPalette.IncomeHex), color);
        Assert.Equal(1, primaries);
    }
}
