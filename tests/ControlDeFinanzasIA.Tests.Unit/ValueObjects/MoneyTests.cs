using System.Globalization;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Tests.Unit.ValueObjects;

/// <summary>
/// Tests para el value object <see cref="Money"/> (F-002, Phase 1).
/// Cubre AC-1 (factory y validación) y AC-6 (equality) de la spec F-002.
/// </summary>
public class MoneyTests
{
    // ============================================================
    // AC-1: Factory y validación
    // ============================================================

    [Fact]
    public void Create_WithPositiveAmount_ReturnsMoneyWithThatAmount()
    {
        var money = Money.Create(100m, Currency.DOP);

        Assert.Equal(100m, money.Amount);
        Assert.Equal(Currency.DOP, money.Currency);
    }

    [Fact]
    public void Create_WithZeroAmount_ReturnsMoneyWithZeroAmount()
    {
        var money = Money.Create(0m, Currency.DOP);

        Assert.Equal(0m, money.Amount);
        Assert.Equal(Currency.DOP, money.Currency);
    }

    [Fact]
    public void Create_WithMaxValueAmount_ReturnsMoneyWithMaxValue()
    {
        var money = Money.Create(decimal.MaxValue, Currency.DOP);

        Assert.Equal(decimal.MaxValue, money.Amount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    [InlineData(-1_000_000)]
    public void Create_WithNegativeAmount_ThrowsArgumentOutOfRangeException(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Money.Create(amount, Currency.DOP));
    }

    [Fact]
    public void Zero_WithUSD_ReturnsMoneyWithZeroAmountInUSD()
    {
        var money = Money.Zero(Currency.USD);

        Assert.Equal(0m, money.Amount);
        Assert.Equal(Currency.USD, money.Currency);
    }

    [Fact]
    public void Zero_WithDOP_ReturnsMoneyWithZeroAmountInDOP()
    {
        var money = Money.Zero(Currency.DOP);

        Assert.Equal(0m, money.Amount);
        Assert.Equal(Currency.DOP, money.Currency);
    }

    // ============================================================
    // AC-2 + AC-3: Suma (Add) y rechazo de mezcla de monedas
    // ============================================================

    [Fact]
    public void Add_SameAmountSameCurrency_ReturnsSum()
    {
        var a = Money.Create(100m, Currency.DOP);
        var b = Money.Create(50m, Currency.DOP);

        Money result = a.Add(b);

        Assert.Equal(150m, result.Amount);
        Assert.Equal(Currency.DOP, result.Currency);
    }

    [Fact]
    public void Add_ZeroPlusZero_ReturnsZero()
    {
        var a = Money.Zero(Currency.DOP);
        var b = Money.Zero(Currency.DOP);

        Money result = a.Add(b);

        Assert.Equal(0m, result.Amount);
        Assert.Equal(Currency.DOP, result.Currency);
    }

    [Fact]
    public void Add_ZeroPlusAmount_ReturnsTheAmount()
    {
        var zero = Money.Zero(Currency.DOP);
        var amount = Money.Create(50m, Currency.DOP);

        Money result = zero.Add(amount);

        Assert.Equal(50m, result.Amount);
        Assert.Equal(Currency.DOP, result.Currency);
    }

    [Fact]
    public void Add_DoesNotMutateOperands()
    {
        var a = Money.Create(100m, Currency.DOP);
        var b = Money.Create(50m, Currency.DOP);

        _ = a.Add(b);

        Assert.Equal(100m, a.Amount);
        Assert.Equal(50m, b.Amount);
    }

    [Fact]
    public void Add_DifferentCurrencies_ThrowsInvalidOperationException()
    {
        var dop = Money.Create(100m, Currency.DOP);
        var usd = Money.Create(50m, Currency.USD);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => dop.Add(usd));

        Assert.Contains("DOP", ex.Message);
        Assert.Contains("USD", ex.Message);
    }

    [Fact]
    public void Add_Null_ThrowsArgumentNullException()
    {
        var money = Money.Create(100m, Currency.DOP);

        Assert.Throws<ArgumentNullException>(() => money.Add(null!));
    }

