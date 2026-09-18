using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Debts;

/// <summary>
/// Tests para <see cref="DebtScheduleGenerator"/>. Cubre >= 10
/// escenarios: mensual, quincenal, 1 cuota, N cuotas, fin de mes y
/// a&#241;o bisiesto (criterio de aceptacion de la spec F-001 de Fase 7).
/// </summary>
public sealed class DebtScheduleGeneratorTests
{
    private static readonly DebtScheduleGenerator _generator = new();

    private static Debt CreateDebt(
        int totalInstallments,
        DateOnly startDate,
        string frequency = nameof(DebtFrequency.Monthly),
        decimal installmentAmount = 1000m) =>
        Debt.Create(
            "Deuda de prueba",
            Money.Create(installmentAmount * totalInstallments, Currency.DOP),
            Currency.DOP,
            totalInstallments,
            Money.Create(installmentAmount, Currency.DOP),
            frequency,
            startDate);

    [Fact]
    public void Generate_MonthlyDebt_ReturnsExpectedNumberOfPayments()
    {
        Debt debt = CreateDebt(12, new DateOnly(2026, 1, 15));
        IReadOnlyList<DebtPayment> payments = _generator.Generate(debt);

        Assert.Equal(12, payments.Count);
    }

    [Fact]
    public void Generate_MonthlyDebt_DatesAreMonthlySpaced()
    {
        Debt debt = CreateDebt(4, new DateOnly(2026, 1, 15));
        IReadOnlyList<DebtPayment> payments = _generator.Generate(debt);

        Assert.Equal(new DateOnly(2026, 1, 15), payments[0].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 2, 15), payments[1].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 3, 15), payments[2].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 4, 15), payments[3].ScheduledDate);
    }

    [Fact]
    public void Generate_MonthlyDebt_InstallmentNumbersAreSequentialStartingAtOne()
    {
        Debt debt = CreateDebt(3, new DateOnly(2026, 1, 15));
        IReadOnlyList<DebtPayment> payments = _generator.Generate(debt);

        Assert.Equal(1, payments[0].InstallmentNumber);
        Assert.Equal(2, payments[1].InstallmentNumber);
        Assert.Equal(3, payments[2].InstallmentNumber);
    }

    [Fact]
    public void Generate_AllPayments_ArePendingWithDebtAmountAndDebtId()
    {
        Debt debt = CreateDebt(3, new DateOnly(2026, 1, 15), installmentAmount: 750m);
        IReadOnlyList<DebtPayment> payments = _generator.Generate(debt);

        Assert.All(payments, payment =>
        {
            Assert.Equal(DebtPaymentStatus.Pending, payment.Status);
            Assert.Equal(750m, payment.Amount.Amount);
            Assert.Equal(debt.Id, payment.DebtId);
            Assert.Null(payment.PaidDate);
        });
    }

    [Fact]
    public void Generate_OneInstallment_ReturnsSinglePaymentAtStartDate()
    {
        Debt debt = CreateDebt(1, new DateOnly(2026, 3, 10));
        IReadOnlyList<DebtPayment> payments = _generator.Generate(debt);

        Assert.Single(payments);
        Assert.Equal(1, payments[0].InstallmentNumber);
        Assert.Equal(new DateOnly(2026, 3, 10), payments[0].ScheduledDate);
    }

    [Fact]
    public void Generate_LargeNumberOfInstallments_ReturnsExactCount()
    {
        Debt debt = CreateDebt(36, new DateOnly(2026, 1, 1));
        IReadOnlyList<DebtPayment> payments = _generator.Generate(debt);

        Assert.Equal(36, payments.Count);
        Assert.Equal(36, payments[^1].InstallmentNumber);
        Assert.Equal(new DateOnly(2028, 12, 1), payments[^1].ScheduledDate);
    }

    [Fact]
    public void Generate_MonthlyDayClampsAtEndOfMonth_31ToFebruary()
    {
        Debt debt = CreateDebt(4, new DateOnly(2026, 1, 31));
        IReadOnlyList<DebtPayment> payments = _generator.Generate(debt);

        Assert.Equal(new DateOnly(2026, 1, 31), payments[0].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 2, 28), payments[1].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 3, 31), payments[2].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 4, 30), payments[3].ScheduledDate);
    }

    [Fact]
    public void Generate_MonthlyDay31InLeapYear_ClampsToFeb29()
    {
        Debt debt = CreateDebt(3, new DateOnly(2024, 1, 31));
        IReadOnlyList<DebtPayment> payments = _generator.Generate(debt);

        Assert.Equal(new DateOnly(2024, 1, 31), payments[0].ScheduledDate);
        Assert.Equal(new DateOnly(2024, 2, 29), payments[1].ScheduledDate);
        Assert.Equal(new DateOnly(2024, 3, 31), payments[2].ScheduledDate);
    }

    [Fact]
    public void Generate_MonthlyDay31InNonLeapYear_ClampsToFeb28()
    {
        Debt debt = CreateDebt(2, new DateOnly(2026, 1, 31));
        IReadOnlyList<DebtPayment> payments = _generator.Generate(debt);

        Assert.Equal(new DateOnly(2026, 2, 28), payments[1].ScheduledDate);
    }

    [Fact]
    public void Generate_BiweeklyDebt_DatesAreSpaced15DaysApart()
    {
        Debt debt = CreateDebt(6, new DateOnly(2026, 1, 1), nameof(DebtFrequency.Biweekly));
        IReadOnlyList<DebtPayment> payments = _generator.Generate(debt);

        Assert.Equal(6, payments.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), payments[0].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 1, 16), payments[1].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 1, 31), payments[2].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 2, 15), payments[3].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 3, 2), payments[4].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 3, 17), payments[5].ScheduledDate);
    }

    [Fact]
    public void Generate_BiweeklyDebt_EachGapIsExactly15Days()
    {
        Debt debt = CreateDebt(4, new DateOnly(2026, 6, 1), nameof(DebtFrequency.Biweekly));
        IReadOnlyList<DebtPayment> payments = _generator.Generate(debt);

        for (int i = 1; i < payments.Count; i++)
        {
            int gapDays = payments[i].ScheduledDate.DayNumber - payments[i - 1].ScheduledDate.DayNumber;
            Assert.Equal(15, gapDays);
        }
    }

    [Fact]
    public void Generate_BiweeklyOneInstallment_ReturnsSinglePaymentAtStartDate()
    {
        Debt debt = CreateDebt(1, new DateOnly(2026, 5, 20), nameof(DebtFrequency.Biweekly));
        IReadOnlyList<DebtPayment> payments = _generator.Generate(debt);

        Assert.Single(payments);
        Assert.Equal(new DateOnly(2026, 5, 20), payments[0].ScheduledDate);
    }

    [Fact]
    public void Generate_ReturnsNewGuidsForEachPayment()
    {
        Debt debt = CreateDebt(3, new DateOnly(2026, 1, 1));
        IReadOnlyList<DebtPayment> payments = _generator.Generate(debt);

        Assert.Equal(3, payments.Select(p => p.Id).Distinct().Count());
        Assert.All(payments, p => Assert.NotEqual(Guid.Empty, p.Id));
    }

    [Fact]
    public void Generate_NullDebt_ThrowsArgumentNullException() => Assert.Throws<ArgumentNullException>(() => _generator.Generate(null!));
}
