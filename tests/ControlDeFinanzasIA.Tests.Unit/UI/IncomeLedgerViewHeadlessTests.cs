using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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
/// 1.0 blockers D1, D2 and D3 (QA audit) for the Income list: the REAL <see cref="IncomeView"/> over the REAL
/// <see cref="IncomeViewModel"/> and use cases, rendered headless and driven with the keyboard like a user.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class IncomeLedgerViewHeadlessTests
{
    private static readonly Guid _salary = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _other = new("11111111-1111-1111-1111-000000000002");

    private static IncomeDto[] Rows(IncomeView view, IncomeViewModel vm) =>
        [
            .. view.GetVisualDescendants().OfType<Button>()
                .Where(b => ReferenceEquals(b.Command, vm.BeginEditCommand) && b.IsEffectivelyVisible)
                .Select(b => (IncomeDto)b.CommandParameter!),
        ];

    private static TextBox SearchBox(IncomeView view) =>
        view.GetVisualDescendants().OfType<TextBox>().Single(t => Avalonia.Automation.AutomationProperties.GetName(t) == "Search");

    private static async Task<Rig> NewRig(int count, bool mixedCategories = false)
    {
        var repo = new FakeIncomeRepository { PageAddedItems = true };
        for (int i = 0; i < count; i++)
        {
            await repo.AddAsync(
                Income.Create(
                    mixedCategories && i % 2 == 1 ? _other : _salary,
                    new DateOnly(2026, 1, 1).AddDays(i % 28).AddMonths(i / 28),
                    Money.Create(10m + i, Currency.DOP),
                    $"Ingreso {i}",
                    null,
                    null,
                    new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(i)));
        }

        var categories = new FakeCategoryRepository();
        categories.SetCategories(
        [
            new Category { Id = _salary, Name = "Salario" },
            new Category { Id = _other, Name = "Otros" },
        ]);

        IncomeViewModel vm = IncomeViewModelTests.CreateViewModel(
            incomeRepo: repo,
            categoryRepo: categories,
            localizerOverride: new ResourceStringLocalizer());
        var rig = new Rig(vm, repo);
        await WaitUntil(() => vm.HasLoaded && vm.Categories.Count > 0);
        return rig;
    }

    [Fact]
    public async Task With45Incomes_PageOneShows20Rows_AndTheNextPageHoldsTheOlderOnes()
    {
        (int FirstRows, string[] FirstTexts, bool PreviousEnabled, string[] Second, string[] SecondTexts, bool OldReachable) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(45);
            int firstRows = Rows(rig.View, rig.Vm).Length;
            string[] firstTexts = VisibleTexts(rig.View);
            bool previousEnabled = ButtonFor(rig.View, rig.Vm.PreviousPageCommand).IsEffectivelyEnabled;

            Press(rig.Window, ButtonFor(rig.View, rig.Vm.NextPageCommand));
            await WaitUntil(() => rig.Vm.Page == 2);
            string[] second = [.. Rows(rig.View, rig.Vm).Select(r => r.Description)];
            string[] secondTexts = VisibleTexts(rig.View);

            await rig.Vm.NextPageCommand.ExecuteAsync(null);
            HeadlessUi.Flush();
            bool old = Rows(rig.View, rig.Vm).Any(r => r.Description == "Ingreso 0");
            return (firstRows, firstTexts, previousEnabled, second, secondTexts, old);
        });

        Assert.Equal(20, result.FirstRows);
        Assert.Contains("Page 1 of 3", result.FirstTexts);
        Assert.Contains("45 records", result.FirstTexts);
        Assert.False(result.PreviousEnabled);
        Assert.Equal(20, result.Second.Length);
        Assert.Contains("Page 2 of 3", result.SecondTexts);
        Assert.True(result.OldReachable);
    }

    [Fact]
    public async Task Delete_NeedsAnInlineConfirmation_AndCancelKeepsTheRecord()
    {
        (int Confirm, int Cancel, int AddedAfterArm, int AddedAfterCancel, int AddedAfterConfirm, int Total, string[] Texts) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(3);
            IncomeDto target = Rows(rig.View, rig.Vm)[0];

            Press(rig.Window, ButtonFor(rig.View, rig.Vm.DeleteIncomeCommand, target));
            int confirm = VisibleTexts(rig.View).Count(t => t == "Confirm");
            int cancel = VisibleButtonsFor(rig.View, rig.Vm.CancelDeleteCommand).Length;
            int armed = rig.Repo.AddedIncomes.Count;

            Press(rig.Window, VisibleButtonsFor(rig.View, rig.Vm.CancelDeleteCommand)[0]);
            int afterCancel = rig.Repo.AddedIncomes.Count;

            Press(rig.Window, ButtonFor(rig.View, rig.Vm.DeleteIncomeCommand, target));
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.DeleteIncomeCommand, target));
            await WaitUntil(() => rig.Vm.TotalCount == 2);

            return (confirm, cancel, armed, afterCancel, rig.Repo.AddedIncomes.Count, rig.Vm.TotalCount, VisibleTexts(rig.View));
        });

        Assert.Equal(1, result.Confirm);
        Assert.Equal(1, result.Cancel);
        Assert.Equal(3, result.AddedAfterArm);
        Assert.Equal(3, result.AddedAfterCancel);
        Assert.Equal(2, result.AddedAfterConfirm);
        Assert.Equal(2, result.Total);
        Assert.Contains("2 records", result.Texts);
        Assert.Contains("Transaction deleted.", result.Texts);
    }

    [Fact]
    public async Task DeletingTheLastRowOfTheLastPage_LandsOnTheNewLastPage()
    {
        (int Rows, string Summary) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(21);
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.NextPageCommand));
            await WaitUntil(() => rig.Vm.Page == 2);
            IncomeDto only = Rows(rig.View, rig.Vm).Single();

            Press(rig.Window, ButtonFor(rig.View, rig.Vm.DeleteIncomeCommand, only));
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.DeleteIncomeCommand, only));
            await WaitUntil(() => rig.Vm.TotalCount == 20);

            return (Rows(rig.View, rig.Vm).Length, VisibleTexts(rig.View).Single(t => t.StartsWith("Page ", StringComparison.Ordinal)));
        });

        Assert.Equal(20, result.Rows);
        Assert.Equal("Page 1 of 1", result.Summary);
    }

    [Fact]
    public async Task TheFilterPanel_NarrowsByTextCategoryAndDates_AndClearBringsEverythingBack()
    {
        (int ByText, int ByCategory, int ByRange, int Cleared) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(10, mixedCategories: true);
            SearchBox(rig.View).Text = "Ingreso 3";
            Press(rig.Window, ButtonFor(rig.View, rig.Vm.ApplyFilterCommand));
            await WaitUntil(() => rig.Vm.TotalCount == 1);
            int byText = Rows(rig.View, rig.Vm).Length;

            Press(rig.Window, ButtonFor(rig.View, rig.Vm.ClearFilterCommand));
            await WaitUntil(() => rig.Vm.TotalCount == 10);

            ComboBox category = rig.View.GetVisualDescendants().OfType<ComboBox>()
                .Single(c => Avalonia.Automation.AutomationProperties.GetName(c) == "Filter by category");
            category.SelectedItem = rig.Vm.Categories.Single(c => c.Id == _other);
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
            int byRange = rig.Vm.TotalCount;

            Press(rig.Window, ButtonFor(rig.View, rig.Vm.ClearFilterCommand));
            await WaitUntil(() => rig.Vm.TotalCount == 10);
            return (byText, byCategory, byRange, rig.Vm.TotalCount);
        });

        Assert.Equal(1, result.ByText);
        Assert.Equal(5, result.ByCategory);
        Assert.Equal(3, result.ByRange);
        Assert.Equal(10, result.Cleared);
    }

    [Fact]
    public async Task EnterInTheSearchBox_AppliesTheFilter_AndDoesNotRegisterTheHalfFilledForm()
    {
        (int Total, int Added) result = await OnUi(async () =>
        {
            using Rig rig = await NewRig(45);
            rig.Vm.SelectedCategoryId = _salary;
            rig.Vm.Amount = 99m;
            rig.Vm.Description = "No debe registrarse";
            int addedBefore = rig.Repo.AddedIncomes.Count;

            TextBox search = SearchBox(rig.View);
            search.Text = "Ingreso 4";
            search.Focus();
            rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await WaitUntil(() => rig.Vm.TotalCount == 6);

            return (rig.Vm.TotalCount, rig.Repo.AddedIncomes.Count - addedBefore);
        });

        Assert.Equal(6, result.Total);
        Assert.Equal(0, result.Added);
    }

    [Fact]
    public async Task AnInvertedRange_ShowsAnInlineError_AFilterWithoutMatchesShowsTheFilteredEmptyState_AndNoDataHasNoPager()
    {
        (string[] ErrorTexts, bool Filtered, bool PagerOnFiltered, bool NoDataOnly, bool PagerOnNoData) result = await OnUi(async () =>
        {
            string[] errorTexts;
            bool filtered;
            bool pagerOnFiltered;
            using (Rig rig = await NewRig(5))
            {
                rig.Vm.FilterDateFrom = new DateOnly(2026, 3, 1);
                rig.Vm.FilterDateTo = new DateOnly(2026, 1, 1);
                Press(rig.Window, ButtonFor(rig.View, rig.Vm.ApplyFilterCommand));
                errorTexts = VisibleTexts(rig.View);

                rig.Vm.FilterDateFrom = null;
                rig.Vm.FilterDateTo = null;
                SearchBox(rig.View).Text = "zzz";
                Press(rig.Window, ButtonFor(rig.View, rig.Vm.ApplyFilterCommand));
                await WaitUntil(() => rig.Vm.IsEmptyFiltered);
                filtered = rig.View.GetVisualDescendants().OfType<EmptyState>().Count(e => e.IsEffectivelyVisible) == 1 && !rig.Vm.IsEmptyNoData;
                pagerOnFiltered = VisibleButtonsFor(rig.View, rig.Vm.NextPageCommand).Length > 0;
            }

            using Rig empty = await NewRig(0);
            bool noDataOnly = empty.View.GetVisualDescendants().OfType<EmptyState>().Count(e => e.IsEffectivelyVisible) == 1 && empty.Vm.IsEmptyNoData;
            return (errorTexts, filtered, pagerOnFiltered, noDataOnly, VisibleButtonsFor(empty.View, empty.Vm.NextPageCommand).Length > 0);
        });

        Assert.Contains("The start date cannot be later than the end date.", result.ErrorTexts);
        Assert.True(result.Filtered);
        Assert.False(result.PagerOnFiltered);
        Assert.True(result.NoDataOnly);
        Assert.False(result.PagerOnNoData);
    }

    [Fact]
    public async Task EveryRow_ShowsItsAmountInTheOtherCurrencyAsAnApproximateLine()
    {
        string[] texts = await OnUi(async () =>
        {
            var repo = new FakeIncomeRepository { PageAddedItems = true };
            await repo.AddAsync(Income.Create(_salary, new DateOnly(2026, 1, 2), Money.Create(10m, Currency.USD), "Freelance", null, null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
            await repo.AddAsync(Income.Create(_salary, new DateOnly(2026, 1, 3), Money.Create(25m, Currency.DOP), "Venta", null, null, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
            IncomeViewModel vm = IncomeViewModelTests.CreateViewModel(incomeRepo: repo, localizerOverride: new ResourceStringLocalizer());
            using var rig = new Rig(vm, repo);
            await WaitUntil(() => vm.HasLoaded && vm.Incomes.Count == 2);
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
        public Rig(IncomeViewModel vm, FakeIncomeRepository repo)
        {
            Vm = vm;
            Repo = repo;
            View = new IncomeView();
            Window = HeadlessUi.Show(View, vm);
        }

        public IncomeViewModel Vm { get; }

        public FakeIncomeRepository Repo { get; }

        public IncomeView View { get; }

        public Window Window { get; }

        public void Dispose()
        {
            Window.Close();
            HeadlessUi.Flush();
        }
    }
}
