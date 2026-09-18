using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integracion de round-trip para <see cref="DebtRepository"/>
/// y <see cref="DebtPaymentRepository"/> contra SQLite real (no
/// in-memory provider), mismo patron que
/// <c>RecurringRuleRepositoryTests</c>.
/// </summary>
public sealed class DebtRepositoryTests
{
    private static Debt CreateDebt(int totalInstallments = 3) => Debt.Create(
        "Prestamo carro",
        Money.Create(3000m, Currency.DOP),
        Currency.DOP,
        totalInstallments,
        Money.Create(1000m, Currency.DOP),
        nameof(DebtFrequency.Monthly),
        new DateOnly(2026, 1, 15),
        "Banco Popular",
        "Cuota mensual fija");

    [Fact]
    public async Task AddWithScheduleAsync_PersistsDebtAndAllPaymentsInOneOperation()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var debtRepository = new DebtRepository(context);
        var paymentRepository = new DebtPaymentRepository(context);
        Debt debt = CreateDebt();
        IReadOnlyList<DebtPayment> payments = new DebtScheduleGenerator().Generate(debt);

        Result addResult = await debtRepository.AddWithScheduleAsync(debt, payments);
        Debt? loadedDebt = await debtRepository.GetByIdAsync(debt.Id);
        IReadOnlyList<DebtPayment> loadedPayments = await paymentRepository.GetByDebtIdAsync(debt.Id);

        Assert.True(addResult.IsSuccess);
        Assert.NotNull(loadedDebt);
        Assert.Equal("Prestamo carro", loadedDebt!.Description);
        Assert.Equal(3000m, loadedDebt.OriginalAmount.Amount);
        Assert.Equal(Currency.DOP, loadedDebt.Currency);
        Assert.Equal(DebtStatus.Active, loadedDebt.Status);
        Assert.Equal(3, loadedPayments.Count);
        Assert.Equal(new[] { 1, 2, 3 }, loadedPayments.Select(p => p.InstallmentNumber));
        Assert.All(loadedPayments, p => Assert.Equal(DebtPaymentStatus.Pending, p.Status));
    }

    [Fact]
    public async Task UpdateAsync_PersistsStatusTransitionToClosed()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var debtRepository = new DebtRepository(context);
        var paymentRepository = new DebtPaymentRepository(context);
        Debt debt = CreateDebt(totalInstallments: 1);
        IReadOnlyList<DebtPayment> payments = new DebtScheduleGenerator().Generate(debt);
        await debtRepository.AddWithScheduleAsync(debt, payments);

        DebtPayment payment = payments[0];
        payment.MarkAsPaid(new DateOnly(2026, 1, 20));
        await paymentRepository.UpdateAsync(payment);
        debt.TryAutoClose(payments);
        Result updateResult = await debtRepository.UpdateAsync(debt);

        Debt? loaded = await debtRepository.GetByIdAsync(debt.Id);
        IReadOnlyList<DebtPayment> loadedPayments = await paymentRepository.GetByDebtIdAsync(debt.Id);

        Assert.True(updateResult.IsSuccess);
        Assert.Equal(DebtStatus.Closed, loaded!.Status);
        Assert.Equal(DebtPaymentStatus.Paid, loadedPayments[0].Status);
        Assert.Equal(new DateOnly(2026, 1, 20), loadedPayments[0].PaidDate);
    }

    [Fact]
    public async Task UpdateAsync_PersistsSkippedPaymentWithoutAffectingOthers()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var debtRepository = new DebtRepository(context);
        var paymentRepository = new DebtPaymentRepository(context);
        Debt debt = CreateDebt(totalInstallments: 3);
        IReadOnlyList<DebtPayment> payments = new DebtScheduleGenerator().Generate(debt);
        await debtRepository.AddWithScheduleAsync(debt, payments);

        DebtPayment second = payments[1];
        second.Skip();
        await paymentRepository.UpdateAsync(second);

        IReadOnlyList<DebtPayment> loadedPayments = await paymentRepository.GetByDebtIdAsync(debt.Id);

        Assert.Equal(DebtPaymentStatus.Pending, loadedPayments[0].Status);
        Assert.Equal(DebtPaymentStatus.Skipped, loadedPayments[1].Status);
        Assert.Equal(2, loadedPayments[1].InstallmentNumber);
        Assert.Equal(DebtPaymentStatus.Pending, loadedPayments[2].Status);
        Assert.Equal(3, loadedPayments[2].InstallmentNumber);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllPersistedDebts()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var debtRepository = new DebtRepository(context);
        Debt first = CreateDebt();
        var second = Debt.Create("Tarjeta de credito", Money.Create(2000m, Currency.USD), Currency.USD, 4,
            Money.Create(500m, Currency.USD), nameof(DebtFrequency.Biweekly), new DateOnly(2026, 2, 1));

        await debtRepository.AddWithScheduleAsync(first, new DebtScheduleGenerator().Generate(first));
        await debtRepository.AddWithScheduleAsync(second, new DebtScheduleGenerator().Generate(second));

        IReadOnlyList<Debt> all = await debtRepository.GetAllAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task RemoveAsync_CascadesToDebtPayments()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var debtRepository = new DebtRepository(context);
        var paymentRepository = new DebtPaymentRepository(context);
        Debt debt = CreateDebt();
        await debtRepository.AddWithScheduleAsync(debt, new DebtScheduleGenerator().Generate(debt));

        Result removeResult = await debtRepository.RemoveAsync(debt.Id);
        IReadOnlyList<DebtPayment> remainingPayments = await paymentRepository.GetByDebtIdAsync(debt.Id);

        Assert.True(removeResult.IsSuccess);
        Assert.Empty(remainingPayments);
    }
}
