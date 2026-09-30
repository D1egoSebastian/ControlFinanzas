using Avalonia.Controls;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Headless Avalonia regression tests for the date pickers of the data-entry forms (Phase 20 verification,
/// pre-existing defect). <c>DatePicker.SelectedDate</c> is a <see cref="DateTimeOffset"/>; a view-model date typed
/// <see cref="DateOnly"/> never connects to it without a converter, so a date the user picked was silently
/// ignored and the form saved the view-model's default date. Each test renders the REAL view over the REAL
/// view-model and proves both directions: the view-model value reaches the picker AND a picked value reaches the
/// view-model. The end-to-end tests prove the saved expense or income carries the picked date.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class DatePickerBindingHeadlessTests
{
    private static readonly DateOnly _initial = new(2026, 9, 30);
    private static readonly DateOnly _fromViewModel = new(2026, 3, 5);
    private static readonly DateOnly _picked = new(2026, 4, 6);
    private static readonly Guid _categoryId = new("11111111-1111-1111-1111-000000000001");

    private static Task<T> OnUiThread<T>(Func<Task<T>> body) => HeadlessUi.OnUiThread(body);

    private static void Flush() => HeadlessUi.Flush();

    private static DatePicker OnlyPicker(Control view) => view.GetVisualDescendants().OfType<DatePicker>().Single();

    private static DateTimeOffset At(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue));

    private static Category Comida() => new() { Id = _categoryId, Name = "Comida", IsSystem = false };

    [Fact]
    public async Task ExpensesView_DatePicker_IsTwoWayWithTheViewModelDate()
    {
        bool ok = await OnUiThread(async () =>
        {
            var rig = new ExpensesViewModelImportTests.Rig();
            var view = new ExpensesView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, rig.ViewModel));
            DatePicker picker = OnlyPicker(view);

            Assert.Equal(_initial, rig.ViewModel.Date);
            Assert.Equal(_initial.ToDateTime(TimeOnly.MinValue), picker.SelectedDate?.DateTime);

            rig.ViewModel.Date = _fromViewModel;
            Flush();
            Assert.Equal(_fromViewModel.ToDateTime(TimeOnly.MinValue), picker.SelectedDate?.DateTime);

            picker.SelectedDate = At(_picked);
            Flush();
            Assert.Equal(_picked, rig.ViewModel.Date);
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task ExpensesView_AnExpenseIsSavedWithTheDatePickedInThePicker()
    {
        bool ok = await OnUiThread(async () =>
        {
            var rig = new ExpensesViewModelImportTests.Rig();
            rig.Categories.SetCategories([Comida()]);
            var view = new ExpensesView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, rig.ViewModel));

            OnlyPicker(view).SelectedDate = At(_picked);
            Flush();
            rig.ViewModel.SelectedCategoryId = _categoryId;
            rig.ViewModel.Amount = 100m;
            rig.ViewModel.Description = "Almuerzo";
            await rig.ViewModel.RegisterExpenseCommand.ExecuteAsync(null);

            Assert.Equal(_picked, Assert.Single(rig.Expenses.AddedExpenses).Date);
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task IncomeView_DatePicker_IsTwoWayWithTheViewModelDate()
    {
        bool ok = await OnUiThread(async () =>
        {
            IncomeViewModel vm = IncomeViewModelTests.CreateViewModel();
            var view = new IncomeView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            DatePicker picker = OnlyPicker(view);

            Assert.NotNull(vm.Date);
            Assert.Equal(vm.Date!.Value.ToDateTime(TimeOnly.MinValue), picker.SelectedDate?.DateTime);

            vm.Date = _fromViewModel;
            Flush();
            Assert.Equal(_fromViewModel.ToDateTime(TimeOnly.MinValue), picker.SelectedDate?.DateTime);

            picker.SelectedDate = At(_picked);
            Flush();
            Assert.Equal(_picked, vm.Date);
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task IncomeView_AnIncomeIsSavedWithTheDatePickedInThePicker()
    {
        bool ok = await OnUiThread(async () =>
        {
            var categories = new FakeCategoryRepository();
            categories.SetCategories([Comida()]);
            var incomes = new FakeIncomeRepository();
            IncomeViewModel vm = IncomeViewModelTests.CreateViewModel(incomeRepo: incomes, categoryRepo: categories);
            var view = new IncomeView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));

            OnlyPicker(view).SelectedDate = At(_picked);
            Flush();
            vm.SelectedCategoryId = _categoryId;
            vm.Amount = 100m;
            vm.Description = "Sueldo";
            await vm.RegisterIncomeCommand.ExecuteAsync(null);

            Assert.Equal(_picked, Assert.Single(incomes.AddedIncomes).Date);
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task DebtsView_StartDatePicker_IsTwoWayWithTheViewModelStartDate()
    {
        bool ok = await OnUiThread(async () =>
        {
            DebtsViewModel vm = DebtsViewModelTests.CreateViewModel(new FakeDebtRepository(), new FakeDebtPaymentRepository());
            var view = new DebtsView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            DatePicker picker = OnlyPicker(view);

            Assert.Equal(DateTime.Today, picker.SelectedDate?.DateTime);

            vm.StartDate = _fromViewModel.ToDateTime(TimeOnly.MinValue);
            Flush();
            Assert.Equal(_fromViewModel.ToDateTime(TimeOnly.MinValue), picker.SelectedDate?.DateTime);

            picker.SelectedDate = At(_picked);
            Flush();
            Assert.Equal(_picked.ToDateTime(TimeOnly.MinValue), vm.StartDate);
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task RecurringRulesView_StartDatePicker_IsTwoWayWithTheViewModelStartDate()
    {
        bool ok = await OnUiThread(async () =>
        {
            RecurringRulesViewModel vm = RowTemplateBindingHeadlessTests.NewRecurringRulesViewModel();
            var view = new RecurringRulesView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            DatePicker picker = OnlyPicker(view);

            Assert.Equal(DateTime.Today, picker.SelectedDate?.DateTime);

            vm.StartDate = _fromViewModel.ToDateTime(TimeOnly.MinValue);
            Flush();
            Assert.Equal(_fromViewModel.ToDateTime(TimeOnly.MinValue), picker.SelectedDate?.DateTime);

            picker.SelectedDate = At(_picked);
            Flush();
            Assert.Equal(_picked.ToDateTime(TimeOnly.MinValue), vm.StartDate);
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task GoalsView_DatePickers_StayTwoWay_WithTheSharedConverter()
    {
        bool ok = await OnUiThread(async () =>
        {
            GoalsViewModel vm = GoalsViewModelTests.CreateViewModel();
            var view = new GoalsView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            var pickers = view.GetVisualDescendants().OfType<DatePicker>().ToList();
            Assert.Equal(2, pickers.Count);

            vm.TargetDate = _fromViewModel;
            Flush();
            Assert.Contains(pickers, p => p.SelectedDate?.DateTime == _fromViewModel.ToDateTime(TimeOnly.MinValue));

            pickers[0].SelectedDate = At(_picked);
            Flush();
            Assert.Equal(_picked, vm.TargetDate);
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }
}
