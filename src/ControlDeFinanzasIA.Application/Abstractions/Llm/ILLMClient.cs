namespace ControlDeFinanzasIA.Application.Abstractions.Llm;

/// <summary>
/// Puerto hacia un proveedor de modelos de lenguaje (F-013, D-1). Las
/// implementaciones concretas viven en Infrastructure; esta interfaz, y
/// todo tipo que aparece en su firma, no tiene ninguna referencia a
/// <c>System.Net.*</c>.
/// </summary>
public interface ILLMClient
{
    /// <summary>Nombre del proveedor para mostrar en la UI. Nunca contiene credenciales.</summary>
    string ProviderName { get; }

    /// <summary>Envía una solicitud de completado al proveedor.</summary>
    /// <param name="request">Solicitud a enviar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <exception cref="LlmException">
    /// Cualquier fallo de transporte, protocolo o credenciales.
    /// </exception>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);

    /// <summary>
    /// Round trip de diagnóstico. NUNCA lanza; los fallos vuelven como
    /// un <see cref="LlmConnectionOutcome"/> (D-2).
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<LlmConnectionOutcome> TestConnectionAsync(CancellationToken ct = default);
}
