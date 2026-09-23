using System.Globalization;
using System.Text.RegularExpressions;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.Common;
using ControlDeFinanzasIA.Domain.Budgets;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetInsights;

/// <summary>
/// Narra en 2-4 oraciones las cifras ya calculadas por los queries
/// deterministicos del dashboard (F-014, R-4, R-6, D-8). Copia el
/// esqueleto de guardas de <c>CategorizeExpenseUseCase</c> linea por
/// linea — <see cref="ISettingsStore"/> → <see cref="ILLMClientFactory.Resolve"/>,
/// cero llamada HTTP si no esta configurada, CTS enlazado con timeout duro,
/// <c>correlationId</c> en <c>Metadata</c> — con UNA diferencia deliberada:
/// cada camino no-feliz devuelve <see cref="Result.Success(object)"/> con un
/// <see cref="NarrativeStatus"/> de repliegue, nunca <see cref="Result.Failure(string, string)"/>.
/// Una tarjeta del dashboard siempre debe renderizar algo.
/// </summary>
public sealed class GetInsightsUseCase
{
    /// <summary>Cota superior de <c>MaxTokens</c> para esta llamada (D-8).</summary>
    public const int MaxTokensCeiling = 400;

    /// <summary>Longitud maxima del texto narrado tras la limpieza (D-8).</summary>
    public const int MaxNarrativeLength = 800;

    /// <summary>Timeout duro del CTS enlazado, igual que <c>CategorizeExpenseUseCase</c> (D-8).</summary>
    public static readonly TimeSpan HardTimeout = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan _regexTimeout = TimeSpan.FromMilliseconds(500);

    private static readonly Regex _fenceRegex = new(
        pattern: "^```(?:[a-zA-Z]*)?\\s*(?<body>[\\s\\S]*?)\\s*```$",
        options: RegexOptions.None,
        matchTimeout: _regexTimeout);

    private static readonly Regex _extraNewlinesRegex = new(
        pattern: "\\n{3,}",
        options: RegexOptions.None,
        matchTimeout: _regexTimeout);

    private readonly ISettingsStore _settingsStore;
    private readonly ILLMClientFactory _factory;
    private readonly IPromptProvider _prompts;
    private readonly DashboardNarrationCache _cache;
    private readonly ILogger<GetInsightsUseCase> _logger;

