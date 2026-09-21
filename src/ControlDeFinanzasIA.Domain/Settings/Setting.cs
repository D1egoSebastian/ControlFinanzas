namespace ControlDeFinanzasIA.Domain.Settings;

/// <summary>
/// Configuración del sistema. Las settings son pares clave-valor
/// que el sistema persiste. Por ejemplo, el nombre del usuario, la
/// zona horaria, las preferencias de notificación, etc.
/// </summary>
/// <remarks>
/// <para>
/// Sigue siendo un <see langword="sealed record" /> (no la reescritura a
/// clase+factory usada por <c>Budget</c>/<c>Goal</c> en las Fases 10/11):
/// una fila <c>(Key, Value)</c> no tiene ninguna invariante mas alla de
/// que <see cref="Key"/> no este vacia, asi que un factory <c>Create</c>
/// seria ceremonia para una regla que cabe en el propio accessor
/// <see langword="init"/> de <see cref="Key"/>. Ver design F-012 D-1.
/// </para>
/// <para>
/// <b>Conocido y aceptado:</b> EF Core materializa las propiedades
/// <see langword="init"/> escribiendo el campo de respaldo directamente,
/// no el accessor, asi que las filas ya existentes en la base de datos
/// no se re-validan al leerlas. Es el comportamiento deseado: una fila
/// corrupta degrada a un default en vez de tumbar una query.
/// </para>
/// </remarks>
public sealed record Setting
{
    /// <summary>Longitud maxima de <see cref="Key"/>.</summary>
    public const int MaxKeyLength = 128;

    /// <summary>Longitud maxima de <see cref="Value"/>.</summary>
    public const int MaxValueLength = 2048;

    private readonly string _key = string.Empty;

    /// <summary>Identificador unico de la fila.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Clave canonica de la setting. Ver <c>SettingKeys</c> en la capa
    /// Application. Nunca vacia ni compuesta solo de espacios.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Se lanza si el valor asignado es <see langword="null"/>, vacio o
    /// solo espacios en blanco.
    /// </exception>
    public string Key
    {
        get => _key;
        init => _key = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Setting.Key must be non-empty.", nameof(value))
            : value.Trim();
    }

    /// <summary>
    /// Valor serializado de la setting. Vacio ES un estado legal (p. ej.
    /// una API key todavia no configurada), por lo que no lleva guarda.
    /// </summary>
    public string Value { get; init; } = string.Empty;
}
