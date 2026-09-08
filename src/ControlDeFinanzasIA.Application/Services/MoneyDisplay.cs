using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.Services;

/// <summary>
/// Representacion de un monto para mostrar en la UI: incluye el
/// monto original y, opcionalmente, su equivalente convertido a la
/// otra moneda disponible.
/// </summary>
/// <remarks>
/// <para>
/// Se usa para que la UI renderice listas de transacciones con una
/// segunda linea mostrando el equivalente en la otra moneda, sin
/// recalcular en cada bind.
/// </para>
/// <para>
/// <see cref="Converted"/> es <see langword="null"/> cuando la
/// conversion fallo (provider no disponible). La UI debe ocultar la
/// segunda linea en ese caso y mostrar un statusMessage global.
/// </para>
/// </remarks>
/// <param name="Original">Monto original (siempre presente).</param>
/// <param name="Converted">
/// Equivalente en la otra moneda, o <see langword="null"/> si la
/// conversion fallo.
/// </param>
/// <param name="ProviderName">
/// Nombre del proveedor de tasas (ej. <c>"stub"</c>,
/// <c>"openexchangerates"</c>). Util para tooltip de auditoria.
/// Puede ser <see langword="null"/> si no se llego a consultar al
/// provider (ej. <c>source.Currency == target</c>).
/// </param>
public sealed record MoneyDisplay(
    Money Original,
    Money? Converted,
    string? ProviderName);
