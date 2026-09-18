using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Debts.SkipDebtPayment;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Debts;

/// <summary>
/// Tests para <see cref="SkipDebtPaymentUseCase"/> (R-5, S-4).
/// </summary>
public sealed class SkipDebtPaymentUseCaseTests
{
    private static List<DebtPayment> CreateSchedule(int totalInstallments = 5)
    {
        var debt = Debt.Create(
            "Deuda de prueba",
            Money.Create(500m * totalInstallments, Currency.DOP),
            Currency.DOP,
            totalInstallments,
            Money.Create(500m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 15));

        return new DebtScheduleGenerator().Generate(debt).ToList();
    }

    [Fact]
    public async Task ExecuteAsync_PendingInstallment_TransitionsToSkipped()
    {
        List<DebtPayment> payments = CreateSchedule();
        var repository = new FakeDebtPaymentRepository();
        repository.SeedRange(payments);
        var useCase = new SkipDebtPaymentUseCase(repository, NullLogger<SkipDebtPaymentUseCase>.Instance);

        Result result = await useCase.ExecuteAsync(payments[1].Id);

        Assert.True(result.IsSuccess);
        DebtPayment persisted = (await repository.GetByIdAsync(payments[1].Id))!;
        Assert.Equal(DebtPaymentStatus.Skipped, persisted.Status);
    }

    [Fact]
    public async Task ExecuteAsync_SkippingMiddleInstallment_DoesNotRenumberOrShiftFollowingInstallments()
    {
        List<DebtPayment> payments = CreateSchedule(5);
        var repository = new FakeDebtPaymentRepository();
        repository.SeedRange(payments);
        var useCase = new SkipDebtPaymentUseCase(repository, NullLogger<SkipDebtPaymentUseCase>.Instance);

        await useCase.ExecuteAsync(payments[1].Id); // skip installment #2

        IReadOnlyList<DebtPayment> schedule = await repository.GetByDebtIdAsync(payments[0].DebtId);
        Assert.Equal(5, schedule.Count);
        Assert.Equal(2, schedule[1].InstallmentNumber);
        Assert.Equal(DebtPaymentStatus.Skipped, schedule[1].Status);
        for (int i = 2; i < schedule.Count; i++)
        {
            Assert.Equal(i + 1, schedule[i].InstallmentNumber);
            Assert.Equal(DebtPaymentStatus.Pending, schedule[i].Status);
        }

        Assert.Equal(new DateOnly(2026, 3, 15), schedule[2].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 4, 15), schedule[3].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 5, 15), schedule[4].ScheduledDate);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadySkipped_ReturnsDomainInvariantFailure()
    {
        List<DebtPayment> payments = CreateSchedule(2);
        payments[0].Skip();
        var repository = new FakeDebtPaymentRepository();
        repository.SeedRange(payments);
        var useCase = new SkipDebtPaymentUseCase(repository, NullLogger<SkipDebtPaymentUseCase>.Instance);

        Result result = await useCase.ExecuteAsync(payments[0].Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("DomainInvariant", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyPaid_ReturnsDomainInvariantFailure()
    {
        List<DebtPayment> payments = CreateSchedule(2);
        payments[0].MarkAsPaid(new DateOnly(2026, 1, 20));
        var repository = new FakeDebtPaymentRepository();
        repository.SeedRange(payments);
        var useCase = new SkipDebtPaymentUseCase(repository, NullLogger<SkipDebtPaymentUseCase>.Instance);

        Result result = await useCase.ExecuteAsync(payments[0].Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("DomainInvariant", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownId_ReturnsNotFound()
    {
        var repository = new FakeDebtPaymentRepository();
        var useCase = new SkipDebtPaymentUseCase(repository, NullLogger<SkipDebtPaymentUseCase>.Instance);

        Result result = await useCase.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFound", result.Error!.Code);
    }
}
