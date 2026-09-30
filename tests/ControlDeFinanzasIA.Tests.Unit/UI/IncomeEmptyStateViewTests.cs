using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
/// F-021 S5b (R-14, R-20, S-39, S-40, S-41, DD-9): the REAL <see cref="IncomeView"/> with the REAL
/// <see cref="IncomeViewModel"/> and the real use cases, in the shared headless session. Only the repositories
/// around them are doubles (the same rig the existing Income tests use).
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class IncomeEmptyStateViewTests
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

    private static Income NewIncome(DateOnly date, string description) =>
        Income.Create(
            null,
            date,
            Money.Create(100m, Currency.DOP),
            description,
            null,
            null,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    private static async Task<Rig> NewRig(int incomes, TaskCompletionSource? gate = null)
    {
        var repository = new FakeIncomeRepository { PageAddedItems = true, GetByFilterGate = gate };
        for (int i = 0; i < incomes; i++)
        {
            await repository.AddAsync(NewIncome(_january.AddDays(i), $"Ingreso {i}"));
        }

        IncomeViewModel vm = IncomeViewModelTests.CreateViewModel(incomeRepo: repository);
        return new Rig(vm);
    }

    private static EmptyState[] Visible(Control root) =>
        [.. root.GetVisualDescendants().OfType<EmptyState>().Where(e => e.IsEffectivelyVisible)];

    private static string[] Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty)];

    private static Button ButtonWithText(Control root, string text) =>
        root.GetVisualDescendants().OfType<Button>()
            .Single(b => b.IsEffectivelyVisible && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text));

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public async Task AnEmptyDatabase_AfterTheFirstLoad_ShowsTheLocalizedNoDataState_WithTheCreateAction(string culture)
    {
        (int Count, string[] Texts, string Title, string Hint, string Action, bool Filtered) result = await OnUi(culture, async () =>
        {
            using Rig rig = await NewRig(incomes: 0);
            await WaitUntil(() => rig.Vm.HasLoaded);

            string[] texts = Texts(rig.View);
            return (Visible(rig.View).Length, texts, Strings.EmptyStateIncomeTitle, Strings.EmptyStateIncomeHint, Strings.EmptyStateCreateFirstAction, texts.Contains(Strings.EmptyStateFilteredTitle));
        });

        Assert.Equal(1, result.Count);
        Assert.Contains(result.Title, result.Texts);
        Assert.Contains(result.Hint, result.Texts);
        Assert.Contains(result.Action, result.Texts);
        Assert.Contains("Ctrl+Shift+N", result.Texts);
        Assert.False(result.Filtered);
        Assert.Equal(culture == "en" ? "No income yet" : "Aún no hay ingresos", result.Title);
    }

    [Fact]
    public async Task WhenIncomeExists_NoEmptyStateIsVisible_AndTheRowsAreListed()
    {
        (int Count, int Rows) result = await OnUi("en", async () =>
        {
            using Rig rig = await NewRig(incomes: 2);
            await WaitUntil(() => rig.Vm.HasLoaded);
            HeadlessUi.Flush();
            return (Visible(rig.View).Length, rig.Vm.Incomes.Count);
        });

        Assert.Equal(0, result.Count);
        Assert.Equal(2, result.Rows);
    }

    [Fact]
    public async Task WhileTheFirstLoadIsPending_NoEmptyStateFlashes_AndItAppearsOnceTheLoadCompletes()
    {
        (bool LoadedBefore, int DuringCount, int AfterCount) result = await OnUi("en", async () =>
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using Rig rig = await NewRig(incomes: 0, gate);

            await Task.Delay(50);
            HeadlessUi.Flush();
            bool loadedBefore = rig.Vm.HasLoaded;
            int during = Visible(rig.View).Length;

            gate.SetResult();
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
                using Rig rig = await NewRig(incomes: 3);
                await WaitUntil(() => rig.Vm.HasLoaded);

                rig.Vm.FilterDateFrom = new DateOnly(2026, 3, 1);
                rig.Vm.FilterDateTo = new DateOnly(2026, 3, 31);
                await rig.Vm.ApplyFilterCommand.ExecuteAsync(null);
                HeadlessUi.Flush();

                string[] filtered = Texts(rig.View);
                bool noData = rig.Vm.IsEmptyNoData;
                bool active = rig.Vm.HasActiveFilter;

                Button clear = ButtonWithText(rig.View, Strings.EmptyStateClearFilterAction);
                clear.Command!.Execute(clear.CommandParameter);
                await WaitUntil(() => rig.Vm.Incomes.Count == 3);

                return (filtered, Texts(rig.View), rig.Vm.Incomes.Count, noData, active, Strings.EmptyStateFilteredTitle, Strings.EmptyStateClearFilterAction, Strings.EmptyStateIncomeTitle);
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
        (object? Before, object? After, bool Focused) result = await OnUi("en", async () =>
        {
            using Rig rig = await NewRig(incomes: 0);
            await WaitUntil(() => rig.Vm.HasLoaded);

            object? before = rig.Vm.DescriptionFocusRequest;
            Button create = ButtonWithText(rig.View, Strings.EmptyStateCreateFirstAction);
            create.Command!.Execute(create.CommandParameter);
            await WaitUntil(() => rig.Vm.DescriptionFocusRequest is not null);
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
            HeadlessUi.Flush();

            TextBox description = rig.View.GetVisualDescendants().OfType<TextBox>()
                .First(t => t.Watermark == Strings.IncomeDescriptionPlaceholder);
            return (before, rig.Vm.DescriptionFocusRequest, description.IsFocused);
        });

        Assert.Null(result.Before);
        Assert.NotNull(result.After);
        Assert.True(result.Focused);
    }

    [Fact]
    public async Task TheCreateAction_CancelsAnEditInProgress()
    {
        (bool EditingBefore, bool EditingAfter) result = await OnUi("en", async () =>
        {
            using Rig rig = await NewRig(incomes: 1);
            await WaitUntil(() => rig.Vm.HasLoaded);
            rig.Vm.BeginEditCommand.Execute(rig.Vm.Incomes.Single());
            bool before = rig.Vm.IsEditMode;

            // With a row present the empty state is hidden, so drive the same command the action is bound to.
            rig.Vm.BeginNewEntryCommand.Execute(null);
            return (before, rig.Vm.IsEditMode);
        });

        Assert.True(result.EditingBefore);
        Assert.False(result.EditingAfter);
    }

    [Theory]
    [InlineData("es-DO", "Registrar ingreso", "Nuevo ingreso", "Descripción")]
    [InlineData("en", "Register income", "New income", "Description")]
    public async Task TheIncomeForm_RendersInTheActiveLanguage_NotInHardcodedSpanish(
        string culture,
        string register,
        string heading,
        string placeholder)
    {
        (string[] Buttons, string[] Texts, string?[] Placeholders) result = await OnUi<(string[], string[], string?[])>(culture, async () =>
        {
            using Rig rig = await NewRig(incomes: 0);
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

    private sealed class Rig : IDisposable
    {
        public Rig(IncomeViewModel vm)
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
}
