using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.Common;

/// <summary>
/// Par ordenado de monedas usado como clave en los diccionarios
/// de tasas del <c>IExchangeRateProvider</c>. Equality estructural
/// por valor (provisto por <c>record</c>).
/// </summary>
/// <param name="From">Moneda origen.</param>
/// <param name="To">Moneda destino.</param>
public sealed record CurrencyPair(Currency From, Currency To)
{
    /// <summary>
    /// Representación textual para debugging y logs:
    /// <c>"USD->DOP"</c>.
    /// </summary>
    /// <returns>Representación textual del par de monedas.</returns>
    public override string ToString() => $"{From}->{To}";
}
