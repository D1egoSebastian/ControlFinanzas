using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using static ControlDeFinanzasIA.Tests.Unit.UI.LedgerViewKit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Pre-1.0 slice E (E2): the filter panel of the Expenses and Income lists sits behind a "Filters" toggle. It is closed
/// by default, the toggle is a real keyboard-reachable control (Space opens it, Tab then walks the filter fields), and
/// a filter that is active while the panel is closed stays visible as a summary with a clear action, so a hidden
/// filter never silently narrows the list.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class FilterPanelToggleTests
{
    private static readonly Guid _food = new("11111111-1111-1111-1111-000000000001");

    private sealed class ExpensesRig : IDisposable
    {
        public ExpensesRig(ExpensesViewModel vm)
        {
            Vm = vm;
            View = new ExpensesView();
            Window = HeadlessUi.Show(View, vm);
        }

        public ExpensesViewModel Vm { get; }

        public ExpensesView View { get; }

        public Window Window { get; }

        public void Dispose()
        {
            Window.Close();
            HeadlessUi.Flush();
        }
    }

    private sealed class IncomeRig : IDisposable
    {
        public IncomeRig(IncomeViewModel vm)
        {
            Vm = vm;
            View = new IncomeView();
            Window = HeadlessUi.Show(View, vm);
        }

        public IncomeViewModel Vm { get; }

        public IncomeView View { get; }

        public Window Window { get; }

        public void Dispose()
        {
            Window.Close();
            HeadlessUi.Flush();
        }
    }

    private static async Task<ExpensesRig> NewExpensesRig()
    {
        var repo = new FakeExpenseRepository { PageAddedItems = true };
        for (int i = 0; i < 3; i++)
        {
            await repo.AddAsync(Expense.Create(_food, new DateOnly(2026, 1, 1 + i), Money.Create(10m + i, Currency.DOP), $"Gasto {i}", null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(i)));
        }

        var categories = new FakeCategoryRepository();
        categories.SetCategories([new Category { Id = _food, Name = "Comida" }]);
        ExpensesViewModel vm = ExpensesViewModelTests.CreateViewModel(expenseRepo: repo, categoryRepo: categories, localizerOverride: new ResourceStringLocalizer());
        var rig = new ExpensesRig(vm);
        await WaitUntil(() => vm.HasLoaded && vm.Categories.Count > 0);
        return rig;
    }

    private static async Task<IncomeRig> NewIncomeRig()
    {
        var repo = new FakeIncomeRepository { PageAddedItems = true };
        for (int i = 0; i < 3; i++)
        {
            await repo.AddAsync(Income.Create(_food, new DateOnly(2026, 1, 1 + i), Money.Create(10m + i, Currency.DOP), $"Ingreso {i}", null, null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(i)));
        }

        var categories = new FakeCategoryRepository();
        categories.SetCategories([new Category { Id = _food, Name = "Comida" }]);
        IncomeViewModel vm = IncomeViewModelTests.CreateViewModel(incomeRepo: repo, categoryRepo: categories, localizerOverride: new ResourceStringLocalizer());
        var rig = new IncomeRig(vm);
        await WaitUntil(() => vm.HasLoaded && vm.Categories.Count > 0);
        return rig;
    }

    private static ToggleButton Toggle(Control view) =>
        view.GetVisualDescendants().OfType<ToggleButton>().Single(t => AutomationProperties.GetName(t) == Strings.FiltersSectionTitle);

    private static TextBox Search(Control view) =>
        view.GetVisualDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == Strings.FilterSearchLabel);

    private static void PressSpace(Window window)
    {
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        HeadlessUi.Flush();
    }

    [Fact]
    public async Task Expenses_ThePanelIsClosedByDefault_AndTheCommandTogglesIt()
    {
        (bool initial, bool afterOne, bool afterTwo) = await OnUi(async () =>
        {
            using ExpensesRig rig = await NewExpensesRig();
            bool initial = rig.Vm.IsFilterPanelOpen;
            rig.Vm.ToggleFilterPanelCommand.Execute(null);
            bool one = rig.Vm.IsFilterPanelOpen;
            rig.Vm.ToggleFilterPanelCommand.Execute(null);
            return (initial, one, rig.Vm.IsFilterPanelOpen);
        });

        Assert.False(initial);
        Assert.True(afterOne);
        Assert.False(afterTwo);
    }

    [Fact]
    public async Task Income_ThePanelIsClosedByDefault_AndTheCommandTogglesIt()
    {
        (bool initial, bool afterOne, bool afterTwo) = await OnUi(async () =>
        {
            using IncomeRig rig = await NewIncomeRig();
            bool initial = rig.Vm.IsFilterPanelOpen;
            rig.Vm.ToggleFilterPanelCommand.Execute(null);
            bool one = rig.Vm.IsFilterPanelOpen;
            rig.Vm.ToggleFilterPanelCommand.Execute(null);
            return (initial, one, rig.Vm.IsFilterPanelOpen);
        });

        Assert.False(initial);
        Assert.True(afterOne);
        Assert.False(afterTwo);
    }

    [Fact]
    public async Task Expenses_TheSpaceKeyOnTheToggleOpensThePanel_TabReachesTheFirstField_AndSpaceClosesIt()
    {
        (bool closedApplyVisible, bool closedChecked, bool openApplyVisible, bool openChecked, bool searchFocused, bool closedAgain) = await OnUi(async () =>
        {
            using ExpensesRig rig = await NewExpensesRig();
            bool closedApply = VisibleButtonsFor(rig.View, rig.Vm.ApplyFilterCommand).Length > 0;
            ToggleButton toggle = Toggle(rig.View);
            bool closedChecked = toggle.IsChecked == true;

            toggle.Focus();
            PressSpace(rig.Window);
            bool openApply = VisibleButtonsFor(rig.View, rig.Vm.ApplyFilterCommand).Length > 0;
            bool openChecked = toggle.IsChecked == true;

            rig.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            HeadlessUi.Flush();
            bool searchFocused = ReferenceEquals(rig.Window.FocusManager!.GetFocusedElement(), Search(rig.View));

            toggle.Focus();
            PressSpace(rig.Window);
            return (closedApply, closedChecked, openApply, openChecked, searchFocused, !rig.Vm.IsFilterPanelOpen);
        });

        Assert.False(closedApplyVisible);
        Assert.False(closedChecked);
        Assert.True(openApplyVisible);
        Assert.True(openChecked);
        Assert.True(searchFocused, "Tab from the toggle must land on the first filter field.");
        Assert.True(closedAgain);
    }

    [Fact]
    public async Task Income_TheSpaceKeyOnTheToggleOpensThePanel_AndTabReachesTheFirstField()
    {
        (bool closedApplyVisible, bool openApplyVisible, bool searchFocused) = await OnUi(async () =>
        {
            using IncomeRig rig = await NewIncomeRig();
            bool closedApply = VisibleButtonsFor(rig.View, rig.Vm.ApplyFilterCommand).Length > 0;
            ToggleButton toggle = Toggle(rig.View);

            toggle.Focus();
            PressSpace(rig.Window);
            bool openApply = VisibleButtonsFor(rig.View, rig.Vm.ApplyFilterCommand).Length > 0;

            rig.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            HeadlessUi.Flush();
            return (closedApply, openApply, ReferenceEquals(rig.Window.FocusManager!.GetFocusedElement(), Search(rig.View)));
        });

        Assert.False(closedApplyVisible);
        Assert.True(openApplyVisible);
        Assert.True(searchFocused);
    }

    [Fact]
    public async Task Expenses_AnActiveFilterOnAClosedPanel_ShowsASummaryWithAClearAction()
    {
        (bool summaryWhileClosed, int clearButtonsClosed, bool summaryWhileOpen, bool noFilterSummary, bool toggleMarkedActive) = await OnUi(async () =>
        {
            using ExpensesRig rig = await NewExpensesRig();
            bool none = VisibleTexts(rig.View).Contains(Strings.FiltersActiveSummary);

            rig.Vm.ApplyDateRangeFilter(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2));
            await WaitUntil(() => rig.Vm.HasActiveFilter && rig.Vm.TotalCount == 2);
            bool summary = VisibleTexts(rig.View).Contains(Strings.FiltersActiveSummary);
            int clear = VisibleButtonsFor(rig.View, rig.Vm.ClearFilterCommand).Length;
            bool marked = Toggle(rig.View).Classes.Contains("active");

            rig.Vm.IsFilterPanelOpen = true;
            HeadlessUi.Flush();
            bool open = VisibleTexts(rig.View).Contains(Strings.FiltersActiveSummary);
            return (summary, clear, open, none, marked);
        });

        Assert.False(noFilterSummary);
        Assert.True(summaryWhileClosed);
        Assert.Equal(1, clearButtonsClosed);
        Assert.True(toggleMarkedActive);
        Assert.False(summaryWhileOpen, "the open panel already shows the filter; the summary is for the closed state only");
    }

    [Fact]
    public async Task Income_AnActiveFilterOnAClosedPanel_ShowsASummaryWithAClearAction()
    {
        (bool summaryWhileClosed, int clearButtonsClosed) = await OnUi(async () =>
        {
            using IncomeRig rig = await NewIncomeRig();
            rig.Vm.FilterText = "Ingreso 1";
            await rig.Vm.ApplyFilterCommand.ExecuteAsync(null);
            HeadlessUi.Flush();
            return (VisibleTexts(rig.View).Contains(Strings.FiltersActiveSummary), VisibleButtonsFor(rig.View, rig.Vm.ClearFilterCommand).Length);
        });

        Assert.True(summaryWhileClosed);
        Assert.Equal(1, clearButtonsClosed);
    }
}
