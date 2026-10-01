using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Controls;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// The REAL <see cref="DashboardView"/> with the REAL <see cref="DashboardViewModel"/> and real queries in the shared
/// headless session: the "expenses of the month" list is rendered, ordered, capped, follows the month navigation,
/// shows the empty state, navigates on "view all" and survives a load failure.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class DashboardMonthExpensesViewHeadlessTests
{
    private static readonly Guid _foodId = Guid.NewGuid();
    private static readonly DateOnly _today = new(2026, 3, 15);

    private static Task<T> OnUi<T>(string culture, Func<Task<T>> body) =>
        HeadlessUi.OnUiThread(async () =>
        {
            CultureInfo previous = CultureInfo.CurrentUICulture;
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            try
            {
                return await body();
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
                CultureInfo.CurrentCulture = previousCulture;
            }
        });

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 300 && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        HeadlessUi.Flush();
    }

    private static async Task Settled(DashboardViewModel vm)
    {
        await WaitUntil(() => !vm.IsLoading);
        HeadlessUi.Flush();
    }

    private sealed class Rig : IDisposable
    {
        private readonly HeadlessUi.Scope _scope;

        public Rig(Action<FakeExpenseRepository>? seed, double width = 1100)
        {
            Expenses = new FakeExpenseRepository { PageAddedItems = true };
            Expenses.CategoryNames[_foodId] = "Comida";
            seed?.Invoke(Expenses);

            FakeCategoryRepository categories = new();
            categories.SetCategories([new Category { Id = _foodId, Name = "Comida" }]);

            ExpensesVm = ExpensesViewModelTests.CreateViewModel();
            Navigation = new FakeNavigationService(new Dictionary<string, ViewModelBase>(StringComparer.Ordinal)
            {
                { RouteNames.Expenses, ExpensesVm },
            });

            Vm = DashboardViewModelTests.CreateViewModel(
                Expenses, new FakeIncomeRepository(), _today, categoryRepo: categories, navigation: Navigation);
            View = new DashboardView();
            Window window = HeadlessUi.Show(View, Vm);
            window.Width = width;
            _scope = new HeadlessUi.Scope(window);
            HeadlessUi.Flush();
        }

        public FakeExpenseRepository Expenses { get; }

        public FakeNavigationService Navigation { get; }

        public ExpensesViewModel ExpensesVm { get; }

        public DashboardViewModel Vm { get; }

        public DashboardView View { get; }

        public void Dispose() => _scope.Dispose();
    }

    private static void Add(FakeExpenseRepository repo, DateOnly date, decimal amount, string description)
        => repo.AddAsync(Expense.Create(
            _foodId,
            date,
            Money.Create(amount, Currency.DOP),
            description,
            null,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc))).GetAwaiter().GetResult();

    private static string[] VisibleTexts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty)];

    private static EmptyState[] VisibleEmptyStates(Control root) =>
        [.. root.GetVisualDescendants().OfType<EmptyState>().Where(e => e.IsEffectivelyVisible)];

    private static Button ViewAllButton(Control root) =>
        root.GetVisualDescendants().OfType<Button>()
            .Single(b => AutomationProperties.GetName(b) == Strings.DashboardMonthExpensesViewAllAutomationName);

    private static double TopOf(Control root, TextBlock block) =>
        block.TranslatePoint(default, root)!.Value.Y;

    private static TextBlock Block(Control root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == text);

    [Theory]
    [InlineData("en")]
    [InlineData("es-DO")]
    public async Task TheList_ShowsTheMonthsExpensesNewestFirst_WithCountAndViewAll_InBothLocales(string culture)
    {
        (string[] Texts, string[] Order, bool ViewAllVisible, int EmptyStates, string Title, string Count) result = await OnUi(culture, async () =>
        {
            using Rig rig = new(e =>
            {
                Add(e, new DateOnly(2026, 3, 2), 100m, "Supermercado");
                Add(e, new DateOnly(2026, 3, 12), 1234.5m, "Gasolina");
                Add(e, new DateOnly(2026, 2, 27), 999m, "Del mes pasado");
            });
            await Settled(rig.Vm);

            string[] texts = VisibleTexts(rig.View);
            string[] order = [.. new[] { "Gasolina", "Supermercado" }.OrderBy(t => TopOf(rig.View, Block(rig.View, t)))];
            return (texts, order, ViewAllButton(rig.View).IsEffectivelyVisible, VisibleEmptyStates(rig.View).Length, Strings.DashboardMonthExpensesTitle, rig.Vm.MonthExpensesCountText);
        });

        Assert.Contains(result.Title, result.Texts);
        Assert.Contains(result.Count, result.Texts);
        Assert.Contains("Comida", result.Texts);
        Assert.Contains($"{1234.5m:N2} DOP", result.Texts);
        Assert.DoesNotContain("Del mes pasado", result.Texts);
        Assert.Equal(new[] { "Gasolina", "Supermercado" }, result.Order);
        Assert.True(result.ViewAllVisible);
        Assert.Equal(0, result.EmptyStates);
        Assert.Equal(culture == "en" ? "Expenses this month" : "Gastos del mes", result.Title);
    }

    [Fact]
    public async Task TheSection_SitsRightUnderTheSummaryCards_AboveTheCharts()
    {
        (double Summary, double Section, double Charts) result = await OnUi("en", async () =>
        {
            using Rig rig = new(e => Add(e, new DateOnly(2026, 3, 2), 100m, "Supermercado"));
            await Settled(rig.Vm);

            return (
                TopOf(rig.View, Block(rig.View, Strings.DashboardNetBalanceLabel)),
                TopOf(rig.View, Block(rig.View, Strings.DashboardMonthExpensesTitle)),
                TopOf(rig.View, Block(rig.View, Strings.DashboardCategoryBreakdownTitle)));
        });

        Assert.True(result.Summary < result.Section, "the list must come after the summary cards");
        Assert.True(result.Section < result.Charts, "the list must come before the charts");
    }

    [Fact]
    public async Task MoreThanTheCap_RendersEightRows_AndTheShowingNote()
    {
        (int Rows, bool HasNote, bool Newest, bool Ninth) result = await OnUi("en", async () =>
        {
            using Rig rig = new(e =>
            {
                for (int day = 1; day <= 11; day++)
                {
                    Add(e, new DateOnly(2026, 3, day), day, $"Gasto {day}");
                }
            });
            await Settled(rig.Vm);

            string[] texts = VisibleTexts(rig.View);
            return (
                texts.Count(t => t.StartsWith("Gasto ", StringComparison.Ordinal)),
                texts.Contains(string.Format(CultureInfo.CurrentCulture, Strings.DashboardMonthExpensesShowingFormat, 8)),
                texts.Contains("Gasto 11"),
                texts.Contains("Gasto 3"));
        });

        Assert.Equal(8, result.Rows);
        Assert.True(result.HasNote);
        Assert.True(result.Newest);
        Assert.False(result.Ninth);
    }

    [Fact]
    public async Task MonthNavigation_SwapsTheRenderedRows()
    {
        (string[] March, string[] February) result = await OnUi("en", async () =>
        {
            using Rig rig = new(e =>
            {
                Add(e, new DateOnly(2026, 3, 2), 100m, "Gasto de marzo");
                Add(e, new DateOnly(2026, 2, 20), 50m, "Gasto de febrero");
            });
            await Settled(rig.Vm);
            string[] march = VisibleTexts(rig.View);

            rig.Vm.PreviousMonthCommand.Execute(null);
            await Settled(rig.Vm);
            return (march, VisibleTexts(rig.View));
        });

        Assert.Contains("Gasto de marzo", result.March);
        Assert.DoesNotContain("Gasto de febrero", result.March);
        Assert.Contains("Gasto de febrero", result.February);
        Assert.DoesNotContain("Gasto de marzo", result.February);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es-DO")]
    public async Task AMonthWithoutExpenses_ShowsTheLocalizedEmptyState_AndNoRowsOrViewAll(string culture)
    {
        (string[] Titles, string[] Hints, bool ViewAllVisible, string Expected, string ExpectedHint) result = await OnUi(culture, async () =>
        {
            using Rig rig = new(e => Add(e, new DateOnly(2026, 2, 20), 50m, "Solo febrero"));
            await Settled(rig.Vm);

            EmptyState[] states = VisibleEmptyStates(rig.View);
            return (
                states.Select(s => s.Title ?? string.Empty).ToArray(),
                states.Select(s => s.Hint ?? string.Empty).ToArray(),
                ViewAllButton(rig.View).IsEffectivelyVisible,
                Strings.DashboardMonthExpensesEmptyTitle,
                Strings.DashboardMonthExpensesEmptyHint);
        });

        Assert.Equal(new[] { result.Expected }, result.Titles);
        Assert.Equal(new[] { result.ExpectedHint }, result.Hints);
        Assert.False(result.ViewAllVisible);
        Assert.Equal(culture == "en" ? "No expenses in this month" : "Sin gastos en este mes", result.Expected);
    }

    [Fact]
    public async Task ViewAll_Click_NavigatesToExpensesFilteredToTheDisplayedMonth()
    {
        (string[] Calls, DateOnly? From, DateOnly? To) result = await OnUi("en", async () =>
        {
            using Rig rig = new(e => Add(e, new DateOnly(2026, 2, 20), 50m, "Febrero"));
            await Settled(rig.Vm);
            rig.Vm.PreviousMonthCommand.Execute(null);
            await Settled(rig.Vm);

            Button viewAll = ViewAllButton(rig.View);
            Assert.True(viewAll.Command!.CanExecute(viewAll.CommandParameter));
            viewAll.Command.Execute(viewAll.CommandParameter);
            HeadlessUi.Flush();

            return (
                rig.Navigation.NavigateToTypedCalls.ToArray(),
                rig.ExpensesVm.CurrentFilter.DateFrom,
                rig.ExpensesVm.CurrentFilter.DateTo);
        });

        Assert.Equal(new[] { RouteNames.Expenses }, result.Calls);
        Assert.Equal(new DateOnly(2026, 2, 1), result.From);
        Assert.Equal(new DateOnly(2026, 2, 28), result.To);
    }

    [Fact]
    public async Task ALoadFailure_ShowsTheLocalizedError_NotTheEmptyState_AndTheSummaryStillRenders()
    {
        (string[] Texts, int EmptyStates, string Error) result = await OnUi("en", async () =>
        {
            using Rig rig = new(e => Add(e, new DateOnly(2026, 3, 2), 100m, "Marzo"));
            rig.Expenses.SetThrowOnGetFilter(new InvalidOperationException("db down"));
            rig.Vm.RefreshCommand.Execute(null);
            await Settled(rig.Vm);

            return (VisibleTexts(rig.View), VisibleEmptyStates(rig.View).Length, Strings.DashboardLoadErrorMessage);
        });

        Assert.Contains(result.Error, result.Texts);
        Assert.Equal(0, result.EmptyStates);
        Assert.DoesNotContain("Marzo", result.Texts);
    }

    [Fact]
    public async Task AtANarrowWidth_TheRowsStayInsideTheViewport()
    {
        (double Right, double Limit) result = await OnUi("en", async () =>
        {
            using Rig rig = new(
                e => Add(e, new DateOnly(2026, 3, 2), 100m, "Una descripcion bastante larga para obligar a que la fila se ajuste al ancho disponible de la ventana"),
                width: 520);
            await Settled(rig.Vm);

            TextBlock description = Block(rig.View, "Una descripcion bastante larga para obligar a que la fila se ajuste al ancho disponible de la ventana");
            TextBlock amount = Block(rig.View, $"{100m:N2} DOP");
            double right = amount.TranslatePoint(new Point(amount.Bounds.Width, 0), rig.View)!.Value.X;
            return (Math.Max(right, description.TranslatePoint(new Point(description.Bounds.Width, 0), rig.View)!.Value.X), rig.View.Bounds.Width);
        });

        Assert.True(result.Right <= result.Limit + 0.5, $"row content ends at {result.Right} but the viewport is {result.Limit} wide");
    }
}
