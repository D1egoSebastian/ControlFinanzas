using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.DTOs;

/// <summary>
/// Tests for <see cref="ExpenseExtensions.ToDto(Expense)"/>. Verifies
/// that the navigation <see cref="Expense.PaymentMethod"/> is
/// surfaced via <see cref="ExpenseDto.PaymentMethodName"/> when the
/// repository loaded it (via <c>Include</c>).
/// </summary>
public sealed class ExpenseExtensionsTests
{
    private static Expense CreateExpenseWithMethod(Guid? paymentMethodId, PaymentMethod? navigation)
    {
        var money = Money.Create(100m, Currency.DOP);

        System.Reflection.PropertyInfo paymentMethodProperty =
            typeof(Expense).GetProperty(nameof(Expense.PaymentMethod))!;

        var expense = Expense.Create(
            categoryId: Guid.NewGuid(),
            date: new DateOnly(2026, 1, 15),
            amount: money,
            description: "Test",
            note: null,
            createdAt: new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            paymentMethodId: paymentMethodId);

        // Set the navigation via reflection (mimics EF Core materialization
        // with Include). The setter is intentionally private; tests are
        // allowed to use reflection because that is what EF Core does
        // internally during query materialization.
        if (navigation is not null)
        {
            paymentMethodProperty.SetValue(expense, navigation);
        }

        return expense;
    }

    [Fact]
    public void ToDto_WithPaymentMethodNavigation_PopulatesPaymentMethodName()
    {
        var methodId = Guid.NewGuid();
        var method = new PaymentMethod
        {
            Id = methodId,
            Name = "Efectivo",
            IsSystem = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        Expense expense = CreateExpenseWithMethod(methodId, method);

        ExpenseDto dto = expense.ToDto();

        Assert.Equal(methodId, dto.PaymentMethodId);
        Assert.Equal("Efectivo", dto.PaymentMethodName);
    }

    [Fact]
    public void ToDto_WithoutPaymentMethodId_LeavesBothFieldsNull()
    {
        Expense expense = CreateExpenseWithMethod(null, navigation: null);

        ExpenseDto dto = expense.ToDto();

        Assert.Null(dto.PaymentMethodId);
        Assert.Null(dto.PaymentMethodName);
    }

    [Fact]
    public void ToDto_WithPaymentMethodIdButNoNavigation_DefensivelyLeavesNameNull()
    {
        // Simulates an Expense materialized WITHOUT Include: PaymentMethodId
        // is set (came from the FK column) but PaymentMethod is null because
        // Include wasn't used. The DTO should still surface the FK so the UI
        // can decide what to do.
        var methodId = Guid.NewGuid();
        Expense expense = CreateExpenseWithMethod(methodId, navigation: null);

        ExpenseDto dto = expense.ToDto();

        Assert.Equal(methodId, dto.PaymentMethodId);
        Assert.Null(dto.PaymentMethodName);
    }
}
