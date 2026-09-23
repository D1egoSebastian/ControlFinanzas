using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.DetectAnomalies;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetAnomalyNarrative;

/// <summary>
/// Narra por lotes, en UNA sola llamada al modelo, las anomalias con
/// mayor puntaje del mes (F-014, R-5, R-6, D-8, D-9). Copia el esqueleto
/// de guardas de <c>CategorizeExpenseUseCase</c> — <see cref="ISettingsStore"/>
/// → <see cref="ILLMClientFactory.Resolve"/>, cero llamada HTTP si no esta
/// configurada, CTS enlazado con timeout duro, <c>correlationId</c> en
/// <c>Metadata</c> — con la misma diferencia deliberada que
/// <c>GetInsightsUseCase</c>: cada camino no-feliz es
/// <see cref="Result.Success(object)"/> con un <see cref="NarrativeStatus"/>
/// de repliegue, nunca <see cref="Result.Failure(string, string)"/>.
/// </summary>
public sealed class GetAnomalyNarrativeUseCase
{
    /// <summary>Cota superior de <c>MaxTokens</c> para esta llamada (D-8).</summary>
    public const int MaxTokensCeiling = 600;

    /// <summary>
    /// Cantidad maxima de anomalias narradas por lote (D-11). El caller
    /// (<c>DetectAnomaliesUseCase</c>) ya entrega la lista en orden del
    /// detector, asi que esto es simplemente "los primeros N" (R-5).
    /// </summary>
    public const int MaxNarratedAnomalies = 5;

    /// <summary>Longitud maxima de la descripcion saneada enviada al modelo (D-8).</summary>
    public const int MaxDescriptionLength = 80;

    /// <summary>Timeout duro del CTS enlazado, igual que <c>CategorizeExpenseUseCase</c> (D-8).</summary>
    public static readonly TimeSpan HardTimeout = TimeSpan.FromSeconds(15);

    private static readonly IReadOnlyDictionary<Guid, string> _emptyMessages =
        new Dictionary<Guid, string>();

    private readonly ISettingsStore _settingsStore;
    private readonly ILLMClientFactory _factory;
    private readonly IPromptProvider _prompts;
    private readonly DashboardNarrationCache _cache;
    private readonly ILogger<GetAnomalyNarrativeUseCase> _logger;

