namespace ControlDeFinanzasIA.Application.Abstractions.Llm;

/// <summary>
/// Solicitud de completado dirigida a un <see cref="ILLMClient"/>
/// (F-013, D-1).
/// </summary>
public sealed record LlmRequest
{
    /// <summary>Mensajes de la conversación, en orden.</summary>
    public required IReadOnlyList<LlmMessage> Messages { get; init; }

    /// <summary>Id del modelo. <c>null</c> =&gt; el adaptador usa el de settings.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// Pista opcional de JSON schema. Ver D-10 para qué proveedores la
    /// reciben realmente.
    /// </summary>
    public string? JsonSchema { get; init; }

    /// <summary>Temperatura de muestreo del modelo.</summary>
    public double Temperature { get; init; }

    /// <summary>Límite de tokens de la respuesta.</summary>
    public int MaxTokens { get; init; }

    /// <summary>
    /// Metadata libre de la solicitud. Fase 13 transporta exactamente
    /// una entrada: <c>"correlationId"</c> (ver D-8). Comparer ordinal:
    /// las claves las autora el código como constantes, nunca las
    /// escribe un usuario.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
}
