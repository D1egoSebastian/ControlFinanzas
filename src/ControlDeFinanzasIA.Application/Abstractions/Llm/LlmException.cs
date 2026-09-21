namespace ControlDeFinanzasIA.Application.Abstractions.Llm;

/// <summary>
/// Excepción lanzada por <see cref="ILLMClient.CompleteAsync"/> ante
/// cualquier fallo de transporte, protocolo o credenciales (F-013, D-1).
/// El mensaje nunca contiene la API key ni el cuerpo crudo de la
/// respuesta del proveedor.
/// </summary>
public sealed class LlmException : Exception
{
    /// <summary>Crea una nueva <see cref="LlmException"/>.</summary>
    /// <param name="kind">Categoría del fallo.</param>
    /// <param name="message">Mensaje descriptivo, sin credenciales.</param>
    /// <param name="inner">Excepción original, si aplica.</param>
    public LlmException(LlmFailureKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }

    /// <summary>Categoría del fallo.</summary>
    public LlmFailureKind Kind { get; }
}

/// <summary>
/// Categoría de fallo de un <see cref="ILLMClient"/> (F-013, D-1). Existe
/// para que Application distinga tipos de fallo sin necesitar
/// <c>System.Net.HttpStatusCode</c> ni ningún otro tipo
/// <c>System.Net.*</c>. Deliberadamente <c>&lt;c&gt;</c> y no
/// <c>&lt;see cref&gt;</c>: R-1 prohíbe la referencia incluso en
/// documentación XML.
/// </summary>
public enum LlmFailureKind
{
    /// <summary>Fallo de conexión, DNS o socket.</summary>
    Transport = 0,

    /// <summary>Timeout de intento o total, o cancelación del pipeline de resiliencia.</summary>
    Timeout = 1,

    /// <summary>Credenciales inválidas (401/403).</summary>
    InvalidCredentials = 2,

    /// <summary>Rate limit (429) tras agotar el presupuesto de reintentos.</summary>
    RateLimited = 3,

    /// <summary>Error de servidor (5xx) tras agotar el presupuesto de reintentos.</summary>
    ServerError = 4,

    /// <summary>Respuesta 2xx cuyo sobre no pudo leerse en absoluto.</summary>
    Malformed = 5,

    /// <summary>El circuit breaker está abierto.</summary>
    CircuitOpen = 6,
}
