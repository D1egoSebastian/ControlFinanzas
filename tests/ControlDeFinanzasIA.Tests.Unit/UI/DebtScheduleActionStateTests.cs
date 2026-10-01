using System.Globalization;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// QA D10: Pay / Skip must be unavailable for installments that are no longer pending, and a rejected action must show a
/// localized message instead of the raw domain exception (which carries an internal id). Real view-model, real use cases.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class DebtScheduleActionStateTests
{
    private static async Task<DebtsViewModel> NewViewModelWithTwoInstallments()
    {
        var debt = Debt.Create(
            "Loan",
            Money.Create(1000m, Currency.DOP),
            Currency.DOP,
            2,
            Money.Create(500m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 1));
        var debts = new FakeDebtRepository();
        debts.Seed(debt);
        var payments = new FakeDebtPaymentRepository();
        payments.SeedRange(new DebtScheduleGenerator().Generate(debt));
        DebtsViewModel vm = DebtsViewModelTests.CreateViewModel(debts, payments);
        await Task.Delay(1);
        vm.SelectedDebt = vm.Debts.Single();
        await Task.Delay(1);
        return vm;
    }

    [Fact]
    public async Task PaidInstallment_CannotBePaidOrSkippedThroughTheCommands()
    {
        DebtsViewModel vm = await NewViewModelWithTwoInstallments();
        DebtPayment first = vm.Schedule.First();
        await vm.RegisterPaymentCommand.ExecuteAsync(first);
        DebtPayment paid = vm.Schedule.Single(p => p.InstallmentNumber == first.InstallmentNumber);
        DebtPayment pending = vm.Schedule.Single(p => p.Status == DebtPaymentStatus.Pending);

        Assert.Equal(DebtPaymentStatus.Paid, paid.Status);
        Assert.False(vm.RegisterPaymentCommand.CanExecute(paid));
        Assert.False(vm.SkipPaymentCommand.CanExecute(paid));
        Assert.True(vm.RegisterPaymentCommand.CanExecute(pending));
        Assert.True(vm.SkipPaymentCommand.CanExecute(pending));
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public async Task ForcingAPaidInstallment_ShowsALocalizedMessageWithoutInternalIds(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            DebtsViewModel vm = await NewViewModelWithTwoInstallments();
            DebtPayment first = vm.Schedule.First();
            await vm.RegisterPaymentCommand.ExecuteAsync(first);
            DebtPayment paid = vm.Schedule.Single(p => p.InstallmentNumber == first.InstallmentNumber);

            await vm.RegisterPaymentCommand.ExecuteAsync(paid);
            string payMessage = vm.ScheduleError;
            vm.ScheduleError = string.Empty;
            await vm.SkipPaymentCommand.ExecuteAsync(paid);

            Assert.Equal(Strings.DebtsInstallmentNotPending, payMessage);
            Assert.Equal(Strings.DebtsInstallmentNotPending, vm.ScheduleError);
            Assert.DoesNotContain(paid.Id.ToString(), payMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DebtPayment", payMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("cannot be marked", payMessage, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task AfterPaying_TheSelectedDebtAndItsScheduleStayVisibleInTheRealView()
    {
        bool ok = await HeadlessUi.OnUiThread(async () =>
        {
            DebtsViewModel vm = await NewViewModelWithTwoInstallments();
            var view = new DebtsView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            HeadlessUi.Flush();
            ListBox list = view.GetVisualDescendants().OfType<ListBox>().Single();
            list.SelectedItem = vm.Debts.Single();
            HeadlessUi.Flush();
            await Task.Delay(50);
            Guid debtId = vm.SelectedDebt!.Id;

            await vm.RegisterPaymentCommand.ExecuteAsync(vm.Schedule.First());
            await Task.Delay(50);
            HeadlessUi.Flush();

            Assert.NotNull(vm.SelectedDebt);
            Assert.Equal(debtId, vm.SelectedDebt!.Id);
            Assert.Equal(2, vm.Schedule.Count);
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task DebtsView_DisablesPayAndSkipButtons_OnlyForNonPendingRows()
    {
        bool ok = await HeadlessUi.OnUiThread(async () =>
        {
            DebtsViewModel vm = await NewViewModelWithTwoInstallments();
            await vm.RegisterPaymentCommand.ExecuteAsync(vm.Schedule.First());
            var view = new DebtsView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, vm));
            HeadlessUi.Flush();

            var rowButtons = view.GetVisualDescendants().OfType<Button>()
                .Where(b => b.CommandParameter is DebtPayment).ToList();
            Assert.Equal(4, rowButtons.Count);
            foreach (Button button in rowButtons)
            {
                var row = (DebtPayment)button.CommandParameter!;
                Assert.Equal(row.Status == DebtPaymentStatus.Pending, button.IsEffectivelyEnabled);
            }

            return true;
        });

        Assert.True(ok);
    }
}
