using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.DTOs;

/// <summary>
/// Tests for <see cref="IncomeExtensions.ToDto(Income)"/> mirroring
/// <see cref="ExpenseExtensionsTests"/> for the income aggregate.
/// </summary>
public sealed class IncomeExtensionsTests
{
    private static Income CreateIncomeWithMethod(Guid? paymentMethodId, PaymentMethod? navigation)
    {
        var money = Money.Create(100m, Currency.DOP);

        System.Reflection.PropertyInfo paymentMethodProperty =
            typeof(Income).GetProperty(nameof(Income.PaymentMethod))!;

        var income = Income.Create(
            categoryId: null,
            date: new DateOnly(2026, 1, 15),
            amount: money,
            description: "Test income",
            note: null,
            source: null,
            createdAt: new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            paymentMethodId: paymentMethodId);

        // Set the navigation via reflection (mimics EF Core materialization
        // with Include). The setter is intentionally private; tests are
        // allowed to use reflection because that is what EF Core does
        // internally during query materialization.
        if (navigation is not null)
        {
            paymentMethodProperty.SetValue(income, navigation);
        }

        return income;
    }

    [Fact]
    public void ToDto_WithPaymentMethodNavigation_PopulatesPaymentMethodName()
    {
        var methodId = Guid.NewGuid();
        var method = new PaymentMethod
        {
            Id = methodId,
            Name = "Tarjeta de crédito",
            IsSystem = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        Income income = CreateIncomeWithMethod(methodId, method);

        IncomeDto dto = income.ToDto();

        Assert.Equal(methodId, dto.PaymentMethodId);
        Assert.Equal("Tarjeta de crédito", dto.PaymentMethodName);
    }

    [Fact]
    public void ToDto_WithoutPaymentMethodId_LeavesBothFieldsNull()
    {
        Income income = CreateIncomeWithMethod(null, navigation: null);

        IncomeDto dto = income.ToDto();

        Assert.Null(dto.PaymentMethodId);
        Assert.Null(dto.PaymentMethodName);
    }

    [Fact]
    public void ToDto_WithPaymentMethodIdButNoNavigation_DefensivelyLeavesNameNull()
    {
        var methodId = Guid.NewGuid();
        Income income = CreateIncomeWithMethod(methodId, navigation: null);

        IncomeDto dto = income.ToDto();

        Assert.Equal(methodId, dto.PaymentMethodId);
        Assert.Null(dto.PaymentMethodName);
    }
}
