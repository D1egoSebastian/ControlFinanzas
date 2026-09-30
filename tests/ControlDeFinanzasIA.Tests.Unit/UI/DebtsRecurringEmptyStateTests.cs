using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Controls;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S6 (R-20, S-39, S-41, DD-9): the REAL <see cref="DebtsView"/> and <see cref="RecurringRulesView"/> with the
/// REAL view models, in the shared headless session. Only the repositories around the view models are doubles.
/// These screens have "no data yet" only: there is no filter, so no filtered state and no action.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class DebtsRecurringEmptyStateTests
{
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

    private static EmptyState[] Visible(Control root) =>
        [.. root.GetVisualDescendants().OfType<EmptyState>().Where(e => e.IsEffectivelyVisible)];

    private static string[] Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty)];

    private static Debt NewDebt() =>
        Debt.Create(
            "Deuda",
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
            1,
            Money.Create(500m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 1));

    private static (DebtsViewModel Vm, DebtsView View, Window Window) ShowDebts(FakeDebtRepository debts)
    {
        DebtsViewModel vm = DebtsViewModelTests.CreateViewModel(debts, new FakeDebtPaymentRepository());
        var view = new DebtsView();
        return (vm, view, HeadlessUi.Show(view, vm));
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public async Task EmptyDebts_AfterTheFirstLoad_ShowTheLocalizedNoDataState_WithoutAnAction(string culture)
    {
        (int Count, string[] Texts, string Title, string Hint, bool Former, bool Filtered, int Buttons) result = await OnUi(culture, async () =>
        {
            (DebtsViewModel vm, DebtsView view, Window window) = ShowDebts(new FakeDebtRepository());
            try
            {
                await WaitUntil(() => vm.HasLoaded);
                string[] texts = Texts(view);
                int actionButtons = Visible(view).Single().GetVisualDescendants().OfType<Button>().Count(b => b.IsEffectivelyVisible);
                return (
                    Visible(view).Length,
                    texts,
                    Strings.EmptyStateDebtsTitle,
                    Strings.EmptyStateDebtsHint,
                    texts.Contains("Todavía no hay deudas registradas."),
                    texts.Contains(Strings.EmptyStateFilteredTitle),
                    actionButtons);
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.Equal(1, result.Count);
        Assert.Contains(result.Title, result.Texts);
        Assert.Contains(result.Hint, result.Texts);
        Assert.False(result.Former);
        Assert.False(result.Filtered);
        Assert.Equal(0, result.Buttons);
        Assert.Equal(culture == "en" ? "No debts yet" : "Aún no hay deudas", result.Title);
    }

    [Fact]
    public async Task WhenADebtExists_TheDebtsEmptyStateIsHidden()
    {
        (int Count, int Rows) result = await OnUi("en", async () =>
        {
            var debts = new FakeDebtRepository();
            debts.Seed(NewDebt());
            (DebtsViewModel vm, DebtsView view, Window window) = ShowDebts(debts);
            try
            {
                await WaitUntil(() => vm.HasLoaded);
                return (Visible(view).Length, vm.Debts.Count);
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.Equal(0, result.Count);
        Assert.Equal(1, result.Rows);
    }

    [Fact]
    public async Task WhileTheDebtsLoadIsPending_NoEmptyStateFlashes_AndItAppearsOnceTheLoadCompletes()
    {
        (bool LoadedBefore, int During, int After) result = await OnUi("en", async () =>
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            (DebtsViewModel vm, DebtsView view, Window window) = ShowDebts(new FakeDebtRepository { GetAllGate = gate });
            try
            {
                await Task.Delay(50);
                HeadlessUi.Flush();
                bool loadedBefore = vm.HasLoaded;
                int during = Visible(view).Length;

                gate.SetResult();
                await WaitUntil(() => vm.HasLoaded);
                return (loadedBefore, during, Visible(view).Length);
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.False(result.LoadedBefore);
        Assert.Equal(0, result.During);
        Assert.Equal(1, result.After);
    }

    [Fact]
    public async Task AFailedDebtsLoad_NeverReportsLoaded_NorShowsTheEmptyState()
    {
        (bool Loaded, int Count) result = await OnUi("en", async () =>
        {
            var debts = new FakeDebtRepository();
            debts.SetThrowOnGetAll(new InvalidOperationException("boom"));
            (DebtsViewModel vm, DebtsView view, Window window) = ShowDebts(debts);
            try
            {
                await Task.Delay(50);
                HeadlessUi.Flush();
                return (vm.HasLoaded, Visible(view).Length);
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.False(result.Loaded);
        Assert.Equal(0, result.Count);
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public async Task EmptyRecurringRules_AfterTheFirstLoad_ShowTheLocalizedNoDataState_WithoutAnAction(string culture)
    {
        (int Count, string[] Texts, string Title, string Hint, bool Former, int Buttons) result = await OnUi(culture, async () =>
        {
            DebtsRecurringLocalizationTests.RecurringRig rig = await DebtsRecurringLocalizationTests.NewRecurringRig(delay: false);
            var view = new RecurringRulesView();
            Window window = HeadlessUi.Show(view, rig.Vm);
            try
            {
                await WaitUntil(() => rig.Vm.HasLoaded);
                string[] texts = Texts(view);
                int actionButtons = Visible(view).Single().GetVisualDescendants().OfType<Button>().Count(b => b.IsEffectivelyVisible);
                return (
                    Visible(view).Length,
                    texts,
                    Strings.EmptyStateRecurringTitle,
                    Strings.EmptyStateRecurringHint,
                    texts.Contains("Todavía no hay reglas recurrentes."),
                    actionButtons);
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.Equal(1, result.Count);
        Assert.Contains(result.Title, result.Texts);
        Assert.Contains(result.Hint, result.Texts);
        Assert.False(result.Former);
        Assert.Equal(0, result.Buttons);
        Assert.Equal(culture == "en" ? "No recurring rules yet" : "Aún no hay reglas recurrentes", result.Title);
    }

    [Fact]
    public async Task WhenARuleExists_TheRecurringEmptyStateIsHidden()
    {
        (int Count, int Rows) result = await OnUi("en", async () =>
        {
            RecurringRule rule = RecurringRule.Create("Netflix", Money.Create(15m, Currency.DOP), Currency.DOP, "Monthly", new DateOnly(2026, 1, 1), null, Guid.NewGuid(), Guid.NewGuid());
            DebtsRecurringLocalizationTests.RecurringRig rig = await DebtsRecurringLocalizationTests.NewRecurringRig(rule, delay: false);
            var view = new RecurringRulesView();
            Window window = HeadlessUi.Show(view, rig.Vm);
            try
            {
                await WaitUntil(() => rig.Vm.HasLoaded);
                return (Visible(view).Length, rig.Vm.Rules.Count);
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.Equal(0, result.Count);
        Assert.Equal(1, result.Rows);
    }

    [Fact]
    public async Task WhileTheRulesLoadIsPending_NoEmptyStateFlashes_AndItAppearsOnceTheLoadCompletes()
    {
        (bool LoadedBefore, int During, int After) result = await OnUi("en", async () =>
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            DebtsRecurringLocalizationTests.RecurringRig rig = await DebtsRecurringLocalizationTests.NewRecurringRig(gate: gate, delay: false);
            var view = new RecurringRulesView();
            Window window = HeadlessUi.Show(view, rig.Vm);
            try
            {
                await Task.Delay(50);
                HeadlessUi.Flush();
                bool loadedBefore = rig.Vm.HasLoaded;
                int during = Visible(view).Length;

                gate.SetResult();
                await WaitUntil(() => rig.Vm.HasLoaded);
                return (loadedBefore, during, Visible(view).Length);
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.False(result.LoadedBefore);
        Assert.Equal(0, result.During);
        Assert.Equal(1, result.After);
    }

    [Theory]
    [InlineData("es-DO", "Nueva deuda", "Crear deuda", "Pagar")]
    [InlineData("en", "New debt", "Create debt", "Pay")]
    public async Task TheDebtsForm_RendersInTheActiveLanguage_NotInHardcodedSpanish(string culture, string heading, string create, string pay)
    {
        (string[] Texts, string[] Buttons) result = await OnUi<(string[], string[])>(culture, async () =>
        {
            Debt debt = NewDebt();
            var debts = new FakeDebtRepository();
            debts.Seed(debt);
            var payments = new FakeDebtPaymentRepository();
            payments.SeedRange(new DebtScheduleGenerator().Generate(debt));
            DebtsViewModel vm = DebtsViewModelTests.CreateViewModel(debts, payments);
            var view = new DebtsView();
            Window window = HeadlessUi.Show(view, vm);
            try
            {
                await WaitUntil(() => vm.HasLoaded);
                vm.SelectedDebt = vm.Debts.Single();
                await WaitUntil(() => vm.Schedule.Count == 1);
                return (
                    Texts(view),
                    [.. view.GetVisualDescendants().OfType<Button>().Select(b => b.Content as string ?? string.Empty)]);
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.Contains(heading, result.Texts);
        Assert.Contains(create, result.Buttons);
        Assert.Contains(pay, result.Buttons);
    }
}
