using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Controls;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using static ControlDeFinanzasIA.Tests.Unit.UI.LedgerViewKit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// 1.0 blockers D1, D2 and D3 (QA audit): the REAL <see cref="ExpensesView"/> over the REAL <see cref="ExpensesViewModel"/>
/// and the real use cases, rendered headless and driven the way a user does (focus a control, press keys). The
/// view-model had paging, delete and filters since Phase 4 but no view exposed them, so every one of these tests would
/// have stayed green on the view-model side while the screen offered nothing.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class ExpensesLedgerViewHeadlessTests
{
    private static readonly Guid _food = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _transport = new("11111111-1111-1111-1111-000000000002");


    private static ExpenseDto[] Rows(ExpensesView view, ExpensesViewModel vm) =>
        [
            .. view.GetVisualDescendants().OfType<Button>()
                .Where(b => ReferenceEquals(b.Command, vm.BeginEditCommand) && b.IsEffectivelyVisible)
                .Select(b => (ExpenseDto)b.CommandParameter!),
        ];

    private static TextBox SearchBox(ExpensesView view) =>
        view.GetVisualDescendants().OfType<TextBox>().Single(t => Avalonia.Automation.AutomationProperties.GetName(t) == "Search");

    private static async Task<Rig> NewRig(int count, bool mixedCategories = false)
    {
        var repo = new FakeExpenseRepository { PageAddedItems = true };
        for (int i = 0; i < count; i++)
        {
            await repo.AddAsync(
                Expense.Create(
                    mixedCategories && i % 2 == 1 ? _transport : _food,
                    new DateOnly(2026, 1, 1).AddDays(i % 28).AddMonths(i / 28),
                    Money.Create(10m + i, Currency.DOP),
                    $"Gasto {i}",
                    null,
                    new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(i)));
        }

        var categories = new FakeCategoryRepository();
        categories.SetCategories(
        [
            new Category { Id = _food, Name = "Comida" },
            new Category { Id = _transport, Name = "Transporte" },
        ]);

        ExpensesViewModel vm = ExpensesViewModelTests.CreateViewModel(
            expenseRepo: repo,
            categoryRepo: categories,
            localizerOverride: new ResourceStringLocalizer());
        var rig = new Rig(vm, repo);
        await WaitUntil(() => vm.HasLoaded && vm.Categories.Count > 0);
        return rig;
    }

    [Fact]
    public async Task With45Expenses_PageOneShows20Rows_AndThePagerReadsPageOneOfThree()
    {
        (int Rows, string[] Texts, bool PreviousEnabled, bool NextEnabled) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(45);
            Button previous = ButtonFor(rig.View, rig.Vm.PreviousPageCommand);
            Button next = ButtonFor(rig.View, rig.Vm.NextPageCommand);
            return (Rows(rig.View, rig.Vm).Length, VisibleTexts(rig.View), previous.IsEffectivelyEnabled, next.IsEffectivelyEnabled);
        });

        Assert.Equal(20, result.Rows);
        Assert.Contains("Page 1 of 3", result.Texts);
        Assert.Contains("45 records", result.Texts);
        Assert.False(result.PreviousEnabled);
        Assert.True(result.NextEnabled);
    }

    [Fact]
    public async Task PressingNext_ShowsTheNextTwentyRows_AndTheLastPageDisablesNext()
    {
        (string[] FirstPage, string[] SecondPage, string[] ThirdPage, string[] Texts, bool NextOnLast, bool PreviousOnLast) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(45);
            string[] first = [.. Rows(rig.View, rig.Vm).Select(r => r.Description)];

            Press(rig.Window, ButtonFor(rig.View, rig.Vm.NextPageCommand));
            await WaitUntil(() => rig.Vm.Page == 2);
            string[] second = [.. Rows(rig.View, rig.Vm).Select(r => r.Description)];
            string[] texts = VisibleTexts(rig.View);

            Press(rig.Window, ButtonFor(rig.View, rig.Vm.NextPageCommand));
            await WaitUntil(() => rig.Vm.Page == 3);
            string[] third = [.. Rows(rig.View, rig.Vm).Select(r => r.Description)];

            return (first, second, third, texts, ButtonFor(rig.View, rig.Vm.NextPageCommand).IsEffectivelyEnabled, ButtonFor(rig.View, rig.Vm.PreviousPageCommand).IsEffectivelyEnabled);
        });

        Assert.Equal(20, result.SecondPage.Length);
        Assert.Empty(result.FirstPage.Intersect(result.SecondPage));
        Assert.Contains("Page 2 of 3", result.Texts);
        Assert.Equal(5, result.ThirdPage.Length);
        Assert.False(result.NextOnLast);
        Assert.True(result.PreviousOnLast);
    }

    [Fact]
    public async Task AnOldRecord_IsReachableFromTheLastPage_AndCanBeEdited()
    {
        (bool OldVisibleOnFirst, bool OldVisibleOnLast, bool Editing, string? Description) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(45);
            bool onFirst = Rows(rig.View, rig.Vm).Any(r => r.Description == "Gasto 0");

            await rig.Vm.NextPageCommand.ExecuteAsync(null);
            await rig.Vm.NextPageCommand.ExecuteAsync(null);
            HeadlessUi.Flush();
            ExpenseDto? old = Rows(rig.View, rig.Vm).SingleOrDefault(r => r.Description == "Gasto 0");
            if (old is not null)
            {
                Press(rig.Window, ButtonFor(rig.View, rig.Vm.BeginEditCommand, old));
            }

            return (onFirst, old is not null, rig.Vm.IsEditMode, rig.Vm.Description);
        });

        Assert.False(result.OldVisibleOnFirst);
        Assert.True(result.OldVisibleOnLast);
        Assert.True(result.Editing);
        Assert.Equal("Gasto 0", result.Description);
    }

    [Fact]
    public async Task Delete_NeedsAnInlineConfirmation_BeforeTheRecordGoesAway_AndTheTotalDecrements()
    {
        (string[] BeforeTexts, int ConfirmButtons, int CancelButtons, int AddedAfterArm, int AddedAfterConfirm, int Total, bool StillListed, string[] AfterTexts) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(45);
            ExpenseDto target = Rows(rig.View, rig.Vm)[0];

            Button delete = ButtonFor(rig.View, rig.Vm.DeleteExpenseCommand, target);
            string[] before = VisibleTexts(rig.View);
            Press(rig.Window, delete);

            // Armed: the same button now confirms, a cancel button appears, nothing has been deleted.
            string[] armed = VisibleTexts(rig.View);
            int confirm = armed.Count(t => t == "Confirm");
            int cancel = VisibleButtonsFor(rig.View, rig.Vm.CancelDeleteCommand).Length;
            int addedAfterArm = rig.Repo.AddedExpenses.Count;

            Press(rig.Window, ButtonFor(rig.View, rig.Vm.DeleteExpenseCommand, target));
            await WaitUntil(() => rig.Vm.TotalCount == 44);

            return (before, confirm, cancel, addedAfterArm, rig.Repo.AddedExpenses.Count, rig.Vm.TotalCount, Rows(rig.View, rig.Vm).Any(r => r.Id == target.Id), VisibleTexts(rig.View));
        });

        Assert.DoesNotContain("Confirm", result.BeforeTexts);
        Assert.Equal(1, result.ConfirmButtons);
        Assert.Equal(1, result.CancelButtons);
        Assert.Equal(45, result.AddedAfterArm);
        Assert.Equal(44, result.AddedAfterConfirm);
        Assert.Equal(44, result.Total);
        Assert.False(result.StillListed);
        Assert.Contains("44 records", result.AfterTexts);
        Assert.Contains("Transaction deleted.", result.AfterTexts);
    }

    [Fact]
    public async Task CancellingTheConfirmation_KeepsTheRecord_AndRestoresTheDeleteButton()
    {
        (int Total, bool StillListed, int CancelButtonsAfter, bool ConfirmStillShown) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(3);
            ExpenseDto target = Rows(rig.View, rig.Vm)[0];
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.DeleteExpenseCommand, target));

            Press(rig.Window, VisibleButtonsFor(rig.View, rig.Vm.CancelDeleteCommand)[0]);

            return (
                rig.Vm.TotalCount,
                Rows(rig.View, rig.Vm).Any(r => r.Id == target.Id),
                VisibleButtonsFor(rig.View, rig.Vm.CancelDeleteCommand).Length,
                VisibleTexts(rig.View).Contains("Confirm"));
        });

        Assert.Equal(3, result.Total);
        Assert.True(result.StillListed);
        Assert.Equal(0, result.CancelButtonsAfter);
        Assert.False(result.ConfirmStillShown);
    }

    [Fact]
    public async Task ArmingASecondRow_DisarmsTheFirst_SoOnlyOneRowAsksForConfirmation()
    {
        int confirmButtons = await OnUi(async () =>
        {
            using Rig rig = await NewRig(3);
            ExpenseDto[] rows = Rows(rig.View, rig.Vm);
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.DeleteExpenseCommand, rows[0]));
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.DeleteExpenseCommand, rows[1]));
            return VisibleTexts(rig.View).Count(t => t == "Confirm");
        });

        Assert.Equal(1, confirmButtons);
    }

    [Fact]
    public async Task DeletingTheLastRowOfTheLastPage_LandsOnTheNewLastPage_WithoutAnEmptyPage()
    {
        (int Rows, string Summary, int Total) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(21);
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.NextPageCommand));
            await WaitUntil(() => rig.Vm.Page == 2);
            ExpenseDto only = Rows(rig.View, rig.Vm).Single();

            Press(rig.Window, ButtonFor(rig.View, rig.Vm.DeleteExpenseCommand, only));
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.DeleteExpenseCommand, only));
            await WaitUntil(() => rig.Vm.TotalCount == 20);

            return (Rows(rig.View, rig.Vm).Length, VisibleTexts(rig.View).Single(t => t.StartsWith("Page ", StringComparison.Ordinal)), rig.Vm.TotalCount);
        });

        Assert.Equal(20, result.Rows);
        Assert.Equal("Page 1 of 1", result.Summary);
        Assert.Equal(20, result.Total);
    }

    [Fact]
    public async Task TheSearchBox_NarrowsTheList_AndClearFilterBringsEverythingBack()
    {
        (int Narrowed, string[] NarrowedTexts, int Cleared, string? SearchAfterClear) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(45);
            SearchBox(rig.View).Text = "Gasto 4";
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.ApplyFilterCommand));
            await WaitUntil(() => rig.Vm.TotalCount == 6);
            int narrowed = Rows(rig.View, rig.Vm).Length;
            string[] texts = VisibleTexts(rig.View);

            Press(rig.Window, ButtonFor(rig.View, rig.Vm.ClearFilterCommand));
            await WaitUntil(() => rig.Vm.TotalCount == 45);
            return (narrowed, texts, Rows(rig.View, rig.Vm).Length, SearchBox(rig.View).Text);
        });

        Assert.Equal(6, result.Narrowed);
        Assert.Contains("6 records", result.NarrowedTexts);
        Assert.Equal(20, result.Cleared);
        Assert.True(string.IsNullOrEmpty(result.SearchAfterClear));
    }

    [Fact]
    public async Task EnterInTheSearchBox_AppliesTheFilter_AndDoesNotRegisterTheHalfFilledForm()
    {
        (int Total, int Added) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(45);

            // A valid registration form sits right above the filter: the default button must not grab Enter.
            rig.Vm.SelectedCategoryId = _food;
            rig.Vm.Amount = 99m;
            rig.Vm.Description = "No debe registrarse";
            int addedBefore = rig.Repo.AddedExpenses.Count;

            TextBox search = SearchBox(rig.View);
            search.Text = "Gasto 4";
            search.Focus();
            rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await WaitUntil(() => rig.Vm.TotalCount == 6);

            return (rig.Vm.TotalCount, rig.Repo.AddedExpenses.Count - addedBefore);
        });

        Assert.Equal(6, result.Total);
        Assert.Equal(0, result.Added);
    }

    [Fact]
    public async Task TheCategoryAndDateRangeControls_NarrowTheList_ThroughTheViewBindings()
    {
        (int ByCategory, int ByRange, int Total) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(10, mixedCategories: true);
            ComboBox category = rig.View.GetVisualDescendants().OfType<ComboBox>()
                .Single(c => Avalonia.Automation.AutomationProperties.GetName(c) == "Filter by category");
            category.SelectedItem = rig.Vm.Categories.Single(c => c.Id == _transport);
            HeadlessUi.Flush();
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.ApplyFilterCommand));
            await WaitUntil(() => rig.Vm.TotalCount == 5);
            int byCategory = rig.Vm.TotalCount;

            DatePicker from = rig.View.GetVisualDescendants().OfType<DatePicker>()
                .Single(d => Avalonia.Automation.AutomationProperties.GetName(d) == "From");
            from.SelectedDate = new DateTimeOffset(new DateTime(2026, 1, 6));
            HeadlessUi.Flush();
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.ApplyFilterCommand));
            await WaitUntil(() => rig.Vm.TotalCount != 5);

            return (byCategory, rig.Vm.TotalCount, rig.Vm.TotalCount);
        });

        Assert.Equal(5, result.ByCategory);
        Assert.Equal(3, result.ByRange);
    }

    [Fact]
    public async Task AnInvertedDateRange_ShowsAnInlineError_AndAFilterThatMatchesNothingShowsTheFilteredEmptyState()
    {
        (string[] ErrorTexts, bool FilteredShown, bool NoDataShown, bool PagerShown) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(5);
            rig.Vm.FilterDateFrom = new DateOnly(2026, 3, 1);
            rig.Vm.FilterDateTo = new DateOnly(2026, 1, 1);
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.ApplyFilterCommand));
            string[] errorTexts = VisibleTexts(rig.View);

            rig.Vm.FilterDateFrom = null;
            rig.Vm.FilterDateTo = null;
            SearchBox(rig.View).Text = "zzz";
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.ApplyFilterCommand));
            await WaitUntil(() => rig.Vm.IsEmptyFiltered);

            EmptyState[] visible = [.. rig.View.GetVisualDescendants().OfType<EmptyState>().Where(e => e.IsEffectivelyVisible)];
            bool pager = VisibleButtonsFor(rig.View, rig.Vm.NextPageCommand).Length > 0;
            return (errorTexts, rig.Vm.IsEmptyFiltered && visible.Length == 1, rig.Vm.IsEmptyNoData, pager);
        });

        Assert.Contains("The start date cannot be later than the end date.", result.ErrorTexts);
        Assert.True(result.FilteredShown);
        Assert.False(result.NoDataShown);
        Assert.False(result.PagerShown);
    }

    [Fact]
    public async Task WithNoData_TheNoDataEmptyStateIsStillTheOnlyThingShown_AndThereIsNoPager()
    {
        (int EmptyStates, bool NoData, bool PagerShown) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(0);
            EmptyState[] visible = [.. rig.View.GetVisualDescendants().OfType<EmptyState>().Where(e => e.IsEffectivelyVisible)];
            return (visible.Length, rig.Vm.IsEmptyNoData, VisibleButtonsFor(rig.View, rig.Vm.NextPageCommand).Length > 0);
        });

        Assert.Equal(1, result.EmptyStates);
        Assert.True(result.NoData);
        Assert.False(result.PagerShown);
    }

    [Fact]
    public async Task EveryNewInteractiveControl_HasAnAutomationName()
    {
        string[] unnamed = await OnUi<string[]>(async () =>
        {
            using Rig rig = await NewRig(3);
            ExpenseDto target = Rows(rig.View, rig.Vm)[0];
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.DeleteExpenseCommand, target));

            ICommand[] commands =
            [
                rig.Vm.ApplyFilterCommand, rig.Vm.ClearFilterCommand, rig.Vm.PreviousPageCommand, rig.Vm.NextPageCommand,
                rig.Vm.DeleteExpenseCommand, rig.Vm.CancelDeleteCommand,
            ];

            return
            [
                .. rig.View.GetVisualDescendants().OfType<Button>()
                    .Where(b => b.IsEffectivelyVisible && commands.Any(c => ReferenceEquals(c, b.Command)))
                    .Where(b => string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(b)) && string.IsNullOrWhiteSpace(b.Content as string))
                    .Select(b => b.Command!.GetType().Name),
            ];
        });

        Assert.Empty(unnamed);
    }

    [Fact]
    public async Task EveryRow_ShowsItsAmountInTheOtherCurrencyAsAnApproximateLine()
    {
        string[] texts = await OnUi(async () =>
        {
            var repo = new FakeExpenseRepository { PageAddedItems = true };
            await repo.AddAsync(Expense.Create(_food, new DateOnly(2026, 1, 2), Money.Create(10m, Currency.USD), "Netflix", null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
            await repo.AddAsync(Expense.Create(_food, new DateOnly(2026, 1, 3), Money.Create(25m, Currency.DOP), "Pan", null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
            ExpensesViewModel vm = ExpensesViewModelTests.CreateViewModel(expenseRepo: repo, localizerOverride: new ResourceStringLocalizer());
            using var rig = new Rig(vm, repo);
            await WaitUntil(() => vm.HasLoaded && vm.Expenses.Count == 2);
            return VisibleTexts(rig.View);
        });

        Assert.Equal(2, texts.Count(t => t.StartsWith("≈ ", StringComparison.Ordinal)));
        Assert.Contains("≈ 0.42 USD", texts);
        Assert.Contains("≈ 600.00 DOP", texts);
        Assert.Contains("10.00 USD", texts);
        Assert.Contains("25.00 DOP", texts);
    }

    private sealed class Rig : IDisposable
    {
        public Rig(ExpensesViewModel vm, FakeExpenseRepository repo)
        {
            Vm = vm;
            Repo = repo;
            View = new ExpensesView();
            Window = HeadlessUi.Show(View, vm);
        }

        public ExpensesViewModel Vm { get; }

        public FakeExpenseRepository Repo { get; }

        public ExpensesView View { get; }

        public Window Window { get; }

        public void Dispose()
        {
            Window.Close();
            HeadlessUi.Flush();
        }
    }
}
