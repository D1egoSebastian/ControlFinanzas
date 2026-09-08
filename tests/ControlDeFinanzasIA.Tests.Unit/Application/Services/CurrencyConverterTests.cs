using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.Application.Services.TestDoubles;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Services;

/// <summary>
/// Tests para <see cref="CurrencyConverter"/> (F-502).
/// </summary>
public class CurrencyConverterTests
{
    private static IMemoryCache BuildCache()
        => new MemoryCache(new MemoryCacheOptions());

    private static IConfiguration BuildConfig(int? ttlMinutes = null)
    {
        var dict = new Dictionary<string, string?>();
        if (ttlMinutes.HasValue)
        {
            dict["ExchangeRates:CacheTtlMinutes"] = ttlMinutes.Value.ToString();
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static CurrencyConverter BuildConverter(
        IExchangeRateProvider provider,
        int? ttlMinutes = null)
        => new(
            provider,
            BuildCache(),
            new FakeClock(),
            BuildConfig(ttlMinutes),
            NullLogger<CurrencyConverter>.Instance);

    [Fact]
    public async Task ConvertAsync_SameCurrency_ReturnsSameMoneyWithoutConsultingProvider()
    {
        FakeExchangeRateProvider provider = new((f, t) =>
            throw new InvalidOperationException("Provider should not be called."));
        CurrencyConverter converter = BuildConverter(provider);
        var source = Money.Create(100m, Currency.DOP);

        Result result = await converter.ConvertAsync(source, Currency.DOP);

        Assert.True(result.IsSuccess);
        Assert.Equal(source, result.Value);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task ConvertAsync_DifferentCurrencies_ReturnsConvertedMoney()
    {
        FakeExchangeRateProvider provider = new((f, t) =>
            ExchangeRate.Create(f, t, 60m, DateTime.UtcNow, "fake"));
        CurrencyConverter converter = BuildConverter(provider);
        var source = Money.Create(100m, Currency.USD);

        Result result = await converter.ConvertAsync(source, Currency.DOP);

        Assert.True(result.IsSuccess);
        var converted = (Money)result.Value!;
        Assert.Equal(6000m, converted.Amount);
        Assert.Equal(Currency.DOP, converted.Currency);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task ConvertAsync_ProviderThrows_ReturnsFailureWithExchangeRateUnavailableCode()
    {
        FakeExchangeRateProvider provider = new(new HttpRequestException("network down"));
        CurrencyConverter converter = BuildConverter(provider);
        var source = Money.Create(100m, Currency.USD);

        Result result = await converter.ConvertAsync(source, Currency.DOP);

        Assert.False(result.IsSuccess);
        Assert.Equal("ExchangeRateUnavailable", result.Error!.Code);
    }

    [Fact]
    public async Task ConvertAsync_CachedRate_ReturnsCachedValueWithoutHittingProvider()
    {
        FakeExchangeRateProvider provider = new((f, t) =>
            ExchangeRate.Create(f, t, 60m, DateTime.UtcNow, "fake"));
        CurrencyConverter converter = BuildConverter(provider);
        var source = Money.Create(100m, Currency.USD);

        await converter.ConvertAsync(source, Currency.DOP); // cache miss
        Result result2 = await converter.ConvertAsync(source, Currency.DOP); // cache hit

        Assert.True(result2.IsSuccess);
        Assert.Equal(1, provider.CallCount); // solo se llamo una vez
    }

    [Fact]
    public async Task DisplayAsync_UsdSource_ReturnsDisplayWithDopConverted()
    {
        FakeExchangeRateProvider provider = new((f, t) =>
            ExchangeRate.Create(f, t, 60m, DateTime.UtcNow, "fake"));
        CurrencyConverter converter = BuildConverter(provider);
        var source = Money.Create(25m, Currency.USD);

        MoneyDisplay display = await converter.DisplayAsync(source);

        Assert.Equal(source, display.Original);
        Assert.NotNull(display.Converted);
        Assert.Equal(Currency.DOP, display.Converted!.Currency);
        Assert.Equal(1500m, display.Converted.Amount);
        Assert.Equal("fake", display.ProviderName);
    }

    [Fact]
    public async Task DisplayAsync_DopSource_ReturnsDisplayWithUsdConverted()
    {
        FakeExchangeRateProvider provider = new((f, t) =>
            ExchangeRate.Create(f, t, 0.0167m, DateTime.UtcNow, "fake"));
        CurrencyConverter converter = BuildConverter(provider);
        var source = Money.Create(60m, Currency.DOP);

        MoneyDisplay display = await converter.DisplayAsync(source);

        Assert.NotNull(display.Converted);
        Assert.Equal(Currency.USD, display.Converted!.Currency);
        Assert.Equal(1.002m, display.Converted.Amount); // 60 * 0.0167 = 1.002
    }

    [Fact]
    public async Task DisplayAsync_ProviderUnavailable_ReturnsDisplayWithNullConverted()
    {
        FakeExchangeRateProvider provider = new(new HttpRequestException("net down"));
        CurrencyConverter converter = BuildConverter(provider);
        var source = Money.Create(100m, Currency.USD);

        MoneyDisplay display = await converter.DisplayAsync(source);

        Assert.Equal(source, display.Original);
        Assert.Null(display.Converted);
        Assert.Equal("fake-throwing", display.ProviderName);
    }

    [Fact]
    public async Task ConvertAsync_OperationCanceledException_IsRethrown()
    {
        FakeExchangeRateProvider provider = new(new OperationCanceledException());
        CurrencyConverter converter = BuildConverter(provider);
        var source = Money.Create(100m, Currency.USD);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => converter.ConvertAsync(source, Currency.DOP));
    }
}
