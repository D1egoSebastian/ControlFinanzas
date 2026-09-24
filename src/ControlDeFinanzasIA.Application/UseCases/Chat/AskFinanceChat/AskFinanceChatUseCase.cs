using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Chat.BuildChatContext;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Chat.AskFinanceChat;

/// <summary>
/// Responde una pregunta de finanzas en lenguaje natural, fundamentada en
/// el <see cref="ChatContextSnapshot"/> ya calculado (F-015, R-4, R-5, R-6,
/// R-7, R-8, D-5). Copia línea por línea el esqueleto de guardas de
/// <c>CategorizeExpenseUseCase</c> (<see cref="ISettingsStore"/> →
/// <see cref="ILLMClientFactory.Resolve"/>, cero llamada HTTP si no está
/// configurada, CTS enlazado con timeout duro, <c>correlationId</c> en
/// <c>Metadata</c>) con la forma de resultado success-shaped de
/// <c>GetInsightsUseCase</c> (F-014): TODO desenlace es
/// <see cref="Result.Success(object)"/> con un <see cref="ChatReply"/> de
/// repliegue, NUNCA <see cref="Result.Failure(string, string)"/> por una
/// razón de IA.
/// </summary>
public sealed class AskFinanceChatUseCase
{
    /// <summary>Longitud máxima, en caracteres, de la pregunta recortada (D-4, R-4). Fuera de rango se rechaza, nunca se trunca.</summary>
    public const int MaxQuestionChars = 500;

    /// <summary>Cantidad máxima de mensajes re-transmitidos de historia: 3 pares pregunta/respuesta (D-5, R-5).</summary>
    public const int MaxReplayMessages = 6;

    /// <summary>Cota superior, en caracteres, de cada respuesta re-transmitida (D-5, R-5).</summary>
    public const int MaxReplayAssistantChars = 600;

    /// <summary>Cota superior, en caracteres, de la suma de toda la historia re-transmitida (D-5, R-5).</summary>
    public const int MaxReplayTotalChars = 4_000;

    /// <summary>Cota superior de <c>MaxTokens</c> para esta llamada, independiente de <see cref="AppSettings.AiMaxTokensPerRequest"/> (R-7).</summary>
    public const int MaxTokensCeiling = 500;

    /// <summary>Cota superior de <c>Temperature</c> para esta llamada, independiente de <see cref="AppSettings.AiTemperature"/> (R-7).</summary>
    public const double TemperatureCeiling = 0.3d;

    /// <summary>Cota superior, en caracteres, del texto de respuesta mostrado (R-7).</summary>
    public const int MaxReplyChars = 4_000;

    /// <summary>Timeout duro del CTS enlazado, igual que <c>CategorizeExpenseUseCase</c>/<c>GetInsightsUseCase</c> (D-16, R-6).</summary>
    public static readonly TimeSpan HardTimeout = TimeSpan.FromSeconds(15);

    private readonly ISettingsStore _settingsStore;
    private readonly ILLMClientFactory _factory;
    private readonly BuildChatContextQuery _buildContext;
    private readonly IPromptProvider _prompts;
    private readonly ILogger<AskFinanceChatUseCase> _logger;

    /// <summary>Inicializa una nueva instancia de <see cref="AskFinanceChatUseCase"/>.</summary>
    /// <param name="settingsStore">Fuente de <see cref="AppSettings"/> — nunca lanza, nunca devuelve null.</param>
    /// <param name="factory">Resuelve el <see cref="ILLMClient"/> configurado, o "no configurado".</param>
    /// <param name="buildContext">Compone el <see cref="ChatContextSnapshot"/> con las cifras deterministas (Slice S1).</param>
    /// <param name="prompts">Proveedor de plantillas de prompt (D-6).</param>
    /// <param name="logger">Logger para diagnóstico y el Warning con correlation id (R-6).</param>
    public AskFinanceChatUseCase(
        ISettingsStore settingsStore,
        ILLMClientFactory factory,
        BuildChatContextQuery buildContext,
        IPromptProvider prompts,
        ILogger<AskFinanceChatUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(buildContext);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsStore = settingsStore;
        _factory = factory;
        _buildContext = buildContext;
        _prompts = prompts;
        _logger = logger;
    }

