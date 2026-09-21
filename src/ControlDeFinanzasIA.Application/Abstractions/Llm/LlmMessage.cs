namespace ControlDeFinanzasIA.Application.Abstractions.Llm;

/// <summary>
/// Un mensaje individual dentro de una conversación enviada a un
/// proveedor LLM (F-013, D-1).
/// </summary>
/// <param name="Role">Rol del emisor del mensaje.</param>
/// <param name="Content">Contenido textual del mensaje.</param>
public sealed record LlmMessage(LlmRole Role, string Content);

/// <summary>Rol del emisor de un <see cref="LlmMessage"/>.</summary>
public enum LlmRole
{
    /// <summary>Instrucciones de sistema / contexto del prompt.</summary>
    System = 0,

    /// <summary>Mensaje del usuario.</summary>
    User = 1,

    /// <summary>Respuesta previa del modelo (no usado en Fase 13).</summary>
    Assistant = 2,
}
