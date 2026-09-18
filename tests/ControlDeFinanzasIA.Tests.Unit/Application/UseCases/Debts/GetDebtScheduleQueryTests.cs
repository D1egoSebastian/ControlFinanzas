using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebtSchedule;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Debts;

public sealed class GetDebtScheduleQueryTests
{
    [Fact]
    public async Task ExecuteAsync_ExistingDebt_ReturnsScheduleOrderedByInstallmentNumber()
    {
        var debt = Debt.Create(
            "Deuda", Money.Create(1500m, Currency.DOP), Currency.DOP, 3,
            Money.Create(500m, Currency.DOP), nameof(DebtFrequency.Monthly), new DateOnly(2026, 1, 15));
        var payments = new DebtScheduleGenerator().Generate(debt).ToList();

        var debts = new FakeDebtRepository();
        debts.Seed(debt);
        var paymentRepo = new FakeDebtPaymentRepository();
        paymentRepo.SeedRange(payments);
        var query = new GetDebtScheduleQuery(debts, paymentRepo);

        Result result = await query.ExecuteAsync(debt.Id);

        IReadOnlyList<DebtPayment> schedule = Assert.IsAssignableFrom<IReadOnlyList<DebtPayment>>(result.Value);
        Assert.Equal(3, schedule.Count);
        Assert.Equal(new[] { 1, 2, 3 }, schedule.Select(p => p.InstallmentNumber));
    }

    [Fact]
    public async Task ExecuteAsync_UnknownDebt_ReturnsNotFound()
    {
        var debts = new FakeDebtRepository();
        var paymentRepo = new FakeDebtPaymentRepository();
        var query = new GetDebtScheduleQuery(debts, paymentRepo);

        Result result = await query.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFound", result.Error!.Code);
    }
}