    /// <summary>Inicializa una nueva instancia de <see cref="GetInsightsUseCase"/>.</summary>
    public GetInsightsUseCase(
        ISettingsStore settingsStore,
        ILLMClientFactory factory,
        IPromptProvider prompts,
        DashboardNarrationCache cache,
        ILogger<GetInsightsUseCase> logger)
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
    /// Ejecuta la narracion de insights para <paramref name="facts"/>.
    /// </summary>
    /// <param name="facts">Cifras ya calculadas, unico insumo del prompt (S-12).</param>
    /// <param name="ct">Token de cancelacion del caller.</param>
    /// <returns>
    /// <see cref="Result.Success(object)"/> con un <see cref="InsightsNarrative"/>.
    /// Nunca <see cref="Result.Failure(string, string)"/> por una razon de IA.
    /// </returns>
    public async Task<Result> ExecuteAsync(InsightFacts facts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var correlationId = Guid.NewGuid();

        // Checked BEFORE settings (D-8): nada que narrar es cero llamadas,
        // sin siquiera leer la configuracion.
        if (facts.ExpensesDop == 0m && facts.IncomesDop == 0m)
        {
            return Result.Success(new InsightsNarrative(NarrativeStatus.Skipped, null));
        }

        AppSettings settings = await _settingsStore.GetAppSettingsAsync(ct).ConfigureAwait(false);

        LlmClientResolution resolution = _factory.Resolve(settings);
        if (resolution.Status != LlmConfigurationStatus.Configured || resolution.Client is null)
        {
            _logger.LogDebug(
                "AI insights skipped: not configured. CorrelationId={CorrelationId} Status={Status}",
                correlationId,
                resolution.Status);
            return Result.Success(new InsightsNarrative(NarrativeStatus.NotConfigured, null));
        }

        ILLMClient client = resolution.Client;
        CultureInfo culture = GetCulture(settings.Locale);

        string systemPrompt = _prompts.GetTemplate(PromptNames.GenerateInsightsSystem, settings.Locale);
        string userTemplate = _prompts.GetTemplate(PromptNames.GenerateInsightsUser, settings.Locale);
        string factsText = BuildFactsText(facts, culture);
        string userPrompt = userTemplate.Replace("{{facts}}", factsText, StringComparison.Ordinal);

        string cacheKey = DashboardNarrationCache.BuildKey(
            PromptNames.GenerateInsightsUser, settings.Locale, settings.AiProvider.ToString(), settings.AiModel, userPrompt);

        if (_cache.TryGetValue(cacheKey, out string cachedText))
        {
            return Result.Success(new InsightsNarrative(NarrativeStatus.Generated, cachedText));
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
            string cleaned = CleanNarrativeText(response.Content);

            _logger.LogDebug(
                "AI insights completed. CorrelationId={CorrelationId} Provider={Provider} Length={Length}",
                correlationId,
                client.ProviderName,
                cleaned.Length);

            if (cleaned.Length == 0)
            {
                return Result.Success(new InsightsNarrative(NarrativeStatus.Unavailable, null));
            }

            _cache.Set(cacheKey, cleaned, NarrativeStatus.Generated);
            return Result.Success(new InsightsNarrative(NarrativeStatus.Generated, cleaned));
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
                "AI insights failed. CorrelationId={CorrelationId} Provider={Provider} Kind={Kind}",
                correlationId,
                client.ProviderName,
                ex.Kind);
            return Result.Success(new InsightsNarrative(NarrativeStatus.Unavailable, null));
        }
        catch (Exception ex)
        {
            // Amplio a proposito: ninguna excepcion debe llegar a la UI
            // (D-8). Cubre el timeout del CTS enlazado y cualquier fallo de
            // transporte/JSON no traducido a LlmException.
            _logger.LogWarning(
                ex,
                "AI insights failed unexpectedly. CorrelationId={CorrelationId} Provider={Provider}",
                correlationId,
                client.ProviderName);
            return Result.Success(new InsightsNarrative(NarrativeStatus.Unavailable, null));
        }
    }

    /// <summary>
    /// Renderiza <paramref name="facts"/> como pares clave=valor, al
    /// estilo del ejemplo de linea de anomalia de D-8. Cada cifra numerica
    /// se copia exacta y textualmente desde <paramref name="facts"/> — el
    /// modelo nunca recibe una cifra que este texto no contenga (S-12).
    /// </summary>
    private static string BuildFactsText(InsightFacts facts, CultureInfo culture)
    {
        var lines = new List<string>
        {
            $"month=\"{facts.Month:yyyy-MM}\"",
            $"expenses=\"{FormatDop(facts.ExpensesDop, culture)}\"",
            $"incomes=\"{FormatDop(facts.IncomesDop, culture)}\"",
            $"netBalance=\"{FormatDop(facts.NetBalanceDop, culture)}\"",
        };

        if (facts.ExpensesDeltaPercent is decimal delta)
        {
            lines.Add($"expensesDeltaPercent=\"{delta.ToString("+0.00;-0.00", culture)}%\"");
        }

        if (facts.TopCategories.Count > 0)
        {
            IEnumerable<string> categoryLines = facts.TopCategories.Select((c, i) =>
                $"{i + 1}. label=\"{Sanitize(c.Label)}\"; amount=\"{FormatDop(c.AmountDop, culture)}\"");
            lines.Add($"topCategories: {string.Join(" | ", categoryLines)}");
        }

        if (facts.TopBudgetLines.Count > 0)
        {
            IEnumerable<string> budgetLines = facts.TopBudgetLines.Select((b, i) =>
                $"{i + 1}. category=\"{Sanitize(b.CategoryName)}\"; percentConsumed=\"{FormatPercent(b.PercentConsumed, culture)}\"; status={FormatStatus(b.Status)}");
            lines.Add($"topBudgetLines: {string.Join(" | ", budgetLines)}");
        }

        if (facts.ProjectedDop is decimal projected)
        {
            lines.Add($"projectedEndOfMonth=\"{FormatDop(projected, culture)}\"");
        }

        if (facts.BudgetTotalDop is decimal budgetTotal)
        {
            lines.Add($"budgetTotal=\"{FormatDop(budgetTotal, culture)}\"");
        }

        lines.Add($"anomalyCount={facts.AnomalyCount.ToString(CultureInfo.InvariantCulture)}");

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatDop(decimal amount, CultureInfo culture) =>
        $"{amount.ToString("N2", culture)} DOP";

    private static string FormatPercent(decimal? percent, CultureInfo culture) =>
        percent is decimal value ? $"{value.ToString("N2", culture)}%" : "n/a";

    private static string FormatStatus(BudgetStatus? status) =>
        status?.ToString() ?? "Indeterminate";

    private static string Sanitize(string value) =>
        value.Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

    /// <summary>
    /// Limpia el texto crudo del modelo (D-8): despoja una cerca de codigo
    /// envolvente, recorta, colapsa 3+ saltos de linea consecutivos, y lo
    /// acota a <see cref="MaxNarrativeLength"/> caracteres.
    /// </summary>
    private static string CleanNarrativeText(string? rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return string.Empty;
        }

        string trimmed = rawContent.Trim();
        Match fenceMatch = _fenceRegex.Match(trimmed);
        string unfenced = fenceMatch.Success ? fenceMatch.Groups["body"].Value.Trim() : trimmed;
        string collapsed = _extraNewlinesRegex.Replace(unfenced, "\n\n").Trim();

        return collapsed.Length > MaxNarrativeLength ? collapsed[..MaxNarrativeLength] : collapsed;
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
