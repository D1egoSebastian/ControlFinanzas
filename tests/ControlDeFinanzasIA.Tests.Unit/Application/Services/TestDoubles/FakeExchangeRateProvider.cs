using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Services.TestDoubles;

/// <summary>
/// Test double de <see cref="IExchangeRateProvider"/> que permite
/// inyectar tasas controladas o simular errores del provider.
/// </summary>
public sealed class FakeExchangeRateProvider : IExchangeRateProvider
{
    private readonly Func<Currency, Currency, ExchangeRate>? _onGetRate;
    private readonly Exception? _throwOnGetRate;

    /// <summary>
    /// Cantidad de veces que se llamo a <see cref="GetRateAsync"/>.
    /// </summary>
    public int CallCount { get; private set; }

    /// <inheritdoc />
    public string ProviderName { get; }

    /// <summary>
    /// Crea un fake que resuelve tasas mediante <paramref name="onGetRate"/>.
    /// </summary>
    /// <param name="onGetRate">Factory de tasas controladas.</param>
    /// <param name="providerName">Nombre del proveedor fake.</param>
    public FakeExchangeRateProvider(
        Func<Currency, Currency, ExchangeRate> onGetRate,
        string providerName = "fake")
    {
        _onGetRate = onGetRate;
        ProviderName = providerName;
    }

    /// <summary>
    /// Crea un fake que lanza <paramref name="throwOnGetRate"/> en cada llamada.
    /// </summary>
    /// <param name="throwOnGetRate">Excepcion a lanzar.</param>
    /// <param name="providerName">Nombre del proveedor fake.</param>
    public FakeExchangeRateProvider(
        Exception throwOnGetRate,
        string providerName = "fake-throwing")
    {
        _throwOnGetRate = throwOnGetRate;
        ProviderName = providerName;
    }

    /// <inheritdoc />
    public Task<ExchangeRate> GetRateAsync(
        Currency from, Currency to, CancellationToken ct = default)
    {
        CallCount++;
        ct.ThrowIfCancellationRequested();
        if (_throwOnGetRate is not null)
        {
            throw _throwOnGetRate;
        }

        return Task.FromResult(_onGetRate!(from, to));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<CurrencyPair, decimal>> GetLatestRatesAsync(
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyDictionary<CurrencyPair, decimal>>(
            new Dictionary<CurrencyPair, decimal>());
    }
}