    // ============================================================
    // AC-2 + AC-4: Resta (Subtract) y rechazo de resultado negativo
    // ============================================================

    [Fact]
    public void Subtract_SameAmountSameCurrency_ReturnsDifference()
    {
        var a = Money.Create(100m, Currency.DOP);
        var b = Money.Create(30m, Currency.DOP);

        Money result = a.Subtract(b);

        Assert.Equal(70m, result.Amount);
        Assert.Equal(Currency.DOP, result.Currency);
    }

    [Fact]
    public void Subtract_EqualAmounts_ReturnsZero()
    {
        var a = Money.Create(100m, Currency.DOP);
        var b = Money.Create(100m, Currency.DOP);

        Money result = a.Subtract(b);

        Assert.Equal(0m, result.Amount);
        Assert.Equal(Currency.DOP, result.Currency);
    }

    [Fact]
    public void Subtract_ZeroMinusZero_ReturnsZero()
    {
        var a = Money.Zero(Currency.DOP);
        var b = Money.Zero(Currency.DOP);

        Money result = a.Subtract(b);

        Assert.Equal(0m, result.Amount);
        Assert.Equal(Currency.DOP, result.Currency);
    }

    [Fact]
    public void Subtract_AmountMinusZero_ReturnsTheAmount()
    {
        var a = Money.Create(50m, Currency.DOP);
        var zero = Money.Zero(Currency.DOP);

        Money result = a.Subtract(zero);

        Assert.Equal(50m, result.Amount);
        Assert.Equal(Currency.DOP, result.Currency);
    }

    [Fact]
    public void Subtract_DoesNotMutateOperands()
    {
        var a = Money.Create(100m, Currency.DOP);
        var b = Money.Create(30m, Currency.DOP);

        _ = a.Subtract(b);

        Assert.Equal(100m, a.Amount);
        Assert.Equal(30m, b.Amount);
    }

    [Fact]
    public void Subtract_NegativeResult_ThrowsInvalidOperationException()
    {
        var a = Money.Create(50m, Currency.DOP);
        var b = Money.Create(100m, Currency.DOP);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => a.Subtract(b));

