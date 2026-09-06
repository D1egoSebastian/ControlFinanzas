namespace ControlDeFinanzasIA.Domain.Settings;

/// <summary>
/// Configuración del sistema. Las settings son pares clave-valor
/// que el sistema persiste. Por ejemplo, el nombre del usuario, la
/// zona horaria, las preferencias de notificación, etc.
/// </summary>
public sealed record Setting
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}
