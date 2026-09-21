using ControlDeFinanzasIA.Application.Settings;

namespace ControlDeFinanzasIA.Application.Abstractions.Llm;

/// <summary>
/// Resuelve el <see cref="ILLMClient"/> correspondiente a la
/// configuración persistida, de forma síncrona y pura (F-013, D-3). No
/// lee settings por sí misma: los recibe del caller.
/// </summary>
public interface ILLMClientFactory
{
    /// <summary>Cliente para tráfico normal (pipeline con retry + circuit breaker).</summary>
    /// <param name="settings">Configuración persistida a resolver.</param>
    LlmClientResolution Resolve(AppSettings settings);

    /// <summary>
    /// Cliente para el botón "Probar conexión" de Settings: sin retry,
    /// sin circuit breaker (D-9).
    /// </summary>
    /// <param name="settings">Configuración persistida a resolver.</param>
    LlmClientResolution ResolveDiagnostic(AppSettings settings);
}
