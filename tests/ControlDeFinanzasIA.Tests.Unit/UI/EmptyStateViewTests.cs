using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Aggregates;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Controls;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S5a (R-14, R-20, S-39, S-40, S-41, DD-9): the REAL <see cref="ExpensesView"/> with the REAL
/// <see cref="ExpensesViewModel"/> and the real use cases, in the shared headless session. Only the repositories
/// around them are doubles (the same rig the existing Expenses tests use).
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class EmptyStateViewTests
{
    private static readonly DateOnly _january = new(2026, 1, 10);

    private static Task<T> OnUi<T>(string culture, Func<Task<T>> body) =>
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

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        HeadlessUi.Flush();
    }

    private static Expense NewExpense(DateOnly date, string description) =>
        Expense.Create(
            Guid.NewGuid(),
            date,
            Money.Create(100m, Currency.DOP),
            description,
            null,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    private static async Task<Rig> NewRig(int expenses, IExpenseRepository? gate = null)
    {
        var repository = new FakeExpenseRepository { PageAddedItems = true };
        for (int i = 0; i < expenses; i++)
        {
            await repository.AddAsync(NewExpense(_january.AddDays(i), $"Gasto {i}"));
        }

        ExpensesViewModel vm = ExpensesViewModelTests.CreateViewModel(expenseRepo: gate ?? repository);
        return new Rig(vm);
    }

    private static EmptyState[] Visible(Control root) =>
        [.. root.GetVisualDescendants().OfType<EmptyState>().Where(e => e.IsEffectivelyVisible)];

    private static string[] Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty)];

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public async Task AnEmptyDatabase_AfterTheFirstLoad_ShowsTheLocalizedNoDataState_WithTheCreateAction(string culture)
    {
        (int Count, string[] Texts, string Title, string Hint, string Action, bool Filtered) result = await OnUi(culture, async () =>
        {
            using Rig rig = await NewRig(expenses: 0);
            await WaitUntil(() => rig.Vm.HasLoaded);

            EmptyState[] visible = Visible(rig.View);
            string[] texts = Texts(rig.View);
            bool filtered = texts.Contains(Strings.EmptyStateFilteredTitle);
            return (visible.Length, texts, Strings.EmptyStateExpensesTitle, Strings.EmptyStateExpensesHint, Strings.EmptyStateCreateFirstAction, filtered);
        });

        Assert.Equal(1, result.Count);
        Assert.Contains(result.Title, result.Texts);
        Assert.Contains(result.Hint, result.Texts);
        Assert.Contains(result.Action, result.Texts);
        Assert.Contains("Ctrl+N", result.Texts);
        Assert.False(result.Filtered);
        Assert.Equal(culture == "en" ? "No expenses yet" : "Aún no hay gastos", result.Title);
    }

    [Fact]
    public async Task WhenExpensesExist_NoEmptyStateIsVisible_AndTheRowsAreListed()
    {
        (int Count, int Rows) result = await OnUi("en", async () =>
        {
            using Rig rig = await NewRig(expenses: 2);
            await WaitUntil(() => rig.Vm.HasLoaded);
            HeadlessUi.Flush();
            return (Visible(rig.View).Length, rig.Vm.Expenses.Count);
        });

        Assert.Equal(0, result.Count);
        Assert.Equal(2, result.Rows);
    }

    [Fact]
    public async Task WhileTheFirstLoadIsPending_NoEmptyStateFlashes_AndItAppearsOnceTheLoadCompletes()
    {
        (bool LoadedBefore, int DuringCount, int AfterCount) result = await OnUi("en", async () =>
        {
            var backing = new FakeExpenseRepository { PageAddedItems = true };
            var gate = new GatedExpenseRepository(backing);
            using Rig rig = await NewRig(expenses: 0, gate);

            await Task.Delay(50);
            HeadlessUi.Flush();
            bool loadedBefore = rig.Vm.HasLoaded;
            int during = Visible(rig.View).Length;

            gate.Release();
            await WaitUntil(() => rig.Vm.HasLoaded);
            return (loadedBefore, during, Visible(rig.View).Length);
        });

        Assert.False(result.LoadedBefore);
        Assert.Equal(0, result.DuringCount);
        Assert.Equal(1, result.AfterCount);
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public async Task AFilterThatMatchesNothing_ShowsTheNoResultsState_NotTheNoDataOne_AndClearingRestoresTheList(string culture)
    {
        (string[] Filtered, string[] AfterClear, int RowsAfterClear, bool NoDataWhileFiltered, bool ActiveWhileFiltered, string FilteredTitle, string ClearAction, string NoDataTitle) result =
            await OnUi(culture, async () =>
            {
                using Rig rig = await NewRig(expenses: 3);
                await WaitUntil(() => rig.Vm.HasLoaded);

                rig.Vm.FilterDateFrom = new DateOnly(2026, 3, 1);
                rig.Vm.FilterDateTo = new DateOnly(2026, 3, 31);
                await rig.Vm.ApplyFilterCommand.ExecuteAsync(null);
                HeadlessUi.Flush();

                string[] filtered = Texts(rig.View);
                bool noData = rig.Vm.IsEmptyNoData;
                bool active = rig.Vm.HasActiveFilter;

                Button clear = rig.View.GetVisualDescendants().OfType<Button>()
                    .Single(b => b.IsEffectivelyVisible && Equals(b.Content, Strings.EmptyStateClearFilterAction)
                        || b.IsEffectivelyVisible && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == Strings.EmptyStateClearFilterAction));
                clear.Command!.Execute(clear.CommandParameter);
                await WaitUntil(() => rig.Vm.Expenses.Count == 3);

                return (filtered, Texts(rig.View), rig.Vm.Expenses.Count, noData, active, Strings.EmptyStateFilteredTitle, Strings.EmptyStateClearFilterAction, Strings.EmptyStateExpensesTitle);
            });

        Assert.Contains(result.FilteredTitle, result.Filtered);
        Assert.Contains(result.ClearAction, result.Filtered);
        Assert.DoesNotContain(result.NoDataTitle, result.Filtered);
        Assert.False(result.NoDataWhileFiltered);
        Assert.True(result.ActiveWhileFiltered);
        Assert.Equal(3, result.RowsAfterClear);
        Assert.DoesNotContain(result.FilteredTitle, result.AfterClear);
        Assert.DoesNotContain(result.NoDataTitle, result.AfterClear);
    }

    [Fact]
    public async Task TheCreateAction_RaisesBeginNewEntry_AndLandsKeyboardFocusOnTheDescriptionBox()
    {
        (object? Before, object? After, bool Focused, string Description) result = await OnUi("en", async () =>
        {
            using Rig rig = await NewRig(expenses: 0);
            await WaitUntil(() => rig.Vm.HasLoaded);

            object? before = rig.Vm.DescriptionFocusRequest;
            Button create = rig.View.GetVisualDescendants().OfType<Button>()
                .Single(b => b.IsEffectivelyVisible && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == Strings.EmptyStateCreateFirstAction));
            create.Command!.Execute(create.CommandParameter);
            await WaitUntil(() => rig.Vm.DescriptionFocusRequest is not null);
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
            HeadlessUi.Flush();

            TextBox description = rig.View.GetVisualDescendants().OfType<TextBox>()
                .First(t => t.Watermark == Strings.ExpensesDescriptionPlaceholder);
            return (before, rig.Vm.DescriptionFocusRequest, description.IsFocused, Strings.ExpensesDescriptionPlaceholder);
        });

        Assert.Null(result.Before);
        Assert.NotNull(result.After);
        Assert.True(result.Focused);
        Assert.Equal("Description", result.Description);
    }

    [Theory]
    [InlineData("es-DO", "Registrar gasto", "Nuevo gasto", "Descripción")]
    [InlineData("en", "Register expense", "New expense", "Description")]
    public async Task TheExpensesForm_RendersInTheActiveLanguage_NotInHardcodedSpanish(
        string culture,
        string register,
        string heading,
        string placeholder)
    {
        (string[] Buttons, string[] Texts, string?[] Placeholders) result = await OnUi<(string[], string[], string?[])>(culture, async () =>
        {
            using Rig rig = await NewRig(expenses: 0);
            await WaitUntil(() => rig.Vm.HasLoaded);

            return (
                [.. rig.View.GetVisualDescendants().OfType<Button>().Select(b => b.Content as string ?? string.Empty)],
                Texts(rig.View),
                [.. rig.View.GetVisualDescendants().OfType<TextBox>().Select(t => t.Watermark)]);
        });

        Assert.Contains(register, result.Buttons);
        Assert.Contains(heading, result.Texts);
        Assert.Contains(placeholder, result.Placeholders);
    }

    /// <summary>Delegates to a fake repository but holds the list query until released (simulates a slow first load).</summary>
    private sealed class GatedExpenseRepository(FakeExpenseRepository inner) : IExpenseRepository
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _gate.TrySetResult();

        public async Task<PagedResult<Expense>> GetByFilterAsync(ExpenseFilter filter, CancellationToken ct = default)
        {
            await _gate.Task;
            return await inner.GetByFilterAsync(filter, ct);
        }

        public Task<Result> AddAsync(Expense expense, CancellationToken ct = default) => inner.AddAsync(expense, ct);

        public Task<Result> UpdateAsync(Expense expense, CancellationToken ct = default) => inner.UpdateAsync(expense, ct);

        public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default) => inner.RemoveAsync(id, ct);

        public Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);

        public Task<Expense?> GetByRecurringRuleAndPeriodAsync(Guid recurringRuleId, DateOnly period, CancellationToken ct = default) =>
            inner.GetByRecurringRuleAndPeriodAsync(recurringRuleId, period, ct);

        public Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken ct = default) => inner.GetAllAsync(ct);

        public Task<IReadOnlyList<CurrencyTotal>> GetPeriodTotalsAsync(DateRange period, CancellationToken ct = default) =>
            inner.GetPeriodTotalsAsync(period, ct);

        public Task<IReadOnlyList<CategoryCurrencyTotal>> GetCategoryTotalsAsync(DateRange period, CancellationToken ct = default) =>
            inner.GetCategoryTotalsAsync(period, ct);

        public Task<IReadOnlyList<MonthCurrencyTotal>> GetMonthlyTotalsAsync(DateRange period, CancellationToken ct = default) =>
            inner.GetMonthlyTotalsAsync(period, ct);

        public Task<IReadOnlyList<DayCurrencyTotal>> GetDailyTotalsAsync(DateRange period, CancellationToken ct = default) =>
            inner.GetDailyTotalsAsync(period, ct);

        public Task<IReadOnlyList<ExpenseAmountRow>> GetAmountRowsAsync(DateRange period, CancellationToken ct = default) =>
            inner.GetAmountRowsAsync(period, ct);
    }

    private sealed class Rig : IDisposable
    {
        public Rig(ExpensesViewModel vm)
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
}
