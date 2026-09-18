using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Dashboard;

/// <summary>
/// Unit tests for <see cref="GetUpcomingPaymentsQuery"/> (F-008, slice 3).
/// Covers R-6/S-5 (both sources combined and date-ordered), the 30-day
/// horizon boundary (inclusive), the 10-item cap, and that inactive
/// recurring rules are excluded.
/// </summary>
public sealed class GetUpcomingPaymentsQueryTests
{
    private static readonly DateOnly _today = new(2026, 3, 1);

    [Fact]
    public async Task ExecuteAsync_PendingPaymentExactlyAtHorizonBoundary_IsIncluded()
    {
        FakeDebtPaymentRepository payments = new();
        FakeDebtRepository debts = new();
        Debt debt = CreateDebt("Préstamo carro", _today.AddDays(30));
        debts.Seed(debt);
        payments.SeedRange(new DebtScheduleGenerator().Generate(debt));

        GetUpcomingPaymentsQuery query = CreateQuery(payments, debts, new FakeRecurringRuleRepository());

        Result result = await query.ExecuteAsync();

        IReadOnlyList<UpcomingPaymentItem> items = Assert.IsAssignableFrom<IReadOnlyList<UpcomingPaymentItem>>(result.Value);
        Assert.Single(items);
        Assert.Equal(_today.AddDays(30), items[0].Date);
    }

    [Fact]
    public async Task ExecuteAsync_PendingPaymentOneDayPastHorizon_IsExcluded()
    {
        FakeDebtPaymentRepository payments = new();
        FakeDebtRepository debts = new();
        Debt debt = CreateDebt("Préstamo carro", _today.AddDays(31));
        debts.Seed(debt);
        payments.SeedRange(new DebtScheduleGenerator().Generate(debt));

        GetUpcomingPaymentsQuery query = CreateQuery(payments, debts, new FakeRecurringRuleRepository());

        Result result = await query.ExecuteAsync();

        IReadOnlyList<UpcomingPaymentItem> items = Assert.IsAssignableFrom<IReadOnlyList<UpcomingPaymentItem>>(result.Value);
        Assert.Empty(items);
    }

    [Fact]
    public async Task ExecuteAsync_CombinesDebtPaymentsAndRecurringRules_OrderedByDate()
    {
        FakeDebtPaymentRepository payments = new();
        FakeDebtRepository debts = new();
        Debt debt = CreateDebt("Préstamo carro", _today.AddDays(10));
        debts.Seed(debt);
        payments.SeedRange(new DebtScheduleGenerator().Generate(debt));

        FakeRecurringRuleRepository rules = new();
        rules.Seed(CreateRule("Netflix", _today.AddDays(5)));

        GetUpcomingPaymentsQuery query = CreateQuery(payments, debts, rules);

        Result result = await query.ExecuteAsync();

        IReadOnlyList<UpcomingPaymentItem> items = Assert.IsAssignableFrom<IReadOnlyList<UpcomingPaymentItem>>(result.Value);
        Assert.Equal(2, items.Count);
        Assert.Equal(UpcomingPaymentKind.RecurringRule, items[0].Kind);
        Assert.Equal("Netflix", items[0].Label);
        Assert.Equal(_today.AddDays(5), items[0].Date);
        Assert.Equal(UpcomingPaymentKind.DebtInstallment, items[1].Kind);
        Assert.Equal("Préstamo carro", items[1].Label);
        Assert.Equal(_today.AddDays(10), items[1].Date);
    }

    [Fact]
    public async Task ExecuteAsync_MoreThanMaxItems_CapsAtMaxItems()
    {
        FakeRecurringRuleRepository rules = new();
        for (int i = 0; i < 15; i++)
        {
            rules.Seed(CreateRule($"Regla {i:D2}", _today.AddDays(i)));
        }

        GetUpcomingPaymentsQuery query = CreateQuery(new FakeDebtPaymentRepository(), new FakeDebtRepository(), rules);

        Result result = await query.ExecuteAsync();

        IReadOnlyList<UpcomingPaymentItem> items = Assert.IsAssignableFrom<IReadOnlyList<UpcomingPaymentItem>>(result.Value);
        Assert.Equal(10, items.Count);
        Assert.Equal("Regla 00", items[0].Label);
        Assert.Equal("Regla 09", items[^1].Label);
    }

    [Fact]
    public async Task ExecuteAsync_InactiveRecurringRule_IsExcluded()
    {
        FakeRecurringRuleRepository rules = new();
        RecurringRule inactiveRule = CreateRule("Gimnasio", _today.AddDays(3));
        inactiveRule.Deactivate();
        rules.Seed(inactiveRule);

        GetUpcomingPaymentsQuery query = CreateQuery(new FakeDebtPaymentRepository(), new FakeDebtRepository(), rules);

        Result result = await query.ExecuteAsync();

        IReadOnlyList<UpcomingPaymentItem> items = Assert.IsAssignableFrom<IReadOnlyList<UpcomingPaymentItem>>(result.Value);
        Assert.Empty(items);
    }

    [Fact]
    public async Task ExecuteAsync_NoUpcomingPayments_ReturnsEmptyListWithoutThrowing()
    {
        GetUpcomingPaymentsQuery query = CreateQuery(
            new FakeDebtPaymentRepository(), new FakeDebtRepository(), new FakeRecurringRuleRepository());

        Result result = await query.ExecuteAsync();

        Assert.True(result.IsSuccess);
        IReadOnlyList<UpcomingPaymentItem> items = Assert.IsAssignableFrom<IReadOnlyList<UpcomingPaymentItem>>(result.Value);
        Assert.Empty(items);
    }

    private static GetUpcomingPaymentsQuery CreateQuery(
        FakeDebtPaymentRepository payments, FakeDebtRepository debts, FakeRecurringRuleRepository rules)
        => new(payments, debts, rules, new FakeClock { Today = _today });

    private static Debt CreateDebt(string description, DateOnly scheduledDate)
        => Debt.Create(
            description,
            Money.Create(1000m, Currency.DOP),
            Currency.DOP,
            totalInstallments: 1,
            installmentAmount: Money.Create(1000m, Currency.DOP),
            frequency: nameof(DebtFrequency.Monthly),
            startDate: scheduledDate);

    private static RecurringRule CreateRule(string name, DateOnly nextGenerationDate)
        => RecurringRule.Create(
            name,
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
            "Monthly",
            startDate: nextGenerationDate,
            endDate: null,
            categoryId: Guid.NewGuid(),
            paymentMethodId: Guid.NewGuid());
}
