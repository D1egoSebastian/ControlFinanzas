using System.Globalization;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// QA D5, D6 and D12: the REAL views over REAL view-models, rendered headless, must show readable row text. The
/// recurring preview must show name + amount + currency + date, ledger rows must show the currency next to the amount,
/// and enum-like values must be localized.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class ListRowTextHeadlessTests
{
    private static List<string> Texts(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? string.Empty)
            .Where(t => t.Length > 0)
            .ToList();

    private static async Task<T> InCulture<T>(string culture, Func<Task<T>> body)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return await HeadlessUi.OnUiThread(async () =>
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                return await body();
            });
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static ExpenseDto Expense(string description, decimal amount, Currency currency) =>
        new(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 20), amount, currency, description, null, DateTime.UtcNow, DateTime.UtcNow, null, "Cash", null);

    private static IncomeDto Income(string description, decimal amount, Currency currency) =>
        new(Guid.NewGuid(), null, new DateOnly(2026, 9, 20), amount, currency, description, null, null, DateTime.UtcNow, DateTime.UtcNow, null, "Cash", null);

    [Fact]
    public async Task RecurringPreview_RendersRuleNameAmountCurrencyAndDate()
    {
        bool ok = await InCulture("en", async () =>
        {
            var rules = new FakeRecurringRuleRepository();
            rules.Seed(RecurringRule.Create(
                "Netflix", Money.Create(15.5m, Currency.USD), Currency.USD, "Monthly", new DateOnly(2026, 9, 1), null, Guid.NewGuid(), Guid.NewGuid()));
            RecurringRulesViewModel vm = RowTemplateBindingHeadlessTests.NewRecurringRulesViewModel(rules);
            var view = new RecurringRulesView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            await vm.PreviewGenerationCommand.ExecuteAsync(null);
            HeadlessUi.Flush();

            Assert.NotEmpty(vm.PreviewItems);
            string line = Assert.Single(Texts(view), t => t.Contains("Netflix", StringComparison.Ordinal) && t.Contains("USD", StringComparison.Ordinal));
            Assert.Contains("15.50", line, StringComparison.Ordinal);
            Assert.DoesNotContain("{", line, StringComparison.Ordinal);
            return true;
        });

        Assert.True(ok);
    }

    [Theory]
    [InlineData(Currency.USD, "15.99 USD")]
    [InlineData(Currency.DOP, "15.99 DOP")]
    public async Task ExpensesView_RowShowsTheCurrencyNextToTheAmount(Currency currency, string expected)
    {
        bool ok = await InCulture("en", async () =>
        {
            var rig = new ExpensesViewModelImportTests.Rig();
            rig.ViewModel.Expenses.Add(Expense("Lunch", 15.99m, currency));
            var view = new ExpensesView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, rig.ViewModel));
            HeadlessUi.Flush();

            Assert.Contains(expected, Texts(view));
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    [Theory]
    [InlineData(Currency.USD, "15.99 USD")]
    [InlineData(Currency.DOP, "15.99 DOP")]
    public async Task IncomeView_RowShowsTheCurrencyNextToTheAmount(Currency currency, string expected)
    {
        bool ok = await InCulture("en", async () =>
        {
            IncomeViewModel vm = IncomeViewModelTests.CreateViewModel();
            vm.Incomes.Add(Income("Salary", 15.99m, currency));
            var view = new IncomeView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            HeadlessUi.Flush();

            Assert.Contains(expected, Texts(view));
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    [Theory]
    [InlineData("es-DO", "Mensual", "Activa", "Pendiente")]
    [InlineData("en", "Monthly", "Active", "Pending")]
    public async Task RecurringAndDebtsLists_ShowLocalizedLabelsForEnumValues(string culture, string frequency, string status, string pending)
    {
        bool ok = await InCulture(culture, async () =>
        {
            var rules = new FakeRecurringRuleRepository();
            rules.Seed(RecurringRule.Create(
                "Netflix", Money.Create(15m, Currency.DOP), Currency.DOP, "Monthly", new DateOnly(2026, 9, 1), null, Guid.NewGuid(), Guid.NewGuid()));
            RecurringRulesViewModel rvm = RowTemplateBindingHeadlessTests.NewRecurringRulesViewModel(rules);
            var rview = new RecurringRulesView();
            using (var _ = new HeadlessUi.Scope(HeadlessUi.Show(rview, rvm)))
            {
                await Task.Delay(50);
                HeadlessUi.Flush();
                List<string> texts = Texts(rview);
                Assert.Contains(frequency, texts);
                Assert.DoesNotContain("Bimonthly", texts);
            }

            var debt = Debt.Create("Loan", Money.Create(1000m, Currency.DOP), Currency.DOP, 2, Money.Create(500m, Currency.DOP), nameof(DebtFrequency.Monthly), new DateOnly(2026, 1, 1));
            var debts = new FakeDebtRepository();
            debts.Seed(debt);
            var payments = new FakeDebtPaymentRepository();
            payments.SeedRange(new DebtScheduleGenerator().Generate(debt));
            DebtsViewModel dvm = DebtsViewModelTests.CreateViewModel(debts, payments);
            await Task.Delay(50);
            dvm.SelectedDebt = dvm.Debts.Single();
            await Task.Delay(50);
            var dview = new DebtsView();
            using (var _ = new HeadlessUi.Scope(HeadlessUi.Show(dview, dvm)))
            {
                HeadlessUi.Flush();
                List<string> texts = Texts(dview);
                Assert.Contains(status, texts);
                Assert.Contains(pending, texts);
            }

            return true;
        });

        Assert.True(ok);
    }
}
