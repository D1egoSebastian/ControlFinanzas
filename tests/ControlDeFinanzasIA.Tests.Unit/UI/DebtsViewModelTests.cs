using ControlDeFinanzasIA.Application.UseCases.Debts.CreateDebtWithSchedule;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebts;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebtSchedule;
using ControlDeFinanzasIA.Application.UseCases.Debts.RegisterDebtPayment;
using ControlDeFinanzasIA.Application.UseCases.Debts.SkipDebtPayment;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

public sealed class DebtsViewModelTests
{
    internal static DebtsViewModel CreateViewModel(FakeDebtRepository debts, FakeDebtPaymentRepository payments)
    {
        var getDebts = new GetDebtsQuery(debts);
        var getSchedule = new GetDebtScheduleQuery(debts, payments);
        var create = new CreateDebtWithScheduleUseCase(debts, new CreateDebtWithScheduleValidator(), NullLogger<CreateDebtWithScheduleUseCase>.Instance);
        var register = new RegisterDebtPaymentUseCase(debts, payments, new FakeClock { Today = new DateOnly(2026, 3, 1) }, NullLogger<RegisterDebtPaymentUseCase>.Instance);
        var skip = new SkipDebtPaymentUseCase(payments, NullLogger<SkipDebtPaymentUseCase>.Instance);

        return new DebtsViewModel(getDebts, getSchedule, create, register, skip, NullLogger<DebtsViewModel>.Instance);
    }

    [Fact]
    public async Task CreateCommand_ValidForm_AddsDebtToList()
    {
        var debts = new FakeDebtRepository();
        var payments = new FakeDebtPaymentRepository();
        DebtsViewModel vm = CreateViewModel(debts, payments);
        vm.Description = "Prestamo";
        vm.OriginalAmount = 3000m;
        vm.Currency = Currency.DOP;
        vm.TotalInstallments = 3;
        vm.InstallmentAmount = 1000m;
        vm.Frequency = nameof(DebtFrequency.Monthly);
        vm.StartDate = new DateTime(2026, 1, 15);

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Single(vm.Debts);
        Assert.Equal(string.Empty, vm.FormError);
        Assert.NotEqual(string.Empty, vm.StatusMessage);
    }

    [Fact]
    public async Task CreateCommand_InvalidForm_SetsFormErrorAndDoesNotAddDebt()
    {
        var debts = new FakeDebtRepository();
        var payments = new FakeDebtPaymentRepository();
        DebtsViewModel vm = CreateViewModel(debts, payments);
        vm.Description = string.Empty; // invalid: required
        vm.StartDate = new DateTime(2026, 1, 15);

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Empty(vm.Debts);
        Assert.NotEqual(string.Empty, vm.FormError);
    }

    [Fact]
    public async Task SelectedDebt_LoadsScheduleAndPendingBalance()
    {
        var debt = Debt.Create("Deuda", ControlDeFinanzasIA.Domain.ValueObjects.Money.Create(1000m, Currency.DOP),
            Currency.DOP, 2, ControlDeFinanzasIA.Domain.ValueObjects.Money.Create(500m, Currency.DOP),
            nameof(DebtFrequency.Monthly), new DateOnly(2026, 1, 1));
        IReadOnlyList<DebtPayment> scheduledPayments = new DebtScheduleGenerator().Generate(debt);
        var debts = new FakeDebtRepository();
        debts.Seed(debt);
        var payments = new FakeDebtPaymentRepository();
        payments.SeedRange(scheduledPayments);
        DebtsViewModel vm = CreateViewModel(debts, payments);
        await Task.Delay(1); // allow constructor's fire-and-forget LoadDebtsAsync to complete

        vm.SelectedDebt = vm.Debts.First(d => d.Id == debt.Id);
        await Task.Delay(1);

        Assert.Equal(2, vm.Schedule.Count);
        Assert.Equal(1000m, vm.PendingBalance);
    }

    [Fact]
    public async Task RegisterPaymentCommand_MarksInstallmentPaidAndRefreshesSchedule()
    {
        var debt = Debt.Create("Deuda", ControlDeFinanzasIA.Domain.ValueObjects.Money.Create(500m, Currency.DOP),
            Currency.DOP, 1, ControlDeFinanzasIA.Domain.ValueObjects.Money.Create(500m, Currency.DOP),
            nameof(DebtFrequency.Monthly), new DateOnly(2026, 1, 1));
        IReadOnlyList<DebtPayment> scheduledPayments = new DebtScheduleGenerator().Generate(debt);
        var debts = new FakeDebtRepository();
        debts.Seed(debt);
        var payments = new FakeDebtPaymentRepository();
        payments.SeedRange(scheduledPayments);
        DebtsViewModel vm = CreateViewModel(debts, payments);
        await Task.Delay(1);
        vm.SelectedDebt = vm.Debts.First(d => d.Id == debt.Id);
        await Task.Delay(1);
        DebtPayment target = vm.Schedule.First();

        await vm.RegisterPaymentCommand.ExecuteAsync(target);

        Assert.Equal(string.Empty, vm.ScheduleError);
        Assert.Equal(0m, vm.PendingBalance);
    }
}
