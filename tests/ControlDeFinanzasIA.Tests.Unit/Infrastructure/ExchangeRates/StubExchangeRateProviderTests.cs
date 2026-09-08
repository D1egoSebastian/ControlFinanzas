using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.ExchangeRates;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Configuration;

namespace ControlDeFinanzasIA.Tests.Unit.Infrastructure.ExchangeRates;

/// <summary>
/// Tests para <see cref="StubExchangeRateProvider"/> (F-501).
/// Cubre configuración default/custom, coherencia inversa,
/// identidad, batch y thread-safety.
/// </summary>
public class StubExchangeRateProviderTests
{
    private static readonly DateTime _fixedAsOf =
        new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);

    private static IConfiguration BuildConfig(decimal? usdToDop = null)
    {
        Dictionary<string, string?> dict = new();
        if (usdToDop.HasValue)
        {
            dict["ExchangeRates:UsdToDop"] = usdToDop.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static StubExchangeRateProvider BuildProvider(decimal? usdToDop = null)
        => new(BuildConfig(usdToDop), new FakeClock { UtcNow = _fixedAsOf });

    // ============================================================
    // ProviderName
    // ============================================================

    [Fact]
    public void ProviderName_ReturnsStub()
    {
        IExchangeRateProvider provider = BuildProvider();

        Assert.Equal("stub", provider.ProviderName);
    }

    // ============================================================
    // Default config
    // ============================================================

    [Fact]
    public async Task GetRateAsync_UsdToDop_WithoutConfig_Returns60()
    {
        IExchangeRateProvider provider = BuildProvider();

        ExchangeRate rate = await provider.GetRateAsync(Currency.USD, Currency.DOP);

        Assert.Equal(Currency.USD, rate.From);
        Assert.Equal(Currency.DOP, rate.To);
        Assert.Equal(60m, rate.Rate);
        Assert.Equal("stub-fixed", rate.Source);
        Assert.Equal(_fixedAsOf, rate.AsOf);
    }

    [Fact]
    public async Task GetRateAsync_DopToUsd_WithoutConfig_ReturnsApproximately1Over60()
    {
        IExchangeRateProvider provider = BuildProvider();

        ExchangeRate rate = await provider.GetRateAsync(Currency.DOP, Currency.USD);

        Assert.Equal(0.0167m, rate.Rate);
    }

    // ============================================================
    // Custom config
    // ============================================================

    [Fact]
    public async Task GetRateAsync_UsdToDop_WithCustomConfig_ReturnsConfigured()
    {
        IExchangeRateProvider provider = BuildProvider(58.5m);

        ExchangeRate rate = await provider.GetRateAsync(Currency.USD, Currency.DOP);

        Assert.Equal(58.5m, rate.Rate);
    }

    // ============================================================
    // Inverse coherence
    // ============================================================

    [Fact]
    public async Task GetRateAsync_InverseCoherence_ProductIsApproximately1()
    {
        IExchangeRateProvider provider = BuildProvider(60m);

        ExchangeRate usdToDop = await provider.GetRateAsync(Currency.USD, Currency.DOP);
        ExchangeRate dopToUsd = await provider.GetRateAsync(Currency.DOP, Currency.USD);

        Assert.Equal(60m, usdToDop.Rate);
        Assert.Equal(0.0167m, dopToUsd.Rate);

        decimal product = usdToDop.Rate * dopToUsd.Rate;
        Assert.True(Math.Abs(product - 1m) < 0.01m);
    }

    // ============================================================
    // Identity
    // ============================================================

    [Theory]
    [InlineData(Currency.USD)]
    [InlineData(Currency.DOP)]
    public async Task GetRateAsync_SameCurrency_ReturnsRate1(Currency currency)
    {
        IExchangeRateProvider provider = BuildProvider();

        ExchangeRate rate = await provider.GetRateAsync(currency, currency);

        Assert.Equal(currency, rate.From);
        Assert.Equal(currency, rate.To);
        Assert.Equal(1m, rate.Rate);
        Assert.Equal("identity", rate.Source);
    }

    // ============================================================
    // GetLatestRates
    // ============================================================

    [Fact]
    public async Task GetLatestRatesAsync_ReturnsBothPairs()
    {
        IExchangeRateProvider provider = BuildProvider();

        IReadOnlyDictionary<CurrencyPair, decimal> rates =
            await provider.GetLatestRatesAsync();

        Assert.Equal(2, rates.Count);
        Assert.Equal(60m, rates[new CurrencyPair(Currency.USD, Currency.DOP)]);
        Assert.Equal(0.0167m, rates[new CurrencyPair(Currency.DOP, Currency.USD)]);
    }

    // ============================================================
    // Thread-safety
    // ============================================================

    [Fact]
    public async Task GetRateAsync_Concurrent_ReturnsSameRate()
    {
        IExchangeRateProvider provider = BuildProvider(usdToDop: 60m);

        Task<ExchangeRate>[] tasks = Enumerable.Range(0, 100)
            .Select(_ => provider.GetRateAsync(Currency.USD, Currency.DOP))
            .ToArray();

        ExchangeRate[] results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal(60m, r.Rate));
    }

    // ============================================================
    // Invalid config
    // ============================================================

    [Fact]
    public async Task GetRateAsync_WithNegativeConfig_FallsBackToDefault()
    {
        IExchangeRateProvider provider = BuildProvider(-5m);

        ExchangeRate rate = await provider.GetRateAsync(Currency.USD, Currency.DOP);

        Assert.Equal(60m, rate.Rate);
    }

    [Fact]
    public async Task GetRateAsync_WithZeroConfig_FallsBackToDefault()
    {
        IExchangeRateProvider provider = BuildProvider(0m);

        ExchangeRate rate = await provider.GetRateAsync(Currency.USD, Currency.DOP);

        Assert.Equal(60m, rate.Rate);
    }

    [Fact]
    public async Task GetRateAsync_WithMalformedConfig_FallsBackToDefault()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExchangeRates:UsdToDop"] = "not-a-number",
            })
            .Build();

        IExchangeRateProvider provider = new StubExchangeRateProvider(
            config, new FakeClock { UtcNow = _fixedAsOf });

        ExchangeRate rate = await provider.GetRateAsync(Currency.USD, Currency.DOP);

        Assert.Equal(60m, rate.Rate);
    }

    // ============================================================
    // Unsupported pair
    // ============================================================

    [Fact]
    public async Task GetRateAsync_UnsupportedPair_ThrowsInvalidOperationException()
    {
        IExchangeRateProvider provider = BuildProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetRateAsync((Currency)999, Currency.USD));
    }
}
