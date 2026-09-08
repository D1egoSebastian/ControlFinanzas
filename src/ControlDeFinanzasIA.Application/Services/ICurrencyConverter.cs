using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.Services;

/// <summary>
/// Servicio de aplicacion que envuelve <see cref="Abstractions.IExchangeRateProvider"/>
/// con cache TTL y manejo defensivo de errores. Es la capa que la
/// UI y los use cases consumen para obtener montos convertidos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que en Application (no Infrastructure):</b> el converter
/// es logica de aplicacion pura (orquestacion de cache + provider +
/// logging). No accede a BD, archivos, ni red. Sigue el patron
/// "use case como orquestador" del Clean Architecture del repo.
/// </para>
/// <para>
/// <b>Errores como valores:</b> <see cref="ConvertAsync"/> retorna
/// <see cref="Result"/> con codigo <c>"ExchangeRateUnavailable"</c>
/// cuando el provider falla. <see cref="DisplayAsync"/> nunca falla
/// estructuralmente: retorna <see cref="MoneyDisplay"/> con
/// <c>Converted: null</c> en caso de fallo.
/// </para>
/// </remarks>
public interface ICurrencyConverter
{
    /// <summary>
    /// Convierte <paramref name="source"/> a la moneda
    /// <paramref name="target"/>.
    /// </summary>
    /// <param name="source">Monto a convertir.</param>
    /// <param name="target">Moneda destino.</param>
    /// <param name="ct">Token de cancelacion opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con <c>IsSuccess = true</c> y
    /// <c>Value = Money</c> convertido en exito; o
    /// <c>Error.Code = "ExchangeRateUnavailable"</c> si el provider
    /// fallo. Si <paramref name="source"/>.Currency ==
    /// <paramref name="target"/>, retorna <paramref name="source"/>
    /// sin consultar cache ni provider.
    /// </returns>
    Task<Result> ConvertAsync(
        Money source,
        Currency target,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene la representacion para mostrar en la UI: monto
    /// original + equivalente en la otra moneda disponible.
    /// </summary>
    /// <param name="source">Monto original.</param>
    /// <param name="ct">Token de cancelacion opcional.</param>
    /// <returns>
    /// <see cref="MoneyDisplay"/> con <c>Converted</c> poblado en
    /// exito, o <see langword="null"/> si la conversion fallo. La
    /// direccion de la conversion es implicita: si source es USD,
    /// convierte a DOP; si source es DOP, convierte a USD.
    /// </returns>
    Task<MoneyDisplay> DisplayAsync(
        Money source,
        CancellationToken ct = default);
}
