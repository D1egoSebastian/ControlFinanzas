namespace ControlDeFinanzasIA.Application.Abstractions.Llm;

/// <summary>
/// Respuesta de un <see cref="ILLMClient.CompleteAsync"/> exitoso
/// (F-013, D-1).
/// </summary>
/// <param name="Content">Texto crudo devuelto por el modelo.</param>
/// <param name="TokensUsed">Tokens totales consumidos por la llamada.</param>
/// <param name="LatencyMs">Latencia de la llamada, en milisegundos.</param>
public sealed record LlmResponse(string Content, int TokensUsed, long LatencyMs);
