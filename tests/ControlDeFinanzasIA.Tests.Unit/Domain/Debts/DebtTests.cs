using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Debts;

/// <summary>
/// Tests para las invariantes de <see cref="Debt.Create"/> (R-1, R-2)
/// y para la logica de dominio de saldo (R-7) y cierre automatico (R-6).
/// </summary>
public sealed class DebtTests
{
    private static Debt CreateValidDebt(
        int totalInstallments = 12,
        decimal originalAmount = 12000m,
        decimal installmentAmount = 1000m,
        string frequency = nameof(DebtFrequency.Monthly),
        DateOnly? startDate = null) =>
        Debt.Create(
            "Prestamo carro",
            Money.Create(originalAmount, Currency.DOP),
            Currency.DOP,
            totalInstallments,
            Money.Create(installmentAmount, Currency.DOP),
            frequency,
            startDate ?? new DateOnly(2026, 1, 15),
            "Banco Popular",
            "Cuota mensual fija");

    [Fact]
    public void Create_ValidDebt_ReturnsActiveDebtWithProvidedValues()
    {
        Debt debt = CreateValidDebt();

        Assert.NotEqual(Guid.Empty, debt.Id);
        Assert.Equal("Prestamo carro", debt.Description);
        Assert.Equal(12000m, debt.OriginalAmount.Amount);
        Assert.Equal(Currency.DOP, debt.Currency);
        Assert.Equal(12, debt.TotalInstallments);
        Assert.Equal(1000m, debt.InstallmentAmount.Amount);
        Assert.Equal(nameof(DebtFrequency.Monthly), debt.Frequency);
        Assert.Equal(new DateOnly(2026, 1, 15), debt.StartDate);
        Assert.Equal("Banco Popular", debt.CounterpartyName);
        Assert.Equal("Cuota mensual fija", debt.Note);
        Assert.Equal(DebtStatus.Active, debt.Status);
    }

    [Fact]
    public void Create_TrimsDescriptionAndOptionalFields()
    {
        var debt = Debt.Create(
            "  Tarjeta de credito  ",
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
            5,
            Money.Create(100m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 1),
            "  Banreservas  ",
            "  nota  ");

        Assert.Equal("Tarjeta de credito", debt.Description);
        Assert.Equal("Banreservas", debt.CounterpartyName);
        Assert.Equal("nota", debt.Note);
    }

