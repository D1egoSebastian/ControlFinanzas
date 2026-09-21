namespace ControlDeFinanzasIA.Application.Abstractions.Llm;

/// <summary>
/// Resultado de un round trip de diagnóstico
/// (<see cref="ILLMClient.TestConnectionAsync"/>). Nunca se lanza como
/// excepción (F-013, D-2).
/// </summary>
/// <param name="Status">Estado de la conexión.</param>
/// <param name="ProviderName">Nombre del proveedor probado.</param>
public sealed record LlmConnectionOutcome(LlmConnectionStatus Status, string ProviderName)
{
    /// <summary>Atajo booleano: <c>true</c> si <see cref="Status"/> es <see cref="LlmConnectionStatus.Ok"/>.</summary>
    public bool IsOk => Status == LlmConnectionStatus.Ok;
}

/// <summary>
/// Estado de un round trip de diagnóstico (F-013, D-2). Deliberadamente
/// NO incluye un valor "no configurado": eso lo decide
/// <see cref="LlmConfigurationStatus"/>, ANTES de que exista un cliente
/// con el cual hacer el round trip.
/// </summary>
public enum LlmConnectionStatus
{
    /// <summary>El proveedor respondió correctamente.</summary>
    Ok = 0,

    /// <summary>Credenciales inválidas (401/403).</summary>
    InvalidCredentials = 1,

    /// <summary>El proveedor no respondió (red, timeout, 5xx, etc.).</summary>
    Unavailable = 2,
}
