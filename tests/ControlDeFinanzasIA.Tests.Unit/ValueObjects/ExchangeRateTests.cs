using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Tests.Unit.ValueObjects;

/// <summary>
/// Tests para el value object <see cref="ExchangeRate"/> (F-501).
/// Cubre las invariantes de <see cref="ExchangeRate.Create"/> y el
/// redondeo bancario de <see cref="ExchangeRate.Convert"/>.
/// </summary>
public class ExchangeRateTests
{
    // ============================================================
    // Create: happy path
    // ============================================================

    [Fact]
    public void Create_WithValidInputs_ReturnsExchangeRateWithThoseValues()
    {
        DateTime asOf = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);

        var rate = ExchangeRate.Create(
            Currency.USD, Currency.DOP, 60m, asOf, "stub-fixed");

        Assert.Equal(Currency.USD, rate.From);
        Assert.Equal(Currency.DOP, rate.To);
        Assert.Equal(60m, rate.Rate);
        Assert.Equal(asOf, rate.AsOf);
        Assert.Equal("stub-fixed", rate.Source);
    }

    // ============================================================
    // Create: invariant From != To
    // ============================================================

    [Fact]
    public void Create_WithFromEqualsToAndIdentityRate_ReturnsIdentityExchangeRate()
    {
        DateTime asOf = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);

        var rate = ExchangeRate.Create(
            Currency.USD, Currency.USD, 1m, asOf, "identity");

        Assert.Equal(Currency.USD, rate.From);
        Assert.Equal(Currency.USD, rate.To);
        Assert.Equal(1m, rate.Rate);
        Assert.Equal("identity", rate.Source);
    }

    [Fact]
    public void Create_WithFromEqualsToAndNonIdentityRate_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => ExchangeRate.Create(
            Currency.USD, Currency.USD, 2m, DateTime.UtcNow, "stub-fixed"));

        Assert.Equal("from", ex.ParamName);
    }

    // ============================================================
    // Create: invariant Rate > 0
    // ============================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Create_WithNonPositiveRate_ThrowsArgumentOutOfRangeException(decimal rate)
    {
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => ExchangeRate.Create(
            Currency.USD, Currency.DOP, rate, DateTime.UtcNow, "stub-fixed"));

        Assert.Equal("rate", ex.ParamName);
    }

    // ============================================================
    // Create: invariant AsOf.Kind == Utc
    // ============================================================

    [Fact]
    public void Create_WithLocalAsOf_ThrowsArgumentException()
    {
        DateTime asOf = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Local);

        ArgumentException ex = Assert.Throws<ArgumentException>(() => ExchangeRate.Create(
            Currency.USD, Currency.DOP, 60m, asOf, "stub-fixed"));

        Assert.Equal("asOf", ex.ParamName);
    }

    [Fact]
    public void Create_WithUnspecifiedAsOf_ThrowsArgumentException()
    {
        DateTime asOf = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Unspecified);

        ArgumentException ex = Assert.Throws<ArgumentException>(() => ExchangeRate.Create(
            Currency.USD, Currency.DOP, 60m, asOf, "stub-fixed"));

        Assert.Equal("asOf", ex.ParamName);
    }

    // ============================================================
    // Create: invariant Source non-empty
    // ============================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidSource_ThrowsArgumentException(string? source)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => ExchangeRate.Create(
            Currency.USD, Currency.DOP, 60m, DateTime.UtcNow, source!));

        Assert.Equal("source", ex.ParamName);
    }

    // ============================================================
    // Convert
    // ============================================================

    [Fact]
    public void Convert_WithSimpleAmount_ReturnsAmountTimesRate()
    {
        var rate = ExchangeRate.Create(
            Currency.USD, Currency.DOP, 60m, DateTime.UtcNow, "stub-fixed");

        decimal converted = rate.Convert(100m);

        Assert.Equal(6000.0000m, converted);
    }

    [Fact]
    public void Convert_WithAmount1005_ReturnsExpected()
    {
        var rate = ExchangeRate.Create(
            Currency.USD, Currency.DOP, 60m, DateTime.UtcNow, "stub-fixed");

        decimal converted = rate.Convert(1.005m);

        Assert.Equal(60.3000m, converted);
    }

    [Fact]
    public void Convert_WithAmount1015_ReturnsExpected()
    {
        var rate = ExchangeRate.Create(
            Currency.USD, Currency.DOP, 60m, DateTime.UtcNow, "stub-fixed");

        decimal converted = rate.Convert(1.015m);

        Assert.Equal(60.9000m, converted);
    }

    [Fact]
    public void Convert_WithAmount1025_ReturnsExpected()
    {
        var rate = ExchangeRate.Create(
            Currency.USD, Currency.DOP, 60m, DateTime.UtcNow, "stub-fixed");

        decimal converted = rate.Convert(1.025m);

        Assert.Equal(61.5000m, converted);
    }

    [Fact]
    public void Convert_WithBankersRoundingMidpoint_RoundsToEven()
    {
        var rate = ExchangeRate.Create(
            Currency.USD, Currency.DOP, 1m, DateTime.UtcNow, "stub-fixed");

        decimal converted = rate.Convert(1.23455m);

        Assert.Equal(1.2346m, converted);
    }

    [Fact]
    public void Convert_WithMaxAmount_ThrowsOverflowException()
    {
        var rate = ExchangeRate.Create(
            Currency.USD, Currency.DOP, 2m, DateTime.UtcNow, "stub-fixed");

        Assert.Throws<OverflowException>(() => rate.Convert(decimal.MaxValue));
    }

    // ============================================================
    // Equality (proporcionada por record automáticamente)
    // ============================================================

    [Fact]
    public void EqualityOperator_SameValues_ReturnsTrue()
    {
        DateTime asOf = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
        var a = ExchangeRate.Create(Currency.USD, Currency.DOP, 60m, asOf, "stub-fixed");
        var b = ExchangeRate.Create(Currency.USD, Currency.DOP, 60m, asOf, "stub-fixed");

        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.False(a != b);
    }

    [Fact]
    public void EqualityOperator_DifferentRate_ReturnsFalse()
    {
        DateTime asOf = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
        var a = ExchangeRate.Create(Currency.USD, Currency.DOP, 60m, asOf, "stub-fixed");
        var b = ExchangeRate.Create(Currency.USD, Currency.DOP, 58.5m, asOf, "stub-fixed");

        Assert.False(a == b);
        Assert.True(a != b);
        Assert.False(a.Equals(b));
    }
}