    /// <summary>Inicializa una nueva instancia de <see cref="GetAnomalyNarrativeUseCase"/>.</summary>
    public GetAnomalyNarrativeUseCase(
        ISettingsStore settingsStore,
        ILLMClientFactory factory,
        IPromptProvider prompts,
        DashboardNarrationCache cache,
        ILogger<GetAnomalyNarrativeUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsStore = settingsStore;
        _factory = factory;
        _prompts = prompts;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la narracion por lotes de las anomalias con mayor puntaje.
    /// </summary>
    /// <param name="anomalies">
    /// Anomalias del mes, ya en orden del detector. Solo las primeras
    /// <see cref="MaxNarratedAnomalies"/> se envian al modelo (R-5).
    /// </param>
    /// <param name="ct">Token de cancelacion del caller.</param>
    /// <returns>
    /// <see cref="Result.Success(object)"/> con un <see cref="AnomalyNarration"/>.
    /// Nunca <see cref="Result.Failure(string, string)"/> por una razon de IA.
    /// </returns>
    public async Task<Result> ExecuteAsync(IReadOnlyList<AnomalyItemDto> anomalies, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(anomalies);

        var correlationId = Guid.NewGuid();

        // Checked BEFORE settings (D-8): una lista vacia es cero llamadas,
        // sin siquiera leer la configuracion.
        if (anomalies.Count == 0)
        {
            return Result.Success(new AnomalyNarration(NarrativeStatus.Skipped, _emptyMessages));
        }

        var capped = anomalies.Take(MaxNarratedAnomalies).ToList();

        AppSettings settings = await _settingsStore.GetAppSettingsAsync(ct).ConfigureAwait(false);

        LlmClientResolution resolution = _factory.Resolve(settings);
        if (resolution.Status != LlmConfigurationStatus.Configured || resolution.Client is null)
        {
            _logger.LogDebug(
                "AI anomaly narration skipped: not configured. CorrelationId={CorrelationId} Status={Status}",
                correlationId,
                resolution.Status);
            return Result.Success(new AnomalyNarration(NarrativeStatus.NotConfigured, _emptyMessages));
        }

        ILLMClient client = resolution.Client;
        CultureInfo culture = GetCulture(settings.Locale);

        // El mapa ordinal -> AlertId vive SOLO aqui; el modelo nunca ve un GUID (D-8).
        var alertIdByOrdinal = new Dictionary<int, Guid>();
        for (int i = 0; i < capped.Count; i++)
        {
            alertIdByOrdinal[i + 1] = capped[i].AlertId;
        }

        string systemPrompt = _prompts.GetTemplate(PromptNames.ExplainAnomaliesSystem, settings.Locale);
        string userTemplate = _prompts.GetTemplate(PromptNames.ExplainAnomaliesUser, settings.Locale);
        string batchText = BuildBatchText(capped, culture);
        string userPrompt = userTemplate.Replace("{{anomalies}}", batchText, StringComparison.Ordinal);

        string cacheKey = DashboardNarrationCache.BuildKey(
            PromptNames.ExplainAnomaliesUser, settings.Locale, settings.AiProvider.ToString(), settings.AiModel, userPrompt);

        if (_cache.TryGetValue(cacheKey, out IReadOnlyDictionary<Guid, string> cachedMessages))
        {
            return Result.Success(new AnomalyNarration(NarrativeStatus.Generated, cachedMessages));
        }

        var request = new LlmRequest
        {
            Messages =
            [
                new LlmMessage(LlmRole.System, systemPrompt),
                new LlmMessage(LlmRole.User, userPrompt),
            ],
            Model = settings.AiModel,
            Temperature = settings.AiTemperature,
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
            IReadOnlyDictionary<int, string> parsed = AnomalyNarrationParser.Parse(response.Content, capped.Count);

            _logger.LogDebug(
                "AI anomaly narration completed. CorrelationId={CorrelationId} Provider={Provider} ParsedCount={ParsedCount}",
                correlationId,
                client.ProviderName,
                parsed.Count);

            if (parsed.Count == 0)
            {
                return Result.Success(new AnomalyNarration(NarrativeStatus.Unavailable, _emptyMessages));
            }

            Dictionary<Guid, string> messagesByAlertId = new();
            foreach (KeyValuePair<int, string> entry in parsed)
            {
                if (alertIdByOrdinal.TryGetValue(entry.Key, out Guid alertId))
                {
                    messagesByAlertId[alertId] = entry.Value;
                }
            }

            _cache.Set(cacheKey, (IReadOnlyDictionary<Guid, string>)messagesByAlertId, NarrativeStatus.Generated);
            return Result.Success(new AnomalyNarration(NarrativeStatus.Generated, messagesByAlertId));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // El caller supersedio esta solicitud (D-8) — se descarta en
            // silencio; no es un fallo de IA.
            throw;
        }
        catch (LlmException ex)
        {
            _logger.LogWarning(
                ex,
                "AI anomaly narration failed. CorrelationId={CorrelationId} Provider={Provider} Kind={Kind}",
                correlationId,
                client.ProviderName,
                ex.Kind);
            return Result.Success(new AnomalyNarration(NarrativeStatus.Unavailable, _emptyMessages));
        }
        catch (Exception ex)
        {
            // Amplio a proposito: ninguna excepcion debe llegar a la UI
            // (D-8). Cubre el timeout del CTS enlazado y cualquier fallo de
            // transporte/JSON no traducido a LlmException.
            _logger.LogWarning(
                ex,
                "AI anomaly narration failed unexpectedly. CorrelationId={CorrelationId} Provider={Provider}",
                correlationId,
                client.ProviderName);
            return Result.Success(new AnomalyNarration(NarrativeStatus.Unavailable, _emptyMessages));
        }
    }

    /// <summary>
    /// Renderiza el lote con claves ordinales cortas, al estilo del
    /// ejemplo de D-8: <c>1. category="..."; description="..."; date=...;
    /// amount="..."; usual="..."; ratio="..."; severity=...</c>. El
    /// <c>AlertId</c>/<c>ExpenseId</c> reales nunca aparecen aqui.
    /// </summary>
    private static string BuildBatchText(IReadOnlyList<AnomalyItemDto> items, CultureInfo culture)
    {
        var lines = new List<string>(items.Count);

        for (int i = 0; i < items.Count; i++)
        {
            AnomalyItemDto item = items[i];
            string description = Sanitize(item.Description, MaxDescriptionLength);
            string category = Sanitize(item.CategoryName, MaxDescriptionLength);

            lines.Add(
                $"{i + 1}. category=\"{category}\"; description=\"{description}\"; " +
                $"date={item.Date:yyyy-MM-dd}; amount=\"{FormatDop(item.AmountDop, culture)}\"; " +
                $"usual=\"{FormatDop(item.MedianDop, culture)}\"; ratio=\"{item.RatioToMedian.ToString("0.00", culture)}x\"; " +
                $"severity={item.Severity}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatDop(decimal amount, CultureInfo culture) =>
        $"{amount.ToString("N2", culture)} DOP";

    private static string Sanitize(string value, int maxLength)
    {
        string cleaned = (value ?? string.Empty)
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
    }

    private static CultureInfo GetCulture(string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