    /// <summary>
    /// Indica si la IA está configurada, sin hacer ninguna llamada HTTP
    /// (D-5). Es la única forma en que la ViewModel detecta configuración;
    /// <see cref="ILLMClientFactory.Resolve"/> sigue siendo la única fuente
    /// de verdad.
    /// </summary>
    public async Task<ChatAvailability> GetAvailabilityAsync(CancellationToken ct = default)
    {
        AppSettings settings = await _settingsStore.GetAppSettingsAsync(ct).ConfigureAwait(false);
        LlmClientResolution resolution = _factory.Resolve(settings);

        return resolution.Status == LlmConfigurationStatus.Configured && resolution.Client is not null
            ? new ChatAvailability(true, resolution.Client.ProviderName)
            : new ChatAvailability(false, settings.AiProvider.ToString());
    }

    /// <summary>
    /// Ejecuta el flujo de pregunta-respuesta del chat financiero.
    /// </summary>
    /// <param name="command">Comando con la pregunta cruda y la historia previa.</param>
    /// <param name="ct">Token de cancelación del caller (Stop / navegar fuera del chat).</param>
    /// <returns>
    /// <see cref="Result.Success(object)"/> con un <see cref="ChatReply"/>
    /// en TODO desenlace. Nunca <see cref="Result.Failure(string, string)"/>.
    /// </returns>
    public async Task<Result> ExecuteAsync(AskFinanceChatCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var correlationId = Guid.NewGuid();

        // D-4: recortada, 1-500 caracteres. Fuera de rango se rechaza SIN
        // leer settings ni tocar el cliente — nunca se trunca.
        string question = (command.Question ?? string.Empty).Trim();
        if (question.Length is 0 or > MaxQuestionChars)
        {
            return Result.Success(new ChatReply(ChatReplyStatus.Rejected, null));
        }

        AppSettings settings = await _settingsStore.GetAppSettingsAsync(ct).ConfigureAwait(false);

        LlmClientResolution resolution = _factory.Resolve(settings);
        if (resolution.Status != LlmConfigurationStatus.Configured || resolution.Client is null)
        {
            _logger.LogDebug(
                "Chat question skipped: AI not configured. CorrelationId={CorrelationId} Status={Status}",
                correlationId,
                resolution.Status);
            return Result.Success(new ChatReply(ChatReplyStatus.NotConfigured, null));
        }

        ILLMClient client = resolution.Client;

        // BuildChatContextQuery nunca falla (D-1): cada sección se degrada
        // por su cuenta. Una cancelación del caller se propaga sin cambios
        // porque no está envuelta en el try/catch de más abajo.
        Result contextResult = await _buildContext.ExecuteAsync(ct).ConfigureAwait(false);
        var snapshot = (ChatContextSnapshot)contextResult.Value!;
        RenderedChatContext rendered = ChatContextRenderer.Render(snapshot, settings.Locale);

        string systemTemplate = _prompts.GetTemplate(PromptNames.ChatFinanceSystem, settings.Locale);
        string systemPrompt = systemTemplate.Replace("{{context}}", rendered.Text, StringComparison.Ordinal);

        IReadOnlyList<LlmMessage> replay = BuildReplay(command.PriorTurns);

        var messages = new List<LlmMessage>(replay.Count + 2) { new(LlmRole.System, systemPrompt) };
        messages.AddRange(replay);
        messages.Add(new LlmMessage(LlmRole.User, question));

        var request = new LlmRequest
        {
            Messages = messages,
            Model = settings.AiModel,
            Temperature = Math.Min(settings.AiTemperature, TemperatureCeiling),
            MaxTokens = Math.Min(settings.AiMaxTokensPerRequest, MaxTokensCeiling),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["correlationId"] = correlationId.ToString("N"),
            },
        };

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(HardTimeout);

