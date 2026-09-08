using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace ControlDeFinanzasIA.Infrastructure.ExchangeRates;

/// <summary>
/// Implementación stub de <see cref="IExchangeRateProvider"/>:
/// devuelve tasas fijas configurables (default
/// <c>1 USD = 60 DOP</c>). Pensada para development offline y
/// para mantener la app operativa mientras se decide el provider
/// real (Fase 6+).
/// </summary>
/// <remarks>
/// <para>
/// <b>No es una fuente de verdad.</b> Las tasas hardcodeadas son
/// un placeholder. Cuando se enchufe el provider real, este stub
/// queda relegado a tests.
/// </para>
/// <para>
/// <b>Thread-safety:</b> el constructor precomputa un dictionary
/// inmutable de tasas; los métodos de instancia solo leen de ese
/// dictionary (sellado). No hay estado mutable después del
/// constructor, por lo que el objeto es intrínsecamente
/// thread-safe.
/// </para>
/// <para>
/// <b>Configuración:</b> lee <c>ExchangeRates:UsdToDop</c> de
/// <see cref="IConfiguration"/>. Default: <c>60.0m</c>. Si la
/// config es inválida (negativa o cero), cae al default sin
/// throw para no bloquear el arranque.
/// </para>
/// </remarks>
public sealed class StubExchangeRateProvider : IExchangeRateProvider
{
    private const decimal _defaultUsdToDop = 60.0m;
    private const string _rateSource = "stub-fixed";
    private const string _identitySource = "identity";

    private readonly IClock _clock;
    private readonly IReadOnlyDictionary<CurrencyPair, ExchangeRate> _rates;

    /// <inheritdoc />
    public string ProviderName => "stub";

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="StubExchangeRateProvider"/>.
    /// </summary>
    /// <param name="configuration">
    /// Configuración desde la que se lee <c>ExchangeRates:UsdToDop</c>.
    /// </param>
    /// <param name="clock">
    /// Reloj usado para sellar <see cref="ExchangeRate.AsOf"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Si <paramref name="configuration"/> o <paramref name="clock"/> son
    /// <see langword="null"/>.
    /// </exception>
    public StubExchangeRateProvider(IConfiguration configuration, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;

        decimal usdToDop = ReadUsdToDop(configuration);

        decimal dopToUsd = Math.Round(
            1m / usdToDop, 4, MidpointRounding.ToEven);

        DateTime asOf = _clock.UtcNow;

        _rates = new Dictionary<CurrencyPair, ExchangeRate>
        {
            [new CurrencyPair(Currency.USD, Currency.DOP)] =
                ExchangeRate.Create(Currency.USD, Currency.DOP, usdToDop, asOf, _rateSource),
            [new CurrencyPair(Currency.DOP, Currency.USD)] =
                ExchangeRate.Create(Currency.DOP, Currency.USD, dopToUsd, asOf, _rateSource),
        };
    }

    /// <inheritdoc />
    public Task<ExchangeRate> GetRateAsync(
        Currency from,
        Currency to,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (from == to)
        {
            return Task.FromResult(ExchangeRate.Create(
                from, to, 1m, _clock.UtcNow, _identitySource));
        }

        CurrencyPair key = new(from, to);
        if (!_rates.TryGetValue(key, out ExchangeRate? rate))
        {
            throw new InvalidOperationException(
                $"No rate available for {key}. Supported pairs: USD<->DOP.");
        }

        return Task.FromResult(rate);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<CurrencyPair, decimal>> GetLatestRatesAsync(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        IReadOnlyDictionary<CurrencyPair, decimal> result = _rates
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Rate);

        return Task.FromResult(result);
    }

    private static decimal ReadUsdToDop(IConfiguration configuration)
    {
        string? configuredValue = configuration["ExchangeRates:UsdToDop"];

        if (!string.IsNullOrWhiteSpace(configuredValue)
            && decimal.TryParse(
                configuredValue,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal parsed)
            && parsed > 0)
        {
            return parsed;
        }

        return _defaultUsdToDop;
    }
}
