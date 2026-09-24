using ControlDeFinanzasIA.Application.UseCases.Chat.BuildChatContext;

namespace ControlDeFinanzasIA.Application.UseCases.Chat.AskFinanceChat;

/// <summary>
/// Un intercambio pregunta/respuesta ya completado, usado como historia
/// a re-transmitir en la próxima llamada (F-015, D-5, R-5). Solo los
/// turnos con <see cref="ChatReplyStatus.Answered"/> llegan aquí — la
/// ViewModel (Slice S5) es la única que construye esta lista, y nunca
/// agrega un turno fallido, cancelado o de "no configurado".
/// </summary>
public sealed record ChatTurn(string Question, string Answer);

/// <summary>
/// Estado de una respuesta del chat financiero (F-015, D-5, D-10). Es el
/// único conjunto de "códigos de error": ninguna falla de IA llega como
/// <see cref="Application.Common.Result.Failure(string, string)"/>.
/// </summary>
public enum ChatReplyStatus
{
    /// <summary>El modelo respondió y el texto quedó disponible.</summary>
    Answered = 0,

    /// <summary>La IA no está configurada; cero llamadas HTTP.</summary>
    NotConfigured = 1,

    /// <summary>El pipeline de IA falló (excepción, timeout, respuesta vacía).</summary>
    Unavailable = 2,

    /// <summary>La pregunta no cumple la regla de longitud 1-500 (D-4, R-4).</summary>
    Rejected = 3,
}

/// <summary>
/// Resultado de <see cref="AskFinanceChatUseCase.ExecuteAsync"/> (F-015,
/// D-5, D-10). <see cref="Snapshot"/> y <see cref="Locale"/> se llevan
/// además del texto (reconciliación #7 del spec) para que el panel de
/// cifras exactas (R-10) renderice desde el mismo DTO y la misma cultura
/// que produjeron el contexto — incluso cuando <see cref="Status"/> es
/// <see cref="ChatReplyStatus.Unavailable"/>.
/// </summary>
public sealed record ChatReply(
    ChatReplyStatus Status,
    string? Text,
    ChatContextSnapshot? Snapshot = null,
    string? Locale = null);

/// <summary>
/// Resultado de <see cref="AskFinanceChatUseCase.GetAvailabilityAsync"/>
/// (F-015, D-5): la única forma en que la ViewModel detecta si la IA está
/// configurada, replicando el fallback de <c>TestAiConnectionUseCase</c>
/// cuando no lo está.
/// </summary>
public sealed record ChatAvailability(bool IsConfigured, string ProviderName);
