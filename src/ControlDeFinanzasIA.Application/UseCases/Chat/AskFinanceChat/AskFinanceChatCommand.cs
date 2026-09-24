namespace ControlDeFinanzasIA.Application.UseCases.Chat.AskFinanceChat;

/// <summary>
/// Comando para <see cref="AskFinanceChatUseCase.ExecuteAsync"/> (F-015,
/// D-5). <see cref="PriorTurns"/> es la única fuente de historia a
/// re-transmitir (R-5) — la ViewModel (Slice S5) la construye a partir de
/// los turnos ya respondidos, nunca de turnos fallidos, cancelados o de
/// "no configurado".
/// </summary>
/// <param name="Question">Pregunta cruda del usuario, sin recortar (D-4).</param>
/// <param name="PriorTurns">Historia de la conversación, más reciente al final.</param>
public sealed record AskFinanceChatCommand(string Question, IReadOnlyList<ChatTurn> PriorTurns);
