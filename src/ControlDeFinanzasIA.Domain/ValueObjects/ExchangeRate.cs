using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Domain.ValueObjects;

/// <summary>
/// Value object inmutable que representa una tasa de cambio entre
/// dos monedas en un instante de tiempo. Dos <see cref="ExchangeRate"/>
/// son iguales si tienen el mismo <see cref="From"/>, <see cref="To"/>,
/// <see cref="Rate"/>, <see cref="AsOf"/> y <see cref="Source"/>.
/// </summary>
/// <remarks>
/// <para>
/// No se puede construir directamente con <c>new</c>: el único camino
/// válido es a través de <see cref="Create"/>, que valida todas
/// las invariantes (RN-1, RN-2, RN-3).
/// </para>
/// <para>
/// La conversión entre monedas NO es responsabilidad de
/// <see cref="ExchangeRate"/> directamente (eso es del
/// <c>ICurrencyConverter</c> en feature 5-009). Aquí solo
/// proveemos <see cref="Convert"/> como primitiva aritmética.
/// </para>
/// <para>
/// <b>Nota sobre la finitud:</b> la spec original pedía rechazar
/// también <see cref="decimal.NaN"/>,
/// <see cref="decimal.PositiveInfinity"/> y
/// <see cref="decimal.NegativeInfinity"/>. Sin embargo, la
/// implementación actual de <c>decimal</c> en la BCL de .NET 8
/// no expone esos valores: las constantes <c>decimal.NaN</c> etc.
/// no existen, los métodos <c>decimal.IsFinite</c> /
/// <c>IsNaN</c> / <c>IsInfinity</c> no existen, y el constructor
/// <c>new decimal(int, int, int, bool, byte)</c> rechaza
/// explícitamente <c>scale &gt; 28</c> con
/// <see cref="ArgumentOutOfRangeException"/>. Por tanto, ningún
/// <c>decimal</c> válido puede ser NaN/Infinity en esta versión de
/// la BCL, y el chequeo de finitud es <i>vacuo</i>. Cuando la BCL
/// añada estos valores (esperado en .NET 9+), la validación
/// deberá reintroducirse.
/// </para>
/// </remarks>
public sealed record ExchangeRate
{
    /// <summary>
    /// Moneda origen de la tasa.
    /// </summary>
    public Currency From { get; }

    /// <summary>
    /// Moneda destino de la tasa.
    /// </summary>
    public Currency To { get; }

    /// <summary>
    /// Factor de conversión. Invariante: siempre es un valor
    /// estrictamente positivo. Validado en <see cref="Create"/>.
    /// </summary>
    public decimal Rate { get; }

    /// <summary>
    /// Instante en que la tasa fue obtenida. Siempre UTC.
    /// </summary>
    public DateTime AsOf { get; }

    /// <summary>
    /// Identificador del origen de la tasa (p. ej. <c>"stub-fixed"</c>).
    /// </summary>
    public string Source { get; }

    private ExchangeRate(
        Currency from,
        Currency to,
        decimal rate,
        DateTime asOf,
        string source)
    {
        From = from;
        To = to;
        Rate = rate;
        AsOf = asOf;
        Source = source;
    }

    /// <summary>
    /// Crea un <see cref="ExchangeRate"/> validando las invariantes:
    /// <list type="bullet">
    ///   <item><description><see cref="From"/> != <see cref="To"/> (RN-3), excepto cuando <paramref name="rate"/> == 1m (tasa identidad).</description></item>
    ///   <item><description><see cref="Rate"/> &gt; 0 (RN-2).</description></item>
    ///   <item><description><see cref="AsOf"/>.Kind == Utc.</description></item>
    ///   <item><description><see cref="Source"/> no vacío ni whitespace.</description></item>
    /// </list>
    /// </summary>
    /// <param name="from">Moneda origen.</param>
    /// <param name="to">Moneda destino.</param>
    /// <param name="rate">Tasa de conversión (&gt; 0).</param>
    /// <param name="asOf">Instante de la tasa (Utc).</param>
    /// <param name="source">Identificador del origen de la tasa.</param>
    /// <returns>Una instancia válida de <see cref="ExchangeRate"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Si <paramref name="from"/> == <paramref name="to"/> y
    /// <paramref name="rate"/> != 1m, <paramref name="asOf"/>.Kind != Utc, o
    /// <paramref name="source"/> es vacío/whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <paramref name="rate"/> &lt;= 0.
    /// </exception>
    public static ExchangeRate Create(
        Currency from,
        Currency to,
        decimal rate,
        DateTime asOf,
        string source)
    {
        if (from == to && rate != 1m)
        {
            throw new ArgumentException(
                $"From and To must differ unless the rate is the identity (1). Received: from={from}, to={to}, rate={rate}.",
                nameof(from));
        }

        if (rate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rate), rate, "Rate must be strictly positive.");
        }

        if (asOf.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                $"AsOf must be UTC. Received Kind={asOf.Kind}.",
                nameof(asOf));
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException(
                "Source must be a non-empty string.",
                nameof(source));
        }

        return new ExchangeRate(from, to, rate, asOf, source);
    }

    /// <summary>
    /// Convierte <paramref name="amount"/> (asumido en
    /// <see cref="From"/>) a su equivalente en <see cref="To"/>,
    /// aplicando <see cref="Rate"/> con redondeo bancario a 4
    /// decimales (alineado con <c>Money.HasPrecision(18, 4)</c>).
    /// </summary>
    /// <param name="amount">Monto en <see cref="From"/>.</param>
    /// <returns>
    /// Monto equivalente en <see cref="To"/>, redondeado a 4
    /// decimales.
    /// </returns>
    /// <exception cref="OverflowException">
    /// Si <paramref name="amount"/> * <see cref="Rate"/> excede
    /// <see cref="decimal.MaxValue"/>.
    /// </exception>
    /// <remarks>
    /// No valida <paramref name="amount"/> (delega al caller; mismo
    /// patrón que <c>Money.Multiply</c>).
    /// </remarks>
    public decimal Convert(decimal amount)
        => Math.Round(amount * Rate, 4, MidpointRounding.ToEven);
}