    [Fact]
    public void Create_OptionalFieldsOmitted_AreNull()
    {
        var debt = Debt.Create(
            "Deuda sin contraparte",
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
            5,
            Money.Create(100m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 1));

        Assert.Null(debt.CounterpartyName);
        Assert.Null(debt.Note);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_EmptyDescription_ThrowsArgumentException(string? description)
    {
        Assert.Throws<ArgumentException>(() => Debt.Create(
            description!,
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
            5,
            Money.Create(100m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Create_DescriptionTooLong_ThrowsArgumentException()
    {
        string tooLong = new('a', Debt.MaxDescriptionLength + 1);
        Assert.Throws<ArgumentException>(() => Debt.Create(
            tooLong,
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
            5,
            Money.Create(100m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Create_OriginalAmountZero_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Debt.Create(
            "Deuda",
            Money.Create(0m, Currency.DOP),
            Currency.DOP,
            5,
            Money.Create(100m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Create_InstallmentAmountZero_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Debt.Create(
            "Deuda",
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
            5,
            Money.Create(0m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_TotalInstallmentsNotPositive_ThrowsArgumentException(int totalInstallments)
    {
        Assert.Throws<ArgumentException>(() => Debt.Create(
            "Deuda",
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
            totalInstallments,
            Money.Create(100m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Create_UnknownFrequency_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Debt.Create(
            "Deuda",
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
            5,
            Money.Create(100m, Currency.DOP),
            "Weekly",
            new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Create_StartDateBefore1900_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Debt.Create(
            "Deuda",
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
            5,
            Money.Create(100m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(1899, 12, 31)));
    }

    [Fact]
    public void Create_CounterpartyNameTooLong_ThrowsArgumentException()
    {
        string tooLong = new('b', Debt.MaxCounterpartyNameLength + 1);
        Assert.Throws<ArgumentException>(() => Debt.Create(
            "Deuda",
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
            5,
            Money.Create(100m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 1),
            tooLong));
    }

    [Fact]
    public void Create_NoteTooLong_ThrowsArgumentException()
    {
        string tooLong = new('c', Debt.MaxNoteLength + 1);
        Assert.Throws<ArgumentException>(() => Debt.Create(
            "Deuda",
            Money.Create(500m, Currency.DOP),
            Currency.DOP,
            5,
            Money.Create(100m, Currency.DOP),
            nameof(DebtFrequency.Monthly),
            new DateOnly(2026, 1, 1),
            null,
            tooLong));
    }

    // --- R-7: saldo pendiente ---

    [Fact]
    public void CalculatePendingBalance_NoPayments_EqualsOriginalAmount()
    {
        Debt debt = CreateValidDebt(totalInstallments: 3, originalAmount: 1500m, installmentAmount: 500m);
        IReadOnlyList<DebtPayment> payments = new DebtScheduleGenerator().Generate(debt);

        Money balance = debt.CalculatePendingBalance(payments);

        Assert.Equal(1500m, balance.Amount);
    }

    [Fact]
    public void CalculatePendingBalance_AfterOnePaid_SubtractsPaidAmount()
    {
        Debt debt = CreateValidDebt(totalInstallments: 3, originalAmount: 1500m, installmentAmount: 500m);
        IReadOnlyList<DebtPayment> payments = new DebtScheduleGenerator().Generate(debt);
        payments[0].MarkAsPaid(new DateOnly(2026, 1, 15));

        Money balance = debt.CalculatePendingBalance(payments);

        Assert.Equal(1000m, balance.Amount);
    }

    [Fact]
    public void CalculatePendingBalance_SkippedPaymentsDoNotReduceBalance()
    {
        Debt debt = CreateValidDebt(totalInstallments: 3, originalAmount: 1500m, installmentAmount: 500m);
        IReadOnlyList<DebtPayment> payments = new DebtScheduleGenerator().Generate(debt);
        payments[0].Skip();

        Money balance = debt.CalculatePendingBalance(payments);

        Assert.Equal(1500m, balance.Amount);
    }

    [Fact]
    public void CalculatePendingBalance_AllPaid_IsZero()
    {
        Debt debt = CreateValidDebt(totalInstallments: 3, originalAmount: 1500m, installmentAmount: 500m);
        IReadOnlyList<DebtPayment> payments = new DebtScheduleGenerator().Generate(debt);
        foreach (DebtPayment payment in payments)
        {
            payment.MarkAsPaid(new DateOnly(2026, 1, 15));
        }

        Money balance = debt.CalculatePendingBalance(payments);

        Assert.Equal(0m, balance.Amount);
    }

    // --- R-6: cierre automatico ---

    [Fact]
    public void TryAutoClose_AllPaidIncludingLast_ClosesDebt()
    {
        Debt debt = CreateValidDebt(totalInstallments: 3);
        IReadOnlyList<DebtPayment> payments = new DebtScheduleGenerator().Generate(debt);
        foreach (DebtPayment payment in payments)
        {
            payment.MarkAsPaid(new DateOnly(2026, 1, 15));
        }

        bool closed = debt.TryAutoClose(payments);

        Assert.True(closed);
        Assert.Equal(DebtStatus.Closed, debt.Status);
    }

    [Fact]
    public void TryAutoClose_LastPendingIsMissing_DoesNotClose()
    {
        Debt debt = CreateValidDebt(totalInstallments: 3);
        IReadOnlyList<DebtPayment> payments = new DebtScheduleGenerator().Generate(debt);
        payments[0].MarkAsPaid(new DateOnly(2026, 1, 15));
        payments[1].MarkAsPaid(new DateOnly(2026, 2, 15));
        // payments[2] (the last one) stays Pending.

        bool closed = debt.TryAutoClose(payments);

        Assert.False(closed);
        Assert.Equal(DebtStatus.Active, debt.Status);
    }

    [Fact]
    public void TryAutoClose_LastInstallmentSkippedNotPaid_DoesNotClose()
    {
        Debt debt = CreateValidDebt(totalInstallments: 3);
        IReadOnlyList<DebtPayment> payments = new DebtScheduleGenerator().Generate(debt);
        payments[0].MarkAsPaid(new DateOnly(2026, 1, 15));
        payments[1].MarkAsPaid(new DateOnly(2026, 2, 15));
        payments[2].Skip();

        bool closed = debt.TryAutoClose(payments);

        Assert.False(closed);
        Assert.Equal(DebtStatus.Active, debt.Status);
    }

    [Fact]
    public void TryAutoClose_MixOfPaidAndSkippedWithLastPaid_Closes()
    {
        Debt debt = CreateValidDebt(totalInstallments: 3);
        IReadOnlyList<DebtPayment> payments = new DebtScheduleGenerator().Generate(debt);
        payments[0].Skip();
        payments[1].MarkAsPaid(new DateOnly(2026, 2, 15));
        payments[2].MarkAsPaid(new DateOnly(2026, 3, 15));

        bool closed = debt.TryAutoClose(payments);

        Assert.True(closed);
        Assert.Equal(DebtStatus.Closed, debt.Status);
    }

    [Fact]
    public void TryAutoClose_AlreadyClosed_ReturnsFalse()
    {
        Debt debt = CreateValidDebt(totalInstallments: 1);
        IReadOnlyList<DebtPayment> payments = new DebtScheduleGenerator().Generate(debt);
        payments[0].MarkAsPaid(new DateOnly(2026, 1, 15));
        Assert.True(debt.TryAutoClose(payments));

        bool closedAgain = debt.TryAutoClose(payments);

        Assert.False(closedAgain);
    }
}
