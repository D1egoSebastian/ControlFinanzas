using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Debts;

/// <summary>
/// Tests para las transiciones de estado de <see cref="DebtPayment"/>
/// (R-4, R-5): <see cref="DebtPayment.MarkAsPaid"/> y
/// <see cref="DebtPayment.Skip"/>.
/// </summary>
public sealed class DebtPaymentTests
{
    private static DebtPayment CreatePendingPayment(int installmentNumber = 1)
    {
        var debt = Debt.Create(
            "Deuda",
            Money.Create(3000m, Currency.DOP),
            Currency.DOP,
            3,
            Money.Create(1000m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 1));

        IReadOnlyList<DebtPayment> payments = new DebtScheduleGenerator().Generate(debt);
        return payments[installmentNumber - 1];
    }

    [Fact]
    public void MarkAsPaid_WhenPending_TransitionsToPaidWithGivenDate()
    {
        DebtPayment payment = CreatePendingPayment();
        var paidDate = new DateOnly(2026, 1, 20);

        payment.MarkAsPaid(paidDate);

        Assert.Equal(DebtPaymentStatus.Paid, payment.Status);
        Assert.Equal(paidDate, payment.PaidDate);
    }

    [Fact]
    public void MarkAsPaid_WhenAlreadyPaid_ThrowsInvalidOperationException()
    {
        DebtPayment payment = CreatePendingPayment();
        payment.MarkAsPaid(new DateOnly(2026, 1, 20));

        Assert.Throws<InvalidOperationException>(() => payment.MarkAsPaid(new DateOnly(2026, 1, 21)));
    }

    [Fact]
    public void MarkAsPaid_WhenSkipped_ThrowsInvalidOperationException()
    {
        DebtPayment payment = CreatePendingPayment();
        payment.Skip();

        Assert.Throws<InvalidOperationException>(() => payment.MarkAsPaid(new DateOnly(2026, 1, 20)));
    }

    [Fact]
    public void Skip_WhenPending_TransitionsToSkipped()
    {
        DebtPayment payment = CreatePendingPayment();

        payment.Skip();

        Assert.Equal(DebtPaymentStatus.Skipped, payment.Status);
        Assert.Null(payment.PaidDate);
    }

    [Fact]
    public void Skip_WhenAlreadySkipped_ThrowsInvalidOperationException()
    {
        DebtPayment payment = CreatePendingPayment();
        payment.Skip();

        Assert.Throws<InvalidOperationException>(() => payment.Skip());
    }

    [Fact]
    public void Skip_WhenAlreadyPaid_ThrowsInvalidOperationException()
    {
        DebtPayment payment = CreatePendingPayment();
        payment.MarkAsPaid(new DateOnly(2026, 1, 20));

        Assert.Throws<InvalidOperationException>(() => payment.Skip());
    }

    [Fact]
    public void Skip_DoesNotAffectInstallmentNumberOrScheduledDate()
    {
        DebtPayment payment = CreatePendingPayment(installmentNumber: 2);
        int originalNumber = payment.InstallmentNumber;
        DateOnly originalDate = payment.ScheduledDate;

        payment.Skip();

        Assert.Equal(originalNumber, payment.InstallmentNumber);
        Assert.Equal(originalDate, payment.ScheduledDate);
    }
}
