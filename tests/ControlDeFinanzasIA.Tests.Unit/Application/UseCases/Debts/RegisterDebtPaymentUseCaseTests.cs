using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Debts.RegisterDebtPayment;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Debts;

/// <summary>
/// Tests para <see cref="RegisterDebtPaymentUseCase"/>: registro de
/// pago (R-4), consistencia de saldo en cada transicion (R-7) y
/// cierre automatico al pagar la ultima cuota (R-6, S-5).
/// </summary>
public sealed class RegisterDebtPaymentUseCaseTests
{
    private static (Debt Debt, List<DebtPayment> Payments) CreateDebtWithSchedule(int totalInstallments, decimal installmentAmount = 500m)
    {
        var debt = Debt.Create(
            "Deuda de prueba",
            Money.Create(installmentAmount * totalInstallments, Currency.DOP),
            Currency.DOP,
            totalInstallments,
            Money.Create(installmentAmount, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 15));

        var payments = new DebtScheduleGenerator().Generate(debt).ToList();
        return (debt, payments);
    }

    private static (RegisterDebtPaymentUseCase UseCase, FakeDebtRepository Debts, FakeDebtPaymentRepository Payments) CreateUseCase(
        Debt debt, IEnumerable<DebtPayment> payments)
    {
        var debts = new FakeDebtRepository();
        debts.Seed(debt);
        var paymentRepo = new FakeDebtPaymentRepository();
        paymentRepo.SeedRange(payments);
        var clock = new FakeClock { Today = new DateOnly(2026, 3, 1) };
        var useCase = new RegisterDebtPaymentUseCase(debts, paymentRepo, clock, NullLogger<RegisterDebtPaymentUseCase>.Instance);
        return (useCase, debts, paymentRepo);
    }

    [Fact]
    public async Task ExecuteAsync_FirstOfThreePayments_TransitionsToPaidAndUpdatesBalance()
    {
        (Debt debt, List<DebtPayment> payments) = CreateDebtWithSchedule(3, installmentAmount: 500m);
        (RegisterDebtPaymentUseCase useCase, FakeDebtRepository debts, FakeDebtPaymentRepository paymentRepo) =
            CreateUseCase(debt, payments);

        Result result = await useCase.ExecuteAsync(payments[0].Id, new DateOnly(2026, 1, 20));

        Assert.True(result.IsSuccess);
        RegisterDebtPaymentResult value = Assert.IsType<RegisterDebtPaymentResult>(result.Value);
        Assert.Equal(1000m, value.PendingBalance); // R-7: 1500 - 500 = 1000
        Assert.False(value.DebtClosed);
        DebtPayment persisted = (await paymentRepo.GetByIdAsync(payments[0].Id))!;
        Assert.Equal(DebtPaymentStatus.Paid, persisted.Status);
        Assert.Equal(new DateOnly(2026, 1, 20), persisted.PaidDate);
    }

    [Fact]
    public async Task ExecuteAsync_ThreeSequentialPayments_BalanceDecreasesConsistentlyAtEachStep()
    {
        (Debt debt, List<DebtPayment> payments) = CreateDebtWithSchedule(3, installmentAmount: 500m);
        (RegisterDebtPaymentUseCase useCase, _, _) = CreateUseCase(debt, payments);

        Result first = await useCase.ExecuteAsync(payments[0].Id, new DateOnly(2026, 1, 15));
        Result second = await useCase.ExecuteAsync(payments[1].Id, new DateOnly(2026, 2, 15));
        Result third = await useCase.ExecuteAsync(payments[2].Id, new DateOnly(2026, 3, 15));

        Assert.Equal(1000m, ((RegisterDebtPaymentResult)first.Value!).PendingBalance);
        Assert.Equal(500m, ((RegisterDebtPaymentResult)second.Value!).PendingBalance);
        Assert.Equal(0m, ((RegisterDebtPaymentResult)third.Value!).PendingBalance);
        Assert.True(((RegisterDebtPaymentResult)third.Value!).DebtClosed);
    }

    [Fact]
    public async Task ExecuteAsync_LastPendingPayment_ClosesDebtAutomatically()
    {
        (Debt debt, List<DebtPayment> payments) = CreateDebtWithSchedule(3);
        payments[0].MarkAsPaid(new DateOnly(2026, 1, 15));
        payments[1].MarkAsPaid(new DateOnly(2026, 2, 15));
        (RegisterDebtPaymentUseCase useCase, FakeDebtRepository debts, _) = CreateUseCase(debt, payments);

        Result result = await useCase.ExecuteAsync(payments[2].Id, new DateOnly(2026, 3, 15));

        Assert.True(result.IsSuccess);
        RegisterDebtPaymentResult value = Assert.IsType<RegisterDebtPaymentResult>(result.Value);
        Assert.True(value.DebtClosed);
        Debt persisted = (await debts.GetByIdAsync(debt.Id))!;
        Assert.Equal(DebtStatus.Closed, persisted.Status);
    }

    [Fact]
    public async Task ExecuteAsync_MiddlePayment_DoesNotCloseDebt()
    {
        (Debt debt, List<DebtPayment> payments) = CreateDebtWithSchedule(3);
        (RegisterDebtPaymentUseCase useCase, FakeDebtRepository debts, _) = CreateUseCase(debt, payments);

        Result result = await useCase.ExecuteAsync(payments[1].Id, new DateOnly(2026, 2, 15));

        Assert.True(result.IsSuccess);
        Assert.False(((RegisterDebtPaymentResult)result.Value!).DebtClosed);
        Debt persisted = (await debts.GetByIdAsync(debt.Id))!;
        Assert.Equal(DebtStatus.Active, persisted.Status);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyPaidInstallment_ReturnsDomainInvariantFailure()
    {
        (Debt debt, List<DebtPayment> payments) = CreateDebtWithSchedule(3);
        payments[0].MarkAsPaid(new DateOnly(2026, 1, 15));
        (RegisterDebtPaymentUseCase useCase, _, _) = CreateUseCase(debt, payments);

        Result result = await useCase.ExecuteAsync(payments[0].Id, new DateOnly(2026, 1, 20));

        Assert.False(result.IsSuccess);
        Assert.Equal("DomainInvariant", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_SkippedInstallment_ReturnsDomainInvariantFailure()
    {
        (Debt debt, List<DebtPayment> payments) = CreateDebtWithSchedule(3);
        payments[0].Skip();
        (RegisterDebtPaymentUseCase useCase, _, _) = CreateUseCase(debt, payments);

        Result result = await useCase.ExecuteAsync(payments[0].Id, new DateOnly(2026, 1, 20));

        Assert.False(result.IsSuccess);
        Assert.Equal("DomainInvariant", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPaymentId_ReturnsNotFound()
    {
        (Debt debt, List<DebtPayment> payments) = CreateDebtWithSchedule(1);
        (RegisterDebtPaymentUseCase useCase, _, _) = CreateUseCase(debt, payments);

        Result result = await useCase.ExecuteAsync(Guid.NewGuid(), new DateOnly(2026, 1, 20));

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutExplicitDate_UsesClockToday()
    {
        (Debt debt, List<DebtPayment> payments) = CreateDebtWithSchedule(1);
        (RegisterDebtPaymentUseCase useCase, _, FakeDebtPaymentRepository paymentRepo) = CreateUseCase(debt, payments);

        Result result = await useCase.ExecuteAsync(payments[0].Id);

        Assert.True(result.IsSuccess);
        DebtPayment persisted = (await paymentRepo.GetByIdAsync(payments[0].Id))!;
        Assert.Equal(new DateOnly(2026, 3, 1), persisted.PaidDate);
    }
}
