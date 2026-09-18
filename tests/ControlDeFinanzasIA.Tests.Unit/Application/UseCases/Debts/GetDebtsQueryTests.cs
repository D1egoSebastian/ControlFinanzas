using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebts;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Debts;

public sealed class GetDebtsQueryTests
{
    private static Debt CreateDebt(int totalInstallments = 1) => Debt.Create(
        "Deuda",
        Money.Create(500m, Currency.DOP),
        Currency.DOP,
        totalInstallments,
        Money.Create(500m, Currency.DOP),
        nameof(DebtFrequency.Monthly),
        new DateOnly(2026, 1, 1));

    [Fact]
    public async Task ExecuteAsync_NoFilter_ReturnsAllDebts()
    {
        var repository = new FakeDebtRepository();
        Debt active = CreateDebt();
        Debt closed = CreateDebt();
        var closedPayments = new DebtScheduleGenerator().Generate(closed).ToList();
        closedPayments[0].MarkAsPaid(new DateOnly(2026, 1, 1));
        closed.TryAutoClose(closedPayments);
        repository.Seed(active);
        repository.Seed(closed);
        var query = new GetDebtsQuery(repository);

        Result result = await query.ExecuteAsync();

        IReadOnlyList<Debt> debts = Assert.IsAssignableFrom<IReadOnlyList<Debt>>(result.Value);
        Assert.Equal(2, debts.Count);
    }

    [Fact]
    public async Task ExecuteAsync_FilterByActive_ReturnsOnlyActiveDebts()
    {
        var repository = new FakeDebtRepository();
        Debt active = CreateDebt();
        Debt closed = CreateDebt();
        var closedPayments = new DebtScheduleGenerator().Generate(closed).ToList();
        closedPayments[0].MarkAsPaid(new DateOnly(2026, 1, 1));
        closed.TryAutoClose(closedPayments);
        repository.Seed(active);
        repository.Seed(closed);
        var query = new GetDebtsQuery(repository);

        Result result = await query.ExecuteAsync(DebtStatus.Active);

        IReadOnlyList<Debt> debts = Assert.IsAssignableFrom<IReadOnlyList<Debt>>(result.Value);
        Assert.Single(debts);
        Assert.Equal(active.Id, debts[0].Id);
    }

    [Fact]
    public async Task ExecuteAsync_FilterByClosed_ReturnsOnlyClosedDebts()
    {
        var repository = new FakeDebtRepository();
        Debt active = CreateDebt();
        Debt closed = CreateDebt();
        var closedPayments = new DebtScheduleGenerator().Generate(closed).ToList();
        closedPayments[0].MarkAsPaid(new DateOnly(2026, 1, 1));
        closed.TryAutoClose(closedPayments);
        repository.Seed(active);
        repository.Seed(closed);
        var query = new GetDebtsQuery(repository);

        Result result = await query.ExecuteAsync(DebtStatus.Closed);

        IReadOnlyList<Debt> debts = Assert.IsAssignableFrom<IReadOnlyList<Debt>>(result.Value);
        Assert.Single(debts);
        Assert.Equal(closed.Id, debts[0].Id);
    }
}
