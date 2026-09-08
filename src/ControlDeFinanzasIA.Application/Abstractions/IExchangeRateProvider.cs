using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Abstracción sobre una fuente de tasas de cambio. La
/// implementación por defecto en Fase 5 es
/// <c>StubExchangeRateProvider</c> (tasas fijas locales). Las
/// features 5-009+ consumirán este contrato sin acoplarse a una
/// API externa.
/// </summary>
/// <remarks>
/// <para>
/// El adapter real (OpenExchangeRates, exchangerate.host, banco
/// central RD) se difiere a Fase 6+. Cuando llegue, se enchufa
/// implementando esta misma interface.
/// </para>
/// <para>
/// <b>Thread-safety:</b> las implementaciones deben ser
/// thread-safe (se asume llamada concurrente desde la UI).
/// </para>
/// </remarks>
public interface IExchangeRateProvider
{
    /// <summary>
    /// Nombre del proveedor, útil para logging y debugging
    /// (ej. <c>"stub"</c>, <c>"openexchangerates"</c>).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Obtiene la tasa vigente de <paramref name="from"/> a
    /// <paramref name="to"/>.
    /// </summary>
    /// <param name="from">Moneda origen.</param>
    /// <param name="to">Moneda destino.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="ExchangeRate"/> vigente. Si <paramref name="from"/>
    /// == <paramref name="to"/>, retorna una tasa identidad
    /// (<c>Rate = 1m</c>, <c>Source = "identity"</c>) sin
    /// consultar la fuente.
    /// </returns>
    Task<ExchangeRate> GetRateAsync(
        Currency from,
        Currency to,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene todas las tasas disponibles en un solo batch.
    /// Útil para popular dropdowns o validar conversiones
    /// múltiples en una sola llamada a la fuente.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// Dictionary inmutable con todas las tasas vigentes. En Fase
    /// 5 son 2 pares (USD&lt;-&gt;DOP en ambas direcciones).
    /// </returns>
    Task<IReadOnlyDictionary<CurrencyPair, decimal>> GetLatestRatesAsync(
        CancellationToken ct = default);
}
