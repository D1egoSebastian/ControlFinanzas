namespace ControlDeFinanzasIA.Application.UseCases.Settings.TestAiConnection;

/// <summary>
/// Resultado del diagnóstico "Probar conexión" de Settings (F-013, slice 7,
/// R-8, D-11). Siempre viaja dentro de un <c>Result.Success</c> — las
/// cuatro variantes de <see cref="AiConnectionResultKind"/> son respuestas
/// válidas de un diagnóstico que completó su trabajo, nunca un fallo del
/// use case.
/// </summary>
/// <param name="Kind">Resultado discriminado del diagnóstico.</param>
/// <param name="ProviderName">
/// Nombre del proveedor probado. Nunca contiene la API key ni la base URL.
/// </param>
public sealed record TestAiConnectionDto(AiConnectionResultKind Kind, string ProviderName);

/// <summary>
/// Resultado discriminado de <see cref="TestAiConnectionUseCase.ExecuteAsync"/>
/// (F-013, slice 7, R-8, D-11).
/// </summary>
public enum AiConnectionResultKind
{
    /// <summary>El proveedor respondió correctamente.</summary>
    Ok = 0,

    /// <summary>No hay proveedor/API key configurados; no se hizo ninguna llamada.</summary>
    NotConfigured = 1,

    /// <summary>El proveedor rechazó las credenciales (401/403).</summary>
    InvalidCredentials = 2,

    /// <summary>El proveedor no respondió (red, timeout, 5xx, etc.).</summary>
    Unavailable = 3,
}
