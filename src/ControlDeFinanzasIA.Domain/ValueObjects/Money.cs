using System.Globalization;
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Domain.ValueObjects;

/// <summary>
/// Value object inmutable que representa una cantidad de dinero
/// en una moneda específica. Dos <see cref="Money"/> son iguales
/// si tienen el mismo <see cref="Amount"/> y la misma
/// <see cref="Currency"/>.
///
/// No se puede construir directamente: el único camino es a través
/// de <see cref="Create"/>, que valida los argumentos. Esto
/// garantiza que toda instancia cumple las invariantes del tipo
/// (<see cref="Amount"/> no negativo y finito).
/// </summary>
public sealed record Money
{
    /// <summary>
    /// Cantidad de dinero. Invariante: siempre es un valor finito
    /// mayor o igual a cero. Validado en <see cref="Create"/>.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// Moneda del <see cref="Amount"/>. No puede ser nula (es un enum).
    /// </summary>
    public Currency Currency { get; }

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Crea un <see cref="Money"/> validando que el monto sea
    /// no-negativo. Es el único camino válido para construir una
    /// instancia.
    /// </summary>
    /// <param name="amount">Cantidad. Debe ser &gt;= 0.</param>
    /// <param name="currency">Moneda del monto.</param>
    /// <returns>Una instancia válida de <see cref="Money"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se lanza si <paramref name="amount"/> es negativo.
    /// </exception>
    /// <remarks>
    /// <b>Nota sobre la finitud:</b> la spec original (F-002) pedía
    /// rechazar también <see cref="decimal.NaN"/>,
    /// <see cref="decimal.PositiveInfinity"/> y
    /// <see cref="decimal.NegativeInfinity"/>. Sin embargo, la
    /// implementación actual de <c>decimal</c> en la BCL de .NET 8
    /// (System.Private.CoreLib 8.0.28) no expone esos valores:
    /// las constantes <c>decimal.NaN</c> etc. no existen, los métodos
    /// <c>decimal.IsFinite</c> / <c>IsNaN</c> / <c>IsInfinity</c> no
    /// existen, y el constructor <c>new decimal(int, int, int, bool,
    /// byte)</c> rechaza explícitamente <c>scale &gt; 28</c> con
    /// <see cref="ArgumentOutOfRangeException"/>. Por tanto, ningún
    /// <c>decimal</c> válido puede ser NaN/Infinity en esta versión de
    /// la BCL, y el chequeo de finitud es <i>vacuo</i>. Cuando la BCL
    /// añada estos valores (esperado en .NET 9+), la validación
    /// deberá reintroducirse.
    /// </remarks>
    public static Money Create(decimal amount, Currency currency)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount, "Amount must be non-negative.");
        }
        return new Money(amount, currency);
    }

    /// <summary>
    /// Devuelve un <see cref="Money"/> con <see cref="Amount"/> = 0
    /// en la moneda indicada. Útil como valor inicial de una
    /// agregación (<c>Aggregate</c> de LINQ) o como total de un
    /// mes sin gastos.
    /// </summary>
    /// <param name="currency">Moneda del cero.</param>
    /// <returns>Un <see cref="Money"/> con Amount = 0 y la moneda dada.</returns>
    public static Money Zero(Currency currency) => Create(0m, currency);

    /// <summary>
    /// Suma este <see cref="Money"/> con <paramref name="other"/>,
    /// ambos deben tener la misma <see cref="Currency"/>. Devuelve
    /// un nuevo <see cref="Money"/> con la suma de los montos.
    /// </summary>
    /// <param name="other">Operando derecho de la suma.</param>
    /// <returns>
    /// Un nuevo <see cref="Money"/> con
    /// <c>Amount = this.Amount + other.Amount</c> y la misma
    /// <see cref="Currency"/> que <c>this</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Se lanza si <paramref name="other"/> es <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Se lanza si <paramref name="other"/>.Currency difiere de
    /// <c>this</c>.Currency. La conversión entre monedas NO es
    /// responsabilidad de <see cref="Money"/>; ocurre en la capa
    /// de presentación con un proveedor de tasas de cambio.
    /// </exception>
    /// <remarks>
    /// Si la suma excede <see cref="decimal.MaxValue"/>, la
    /// operación lanza <see cref="OverflowException"/>. Esto es
    /// aceptable (el caller lo trata) y consistente con la
    /// aritmética de <see cref="decimal"/>.
    /// </remarks>
    public Money Add(Money other)
    {
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }
        if (other.Currency != this.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot operate on Money with different currencies: {this.Currency} vs {other.Currency}.");
        }
        return new Money(this.Amount + other.Amount, this.Currency);
    }

    /// <summary>
    /// Resta <paramref name="other"/> de este <see cref="Money"/>.
    /// Ambos deben tener la misma <see cref="Currency"/>. El resultado
    /// no puede ser negativo: si la resta daría un monto negativo,
    /// se lanza <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="other">Operando derecho de la resta.</param>
    /// <returns>
    /// Un nuevo <see cref="Money"/> con
    /// <c>Amount = this.Amount - other.Amount</c> y la misma
    /// <see cref="Currency"/> que <c>this</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Se lanza si <paramref name="other"/> es <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Se lanza si <paramref name="other"/>.Currency difiere de
    /// <c>this</c>.Currency, o si la resta resultaría en un
    /// <see cref="Amount"/> negativo.
    /// </exception>
    /// <remarks>
    /// Si la resta produce un valor inferior a
    /// <see cref="decimal.MinValue"/>, la operación lanza
    /// <see cref="OverflowException"/> antes de la validación de
    /// negatividad. Esto es aceptable: la invariante principal
    /// ("Amount no es negativo") se mantiene para todos los
    /// <see cref="Money"/> construidos exitosamente.
    /// </remarks>
    public Money Subtract(Money other)
    {
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }
        if (other.Currency != this.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot operate on Money with different currencies: {this.Currency} vs {other.Currency}.");
        }
        decimal result = this.Amount - other.Amount;
        if (result < 0)
        {
            throw new InvalidOperationException(
                $"Subtraction would result in negative Money: {this.Amount} - {other.Amount} = {result} {this.Currency}.");
        }
        return new Money(result, this.Currency);
    }

    /// <summary>
    /// Multiplica este <see cref="Money"/> por el escalar
    /// <paramref name="scalar"/>. El escalar debe ser no-negativo.
    /// </summary>
    /// <param name="scalar">
    /// Factor de multiplicación. Debe ser &gt;= 0.
    /// </param>
    /// <returns>
    /// Un nuevo <see cref="Money"/> con
    /// <c>Amount = this.Amount * scalar</c> y la misma
    /// <see cref="Currency"/> que <c>this</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se lanza si <paramref name="scalar"/> es negativo. El
    /// mensaje de la excepción incluye el valor recibido para
    /// facilitar el diagnóstico.
    /// </exception>
    /// <remarks>
    /// Si la multiplicación excede <see cref="decimal.MaxValue"/>,
    /// la operación lanza <see cref="OverflowException"/>. Esto es
    /// aceptable (el caller lo trata) y consistente con la
    /// aritmética de <see cref="decimal"/>.
    /// </remarks>
    public Money Multiply(decimal scalar)
    {
        if (scalar < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scalar), scalar, "Scalar must be non-negative.");
        }

        return new Money(this.Amount * scalar, this.Currency);
    }

    /// <summary>
    /// Returns a formatted string representation of this <see cref="Money"/>
    /// using the provided <paramref name="cultureInfo"/>.
    /// </summary>
    /// <param name="cultureInfo">The culture used to format the amount.</param>
    /// <returns>A string in the form "&lt;amount&gt; &lt;currency&gt;".</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cultureInfo"/> is <see langword="null"/>.
    /// </exception>
    public string ToString(CultureInfo? cultureInfo)
    {
        ArgumentNullException.ThrowIfNull(cultureInfo);
        return $"{Amount.ToString("N2", cultureInfo)} {Currency}";
    }

    /// <summary>
    /// Returns a formatted string representation of this <see cref="Money"/>
    /// using <see cref="CultureInfo.CurrentCulture"/>.
    /// </summary>
    /// <returns>A string in the form "&lt;amount&gt; &lt;currency&gt;".</returns>
    public override string ToString() => ToString(CultureInfo.CurrentCulture);
}


