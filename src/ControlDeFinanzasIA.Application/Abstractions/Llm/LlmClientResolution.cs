namespace ControlDeFinanzasIA.Application.Abstractions.Llm;

/// <summary>
/// Resultado de <see cref="ILLMClientFactory.Resolve"/> /
/// <see cref="ILLMClientFactory.ResolveDiagnostic"/> (F-013, D-3).
/// </summary>
/// <param name="Client">
/// Cliente listo para usar, o <c>null</c> cuando <paramref name="Status"/>
/// no es <see cref="LlmConfigurationStatus.Configured"/>.
/// </param>
/// <param name="Status">Estado de configuración resuelto.</param>
public sealed record LlmClientResolution(ILLMClient? Client, LlmConfigurationStatus Status)
{
    /// <summary>Crea una resolución exitosa con un cliente listo para usar.</summary>
    /// <param name="client">Cliente resuelto.</param>
    public static LlmClientResolution Configured(ILLMClient client) =>
        new(client, LlmConfigurationStatus.Configured);

    /// <summary>
    /// Crea una resolución de "no configurado" — sin cliente, sin llamada
    /// HTTP.
    /// </summary>
    /// <param name="reason">Motivo por el cual no se pudo resolver un cliente.</param>
    public static LlmClientResolution NotConfigured(LlmConfigurationStatus reason) =>
        new(null, reason);
}

/// <summary>
/// Motivo por el cual <see cref="ILLMClientFactory"/> pudo o no resolver
/// un cliente listo para usar (F-013, D-3).
/// </summary>
public enum LlmConfigurationStatus
{
    /// <summary>Hay API key y, si aplica, base URL configuradas.</summary>
    Configured = 0,

    /// <summary>No hay API key configurada, para ningún proveedor.</summary>
    MissingApiKey = 1,

    /// <summary>Proveedor <c>Custom</c> sin base URL configurada.</summary>
    MissingBaseUrl = 2,
}
