using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S7b (R-12, R-14, DD-8, T8): the primary button of each form is the window default button, so Enter inside
/// an input submits. Real views over real view-models and use cases (only the repositories are the existing doubles).
/// The default must follow the mode (register vs update, never both), the import Confirm button must never be a
/// default (it writes data) and an open error overlay must keep Enter from reaching the form behind it.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class DefaultButtonTests
{
    private static readonly Guid _categoryId = new("22222222-2222-2222-2222-000000000001");

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 300 && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        HeadlessUi.Flush();
    }

    private static void PressEnter(Window window)
    {
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        HeadlessUi.Flush();
    }

    private static Button ButtonFor(Control root, System.Windows.Input.ICommand command) =>
        root.GetVisualDescendants().OfType<Button>().Single(b => ReferenceEquals(b.Command, command));

    private static TextBox Box(Control root, string watermark) =>
        root.GetVisualDescendants().OfType<TextBox>().First(t => t.Watermark == watermark);

    private static FakeCategoryRepository Categories()
    {
        var categories = new FakeCategoryRepository();
        categories.SetCategories([new Category { Id = _categoryId, Name = "Comida", IsSystem = false }]);
        return categories;
    }

    [Fact]
    public async Task Expenses_TheDefaultButtonFollowsTheMode_AndEnterInsideAnInputSubmitsIt()
    {
        (bool RegisterDefault, bool UpdateDefaultInRegister, int AddedByEnter, bool RegisterInEdit, bool UpdateInEdit, int CountAfterUpdate, string? Description) result =
            await HeadlessUi.OnUiThread(async () =>
            {
                var repository = new FakeExpenseRepository { PageAddedItems = true };
                ExpensesViewModel vm = ExpensesViewModelTests.CreateViewModel(expenseRepo: repository, categoryRepo: Categories());
                var view = new ExpensesView();
                Window window = HeadlessUi.Show(view, vm);
                try
                {
                    await WaitUntil(() => vm.HasLoaded && vm.Categories.Count > 0);
                    Button register = ButtonFor(view, vm.RegisterExpenseCommand);
                    Button update = ButtonFor(view, vm.UpdateExpenseCommand);
                    bool registerDefault = register.IsDefault;
                    bool updateDefaultInRegister = update.IsDefault;

                    vm.SelectedCategoryId = _categoryId;
                    vm.Amount = 100m;
                    vm.Description = "Almuerzo";
                    Box(view, Strings.ExpensesDescriptionPlaceholder).Focus();
                    HeadlessUi.Flush();
                    PressEnter(window);
                    await WaitUntil(() => repository.AddedExpenses.Count == 1 && vm.Expenses.Count == 1);
                    int addedByEnter = repository.AddedExpenses.Count;

                    vm.BeginEditCommand.Execute(vm.Expenses[0]);
                    HeadlessUi.Flush();
                    bool registerInEdit = register.IsDefault;
                    bool updateInEdit = update.IsDefault;

                    vm.Description = "Editado";
                    Box(view, Strings.ExpensesDescriptionPlaceholder).Focus();
                    HeadlessUi.Flush();
                    PressEnter(window);
                    await WaitUntil(() => repository.AddedExpenses.Any(e => e.Description == "Editado"));

                    return (registerDefault, updateDefaultInRegister, addedByEnter, registerInEdit, updateInEdit, repository.AddedExpenses.Count, repository.AddedExpenses.SingleOrDefault()?.Description);
                }
                finally
                {
                    window.Close();
                    HeadlessUi.Flush();
                }
            });

        Assert.True(result.RegisterDefault);
        Assert.False(result.UpdateDefaultInRegister);
        Assert.Equal(1, result.AddedByEnter);
        Assert.False(result.RegisterInEdit, "Register must not stay the default in edit mode.");
        Assert.True(result.UpdateInEdit);
        Assert.Equal(1, result.CountAfterUpdate);
        Assert.Equal("Editado", result.Description);
    }

    [Fact]
    public async Task Income_TheDefaultButtonFollowsTheMode_AndEnterInsideAnInputSubmitsIt()
    {
        (bool RegisterDefault, bool UpdateDefaultInRegister, int AddedByEnter, bool RegisterInEdit, bool UpdateInEdit, int CountAfterUpdate, string? Description) result =
            await HeadlessUi.OnUiThread(async () =>
            {
                var repository = new FakeIncomeRepository { PageAddedItems = true };
                IncomeViewModel vm = IncomeViewModelTests.CreateViewModel(incomeRepo: repository, categoryRepo: Categories());
                var view = new IncomeView();
                Window window = HeadlessUi.Show(view, vm);
                try
                {
                    await WaitUntil(() => vm.HasLoaded && vm.Categories.Count > 0);
                    Button register = ButtonFor(view, vm.RegisterIncomeCommand);
                    Button update = ButtonFor(view, vm.UpdateIncomeCommand);
                    bool registerDefault = register.IsDefault;
                    bool updateDefaultInRegister = update.IsDefault;

                    vm.SelectedCategoryId = _categoryId;
                    vm.Amount = 100m;
                    vm.Description = "Salario";
                    Box(view, Strings.IncomeDescriptionPlaceholder).Focus();
                    HeadlessUi.Flush();
                    PressEnter(window);
                    await WaitUntil(() => repository.AddedIncomes.Count == 1 && vm.Incomes.Count == 1);
                    int addedByEnter = repository.AddedIncomes.Count;

                    vm.BeginEditCommand.Execute(vm.Incomes[0]);
                    HeadlessUi.Flush();
                    bool registerInEdit = register.IsDefault;
                    bool updateInEdit = update.IsDefault;

                    vm.Description = "Editado";
                    Box(view, Strings.IncomeDescriptionPlaceholder).Focus();
                    HeadlessUi.Flush();
                    PressEnter(window);
                    await WaitUntil(() => repository.AddedIncomes.Any(e => e.Description == "Editado"));

                    return (registerDefault, updateDefaultInRegister, addedByEnter, registerInEdit, updateInEdit, repository.AddedIncomes.Count, repository.AddedIncomes.SingleOrDefault()?.Description);
                }
                finally
                {
                    window.Close();
                    HeadlessUi.Flush();
                }
            });

        Assert.True(result.RegisterDefault);
        Assert.False(result.UpdateDefaultInRegister);
        Assert.Equal(1, result.AddedByEnter);
        Assert.False(result.RegisterInEdit);
        Assert.True(result.UpdateInEdit);
        Assert.Equal(1, result.CountAfterUpdate);
        Assert.Equal("Editado", result.Description);
    }

    [Fact]
    public async Task Debts_TheCreateButtonIsTheOnlyDefault_AndEnterInsideAnInputSubmitsTheForm()
    {
        (bool CreateDefault, int Defaults, string? FormErrorBefore, string? FormErrorAfter) result = await HeadlessUi.OnUiThread(async () =>
        {
            DebtsViewModel vm = DebtsViewModelTests.CreateViewModel(new FakeDebtRepository(), new FakeDebtPaymentRepository());
            var view = new DebtsView();
            Window window = HeadlessUi.Show(view, vm);
            try
            {
                await WaitUntil(() => vm.HasLoaded);
                Button create = ButtonFor(view, vm.CreateCommand);
                int defaults = view.GetVisualDescendants().OfType<Button>().Count(b => b.IsDefault);
                string? before = vm.FormError;

                Box(view, Strings.DebtsDescriptionPlaceholder).Focus();
                HeadlessUi.Flush();
                PressEnter(window);
                await WaitUntil(() => !string.IsNullOrEmpty(vm.FormError));

                return (create.IsDefault, defaults, before, vm.FormError);
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.True(result.CreateDefault);
        Assert.Equal(1, result.Defaults);
        Assert.True(string.IsNullOrEmpty(result.FormErrorBefore));
        Assert.False(string.IsNullOrEmpty(result.FormErrorAfter), "Enter in the Description input must submit the (invalid) form and report it.");
    }

    [Fact]
    public async Task Recurring_TheSaveButtonIsTheOnlyDefault_AndEnterInsideAnInputSubmitsTheForm()
    {
        (bool SaveDefault, bool ConfirmDefault, int Defaults, string? FormErrorBefore, string? FormErrorAfter) result = await HeadlessUi.OnUiThread(async () =>
        {
            DebtsRecurringLocalizationTests.RecurringRig rig = await DebtsRecurringLocalizationTests.NewRecurringRig(delay: false);
            var view = new RecurringRulesView();
            Window window = HeadlessUi.Show(view, rig.Vm);
            try
            {
                await WaitUntil(() => rig.Vm.HasLoaded);
                Button save = ButtonFor(view, rig.Vm.SaveCommand);
                Button confirm = ButtonFor(view, rig.Vm.ConfirmGenerationCommand);
                int defaults = view.GetVisualDescendants().OfType<Button>().Count(b => b.IsDefault);
                string? before = rig.Vm.FormError;

                Box(view, Strings.RecurringNamePlaceholder).Focus();
                HeadlessUi.Flush();
                PressEnter(window);
                await WaitUntil(() => !string.IsNullOrEmpty(rig.Vm.FormError));

                return (save.IsDefault, confirm.IsDefault, defaults, before, rig.Vm.FormError);
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.True(result.SaveDefault);
        Assert.False(result.ConfirmDefault, "Generating the recurring expenses writes data and must not be the Enter default.");
        Assert.Equal(1, result.Defaults);
        Assert.True(string.IsNullOrEmpty(result.FormErrorBefore));
        Assert.False(string.IsNullOrEmpty(result.FormErrorAfter));
    }

    [Fact]
    public async Task TheImportButtons_AreNeverDefault_InAnyState()
    {
        (int Visible, int ImportDefaults, int TotalDefaults) result = await HeadlessUi.OnUiThread(async () =>
        {
            var rig = new ExpensesViewModelImportTests.Rig();
            var view = new ExpensesView();
            Window window = HeadlessUi.Show(view, rig.ViewModel);
            try
            {
                await WaitUntil(() => rig.ViewModel.HasLoaded);
                rig.ViewModel.OpenImportCommand.Execute(null);
                await rig.Import.ChooseFileCommand.ExecuteAsync(null);
                HeadlessUi.Flush();
                Assert.Equal(ImportPanelState.Previewed, rig.Import.State);

                var importButtons = view.GetVisualDescendants().OfType<ImportView>()
                    .SelectMany(i => i.GetVisualDescendants().OfType<Button>())
                    .Where(b => b.IsEffectivelyVisible)
                    .ToList();
                return (importButtons.Count, importButtons.Count(b => b.IsDefault), view.GetVisualDescendants().OfType<Button>().Count(b => b.IsDefault));
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.True(result.Visible >= 2, "The previewed import panel shows its buttons (including Confirm), so the check is not vacuous.");
        Assert.Equal(0, result.ImportDefaults);
        Assert.Equal(1, result.TotalDefaults);
        Assert.DoesNotContain("IsDefault", File.ReadAllText(Packaging.RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Views", "ImportView.axaml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOpenErrorOverlay_KeepsEnterFromReachingTheFormBehindIt()
    {
        (int WhileOpen, bool OverlayClosed, int AfterClose) result = await HeadlessUi.OnUiThread(async () =>
        {
            var repository = new FakeExpenseRepository { PageAddedItems = true };
            ExpensesViewModel expenses = ExpensesViewModelTests.CreateViewModel(expenseRepo: repository, categoryRepo: Categories());
            var overlay = new ErrorOverlayViewModel(new FakeClipboardService(), "1.0.0");
            var navigation = new FakeNavigationService(new Dictionary<string, ViewModelBase> { [RouteNames.Expenses] = expenses });
            var shell = new ShellViewModel(
                navigation,
                new FakeStringLocalizer<Strings>(new Dictionary<string, string>()),
                new NotificationListViewModel(
                    new FakeNotificationCenter(),
                    new FakeClock { Today = new DateOnly(2026, 9, 20) },
                    NullLogger<NotificationListViewModel>.Instance),
                overlay);
            var window = new MainWindow { DataContext = shell, Width = 1100, Height = 900 };
            window.Show();
            navigation.NavigateTo(RouteNames.Expenses);
            HeadlessUi.Flush();
            try
            {
                await WaitUntil(() => expenses.HasLoaded && expenses.Categories.Count > 0);
                expenses.SelectedCategoryId = _categoryId;
                expenses.Amount = 100m;
                expenses.Description = "Almuerzo";
                HeadlessUi.Flush();

                overlay.Present(Guid.NewGuid(), DateTime.UtcNow);
                HeadlessUi.Flush();
                // The overlay moves focus to its Close button, which would swallow Enter by itself; clear it so the
                // only thing keeping Enter away from the default button behind is the disabled shell.
                window.FocusManager!.ClearFocus();
                HeadlessUi.Flush();
                PressEnter(window);
                await Task.Delay(100);
                int whileOpen = repository.AddedExpenses.Count;

                window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                HeadlessUi.Flush();
                bool closed = !overlay.IsOpen;

                // The overlay does not restore focus when it closes (it stays on the hidden Close button), so the
                // positive control puts the focus back in the form the way a user would.
                Box(window, Strings.ExpensesDescriptionPlaceholder).Focus();
                HeadlessUi.Flush();
                PressEnter(window);
                await WaitUntil(() => repository.AddedExpenses.Count == 1);

                return (whileOpen, closed, repository.AddedExpenses.Count);
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.Equal(0, result.WhileOpen);
        Assert.True(result.OverlayClosed);
        Assert.Equal(1, result.AfterClose);
    }
}