        Assert.Contains("-50", ex.Message);
    }

    [Fact]
    public void Subtract_DifferentCurrencies_ThrowsInvalidOperationException()
    {
        var dop = Money.Create(100m, Currency.DOP);
        var usd = Money.Create(50m, Currency.USD);

        Assert.Throws<InvalidOperationException>(() => dop.Subtract(usd));
    }

    [Fact]
    public void Subtract_Null_ThrowsArgumentNullException()
    {
        var money = Money.Create(100m, Currency.DOP);

        Assert.Throws<ArgumentNullException>(() => money.Subtract(null!));
    }

    // ============================================================
    // AC-2 + AC-4: Multiplicación por escalar (Multiply)
    // ============================================================

    [Fact]
    public void Multiply_PositiveScalar_ReturnsProduct()
    {
        var money = Money.Create(100m, Currency.DOP);

        Money result = money.Multiply(2m);

        Assert.Equal(200m, result.Amount);
        Assert.Equal(Currency.DOP, result.Currency);
    }

    [Fact]
    public void Multiply_ByZero_ReturnsZeroInSameCurrency()
    {
        var money = Money.Create(100m, Currency.DOP);

        Money result = money.Multiply(0m);

        Assert.Equal(0m, result.Amount);
        Assert.Equal(Currency.DOP, result.Currency);
    }

    [Fact]
    public void Multiply_ByFractionalScalar_ReturnsScaledAmount()
    {
        var money = Money.Create(100m, Currency.DOP);

        Money result = money.Multiply(0.5m);

        Assert.Equal(50m, result.Amount);
        Assert.Equal(Currency.DOP, result.Currency);
    }

    [Fact]
    public void Multiply_DoesNotMutateReceiver()
    {
        var money = Money.Create(100m, Currency.DOP);

        _ = money.Multiply(5m);

        Assert.Equal(100m, money.Amount);
        Assert.Equal(Currency.DOP, money.Currency);
    }

    [Fact]
    public void Multiply_NegativeScalar_ThrowsArgumentOutOfRangeException()
    {
        var money = Money.Create(100m, Currency.DOP);

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => money.Multiply(-1m));

        Assert.Contains("-1", ex.Message);
    }

    [Fact]
    public void Multiply_ZeroMoneyByPositiveScalar_ReturnsZero()
    {
        var zero = Money.Zero(Currency.DOP);

        Money result = zero.Multiply(7m);

        Assert.Equal(0m, result.Amount);
        Assert.Equal(Currency.DOP, result.Currency);
    }

    // ============================================================
    // AC-5: Formato (ToString)
    // ============================================================

    [Fact]
    public void ToString_WithEsDOCulture_FormatsWithCommaAndPeriodSeparators()
    {
        var money = Money.Create(1234.56m, Currency.DOP);
        var esDO = CultureInfo.GetCultureInfo("es-DO");

        string result = money.ToString(esDO);

        Assert.Equal("1,234.56 DOP", result);
    }

    [Fact]
    public void ToString_WithEnUSCulture_FormatsWithCommaAndPeriodSeparators()
    {
        var money = Money.Create(1234.56m, Currency.USD);
        var enUS = CultureInfo.GetCultureInfo("en-US");

        string result = money.ToString(enUS);

        Assert.Equal("1,234.56 USD", result);
    }

    [Fact]
    public void ToString_WithZeroAmount_FormatsAsZeroPointZeroZero()
    {
        var money = Money.Create(0m, Currency.DOP);
        var esDO = CultureInfo.GetCultureInfo("es-DO");

        string result = money.ToString(esDO);

        Assert.Equal("0.00 DOP", result);
    }

    [Fact]
    public void ToString_WithNullCulture_ThrowsArgumentNullException()
    {
        var money = Money.Create(100m, Currency.DOP);

        Assert.Throws<ArgumentNullException>(() => money.ToString(null!));
    }

    [Fact]
    public void ToString_NoArgs_UsesCurrentCulture()
    {
        var money = Money.Create(1234.56m, Currency.DOP);

        string withArgs = money.ToString(CultureInfo.CurrentCulture);
        string noArgs = money.ToString();

        Assert.Equal(withArgs, noArgs);
    }

    [Fact]
    public void ToString_WithFrFRCulture_UsesFrenchSeparators()
    {
        var money = Money.Create(1234.56m, Currency.DOP);
        var frFR = CultureInfo.GetCultureInfo("fr-FR");

        string result = money.ToString(frFR);

        // En fr-FR: 1234.56 → "1 234,56" (espacio de millares, coma decimal)
        // Comprobación flexible: el resultado NO debe ser el formato en-US
        // y debe contener ",56" (coma antes de los decimales).
        Assert.NotEqual("1,234.56 DOP", result);
        Assert.Contains(",56", result);
        Assert.Contains("DOP", result);
    }

    // ============================================================
    // AC-6: Equality (proporcionada por record automáticamente)
    // ============================================================

    [Fact]
    public void EqualityOperator_SameAmountSameCurrency_ReturnsTrue()
    {
        var a = Money.Create(100m, Currency.DOP);
        var b = Money.Create(100m, Currency.DOP);

        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.False(a != b);
    }

    [Fact]
    public void EqualityOperator_SameAmountDifferentCurrency_ReturnsFalse()
    {
        var dop = Money.Create(100m, Currency.DOP);
        var usd = Money.Create(100m, Currency.USD);

        Assert.False(dop == usd);
        Assert.True(dop != usd);
        Assert.False(dop.Equals(usd));
    }

    [Fact]
    public void EqualityOperator_DifferentAmountSameCurrency_ReturnsFalse()
    {
        var a = Money.Create(100m, Currency.DOP);
        var b = Money.Create(200m, Currency.DOP);

        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void GetHashCode_SameAmountSameCurrency_ReturnsSameHash()
    {
        var a = Money.Create(100m, Currency.DOP);
        var b = Money.Create(100m, Currency.DOP);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