        try
        {
            LlmResponse response = await client.CompleteAsync(request, linked.Token).ConfigureAwait(false);
            string text = (response.Content ?? string.Empty).Trim();

            _logger.LogDebug(
                "Chat question completed. CorrelationId={CorrelationId} QuestionLength={QuestionLength} ReplayMessages={ReplayMessages} ContextChars={ContextChars} OmittedSections={OmittedSections} Provider={Provider}",
                correlationId,
                question.Length,
                replay.Count,
                rendered.Text.Length,
                rendered.OmittedSections.Count,
                client.ProviderName);

            if (text.Length == 0)
            {
                return Result.Success(new ChatReply(ChatReplyStatus.Unavailable, null, snapshot, settings.Locale));
            }

            if (text.Length > MaxReplyChars)
            {
                text = string.Concat(text.AsSpan(0, MaxReplyChars - 1), "…");
            }

            return Result.Success(new ChatReply(ChatReplyStatus.Answered, text, snapshot, settings.Locale));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // El caller (típicamente la ViewModel, Stop o navegar fuera)
            // superseó esta solicitud. Se propaga sin cambios (R-6, R-9).
            throw;
        }
        catch (LlmException ex)
        {
            _logger.LogWarning(
                ex,
                "Chat question failed. CorrelationId={CorrelationId} Provider={Provider} Kind={Kind}",
                correlationId,
                client.ProviderName,
                ex.Kind);
            return Result.Success(new ChatReply(ChatReplyStatus.Unavailable, null, snapshot, settings.Locale));
        }
        catch (Exception ex)
        {
            // Amplio a propósito: ninguna excepción puede llegar a la UI
            // (R-6). Cubre el timeout del CTS enlazado (15s) y cualquier
            // fallo de transporte/JSON no traducido a LlmException.
            _logger.LogWarning(
                ex,
                "Chat question failed unexpectedly. CorrelationId={CorrelationId} Provider={Provider}",
                correlationId,
                client.ProviderName);
            return Result.Success(new ChatReply(ChatReplyStatus.Unavailable, null, snapshot, settings.Locale));
        }
    }

    /// <summary>
    /// Arma los mensajes de historia a re-transmitir (D-5, R-5): descarta
    /// turnos cuya pregunta no cumple la regla de longitud 1-500 o cuya
    /// respuesta está vacía, conserva únicamente los últimos
    /// <c>MaxReplayMessages / 2</c> pares, recorta cada respuesta a
    /// <see cref="MaxReplayAssistantChars"/> caracteres, y — si aun así el
    /// total supera <see cref="MaxReplayTotalChars"/> — descarta el par más
    /// antiguo primero. Los pares se descartan COMPLETOS para que la
    /// transcripción alterne User/Assistant siempre, tal como lo exige la
    /// API de Anthropic.
    /// </summary>
    private static IReadOnlyList<LlmMessage> BuildReplay(IReadOnlyList<ChatTurn>? priorTurns)
    {
        if (priorTurns is null || priorTurns.Count == 0)
        {
            return [];
        }

        var eligible = new List<ChatTurn>();
        foreach (ChatTurn turn in priorTurns)
        {
            string q = (turn.Question ?? string.Empty).Trim();
            string a = (turn.Answer ?? string.Empty).Trim();
            if (q.Length is 0 or > MaxQuestionChars || a.Length == 0)
            {
                continue;
            }

            string cappedAnswer = a.Length > MaxReplayAssistantChars
                ? string.Concat(a.AsSpan(0, MaxReplayAssistantChars - 1), "…")
                : a;

            eligible.Add(new ChatTurn(q, cappedAnswer));
        }

        int maxPairs = MaxReplayMessages / 2;
        List<ChatTurn> lastPairs = eligible.Count > maxPairs
            ? eligible.Skip(eligible.Count - maxPairs).ToList()
            : eligible;

        while (lastPairs.Count > 0 && TotalChars(lastPairs) > MaxReplayTotalChars)
        {
            lastPairs.RemoveAt(0);
        }

        var messages = new List<LlmMessage>(lastPairs.Count * 2);
        foreach (ChatTurn turn in lastPairs)
        {
            messages.Add(new LlmMessage(LlmRole.User, turn.Question));
            messages.Add(new LlmMessage(LlmRole.Assistant, turn.Answer));
        }

        return messages;
    }

    private static int TotalChars(IReadOnlyList<ChatTurn> turns) =>
        turns.Sum(t => t.Question.Length + t.Answer.Length);
}
