using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.Services;

/// <inheritdoc cref="ICurrencyConverter"/>
public sealed class CurrencyConverter : ICurrencyConverter
{
    private const int _defaultCacheTtlMinutes = 60;
    private const string _cacheKeyPrefix = "exchange-rate";
    private const string _errorCodeUnavailable = "ExchangeRateUnavailable";
    private const string _errorCodeInvalidRate = "InvalidRate";

    private readonly IExchangeRateProvider _provider;
    private readonly IMemoryCache _cache;
    private readonly IClock _clock;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CurrencyConverter> _logger;
    private readonly TimeSpan _cacheTtl;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="CurrencyConverter"/>.
    /// </summary>
    /// <param name="provider">Proveedor de tasas de cambio.</param>
    /// <param name="cache">Cache en memoria para las tasas.</param>
    /// <param name="clock">Reloj del sistema.</param>
    /// <param name="configuration">Configuracion de la aplicacion.</param>
    /// <param name="logger">Logger del converter.</param>
    /// <exception cref="ArgumentNullException">
    /// Si alguno de los parametros es <see langword="null"/>.
    /// </exception>
    public CurrencyConverter(
        IExchangeRateProvider provider,
        IMemoryCache cache,
        IClock clock,
        IConfiguration configuration,
        ILogger<CurrencyConverter> logger)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _provider = provider;
        _cache = cache;
        _clock = clock;
        _configuration = configuration;
        _logger = logger;

        int ttlMinutes = _defaultCacheTtlMinutes;
        string? configuredValue = configuration["ExchangeRates:CacheTtlMinutes"];
        if (int.TryParse(configuredValue, out int parsed))
        {
            ttlMinutes = parsed;
        }

        if (ttlMinutes <= 0)
        {
            _logger.LogInformation(
                "Invalid ExchangeRates:CacheTtlMinutes ({Ttl}); using default {Default}.",
                ttlMinutes, _defaultCacheTtlMinutes);
            ttlMinutes = _defaultCacheTtlMinutes;
        }

        _cacheTtl = TimeSpan.FromMinutes(ttlMinutes);
    }

    /// <inheritdoc />
    public async Task<Result> ConvertAsync(
        Money source, Currency target, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Currency == target)
        {
            _logger.LogDebug(
                "Same-currency conversion for {Currency}; skipping provider.",
                source.Currency);
            return Result.Success(source);
        }

        ExchangeRate rate;
        string cacheKey = BuildCacheKey(source.Currency, target);

        if (_cache.TryGetValue(cacheKey, out ExchangeRate? cached) && cached is not null)
        {
            _logger.LogDebug(
                "Cache hit for {From}->{To}.",
                source.Currency, target);
            rate = cached;
        }
        else
        {
            _logger.LogInformation(
                "Cache miss for {From}->{To}; consulting provider {Provider}.",
                source.Currency, target, _provider.ProviderName);

            try
            {
                rate = await _provider.GetRateAsync(source.Currency, target, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Exchange rate provider {Provider} failed for {From}->{To}.",
                    _provider.ProviderName, source.Currency, target);
                return Result.Failure(_errorCodeUnavailable,
                    $"Provider threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (rate.Rate <= 0)
            {
                return Result.Failure(_errorCodeInvalidRate,
                    $"Provider returned non-positive rate: {rate.Rate}.");
            }

            _cache.Set(cacheKey, rate, _cacheTtl);
        }

        decimal convertedAmount = rate.Convert(source.Amount);
        var converted = Money.Create(convertedAmount, target);
        return Result.Success(converted);
    }

    /// <inheritdoc />
    public async Task<MoneyDisplay> DisplayAsync(
        Money source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        Currency target = source.Currency == Currency.USD
            ? Currency.DOP
            : Currency.USD;

        Result result = await ConvertAsync(source, target, ct).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            var converted = (Money)result.Value!;
            return new MoneyDisplay(source, converted, _provider.ProviderName);
        }

        return new MoneyDisplay(source, Converted: null, _provider.ProviderName);
    }

    private static string BuildCacheKey(Currency from, Currency to)
        => $"{_cacheKeyPrefix}:{from}:{to}";
}
