using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Domain.Catalog;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Expenses.CategorizeExpense;

/// <summary>
/// Sugiere una categoría existente para la descripción de un gasto,
/// llamando a un <see cref="ILLMClient"/> a través de <see cref="ILLMClientFactory"/>
/// (F-013, slice 5b, R-3, R-4, R-6, R-7, D-6, D-7, D-8). El constructor NO
/// recibe <c>HttpClient</c>, <c>IHttpClientFactory</c> ni ningún tipo
/// <c>System.Net.*</c> — esa firma ES la garantía de aislamiento HTTP de
/// Application para esta fase, asserteable por
/// <c>LlmContractPurityTests</c> y por lectura directa.
/// </summary>
/// <remarks>
/// No es <see langword="sealed"/> y <see cref="ExecuteAsync"/> es
/// <see langword="virtual"/> — la única desviación de la convención
/// "sealed" de los demás use cases de este codebase. Es deliberada: el
/// slice 6 (UI) necesita una <c>FakeCategorizeExpenseUseCase</c> que
/// controle exactamente cuándo una sugerencia completa, se cancela o
/// lanza, sin levantar un fake HTTP — y este tipo concreto (no una
/// interfaz) es lo que <c>ExpensesViewModel</c> recibe como su 17° parámetro
/// de constructor opcional (D-12).
/// </remarks>
public class CategorizeExpenseUseCase
{
    /// <summary>Longitud mínima, en caracteres, para considerar una descripción elegible (R-6, D-7).</summary>
    public const int MinimumDescriptionLength = 4;

    /// <summary>Longitud máxima, en caracteres, enviada al modelo (R-6, D-7).</summary>
    public const int MaxDescriptionLength = 200;

    /// <summary>Cota superior de <c>MaxTokens</c> para esta llamada, independiente de <see cref="AppSettings.AiMaxTokensPerRequest"/> (R-6).</summary>
    public const int MaxTokensCeiling = 128;

    /// <summary>
    /// Clasificar dentro de una lista cerrada no es una tarea creativa.
    /// Esta llamada SIEMPRE envía 0.0 e IGNORA deliberadamente
    /// <see cref="AppSettings.AiTemperature"/> (R-7): la no-determinismo
    /// aquí significaría que la misma descripción produce categorías
    /// distintas en días distintos, algo inaceptable en un libro mayor.
    /// <see cref="AppSettings.AiTemperature"/> sigue siendo el control del
    /// usuario para las funciones narrativas de la Fase 14, que es para lo
    /// que fue diseñado.
    /// </summary>
    public const double ClassificationTemperature = 0.0d;

    /// <summary>
    /// Ceiling de defensa en profundidad (D-7 paso 8): se mantiene incluso
    /// si un futuro proveedor se registrara sin el handler de resiliencia,
    /// y es el único límite que existe en un test unitario donde no hay
    /// handler.
    /// </summary>
    public static readonly TimeSpan HardTimeout = TimeSpan.FromSeconds(15);

    private readonly ISettingsStore _settingsStore;
    private readonly ILLMClientFactory _factory;
    private readonly ICategoryRepository _categories;
    private readonly IPromptProvider _prompts;
    private readonly ILogger<CategorizeExpenseUseCase> _logger;

    /// <summary>Inicializa una nueva instancia de <see cref="CategorizeExpenseUseCase"/>.</summary>
    /// <param name="settingsStore">Fuente de <see cref="AppSettings"/> — nunca lanza, nunca devuelve null.</param>
    /// <param name="factory">Resuelve el <see cref="ILLMClient"/> configurado, o "no configurado".</param>
    /// <param name="categories">Repositorio de categorías. Solo se invoca <see cref="ICategoryRepository.GetAllAsync"/> (D-6, S-11).</param>
    /// <param name="prompts">Proveedor de plantillas de prompt (D-4).</param>
    /// <param name="logger">Logger para diagnóstico y el Warning con correlation id (D-8).</param>
    public CategorizeExpenseUseCase(
        ISettingsStore settingsStore,
        ILLMClientFactory factory,
        ICategoryRepository categories,
        IPromptProvider prompts,
        ILogger<CategorizeExpenseUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsStore = settingsStore;
        _factory = factory;
        _categories = categories;
        _prompts = prompts;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el flujo de categorización automática.
    /// </summary>
    /// <param name="command">Comando con la descripción cruda del gasto.</param>
    /// <param name="ct">Token de cancelación del caller (p. ej. el CTS por-invocación de la ViewModel).</param>
    /// <returns>
    /// <see cref="Result.Success(object)"/> con un <see cref="CategorySuggestionDto"/>
    /// en TODO round trip limpio — incluyendo "no hubo match" (D-7). Solo
    /// falla con código <c>"AiNotConfigured"</c> (sin llamada HTTP) o
    /// <c>"LLMUnavailable"</c> (fallo del pipeline).
    /// </returns>
    public virtual async Task<Result> ExecuteAsync(CategorizeExpenseCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var correlationId = Guid.NewGuid();

        string description = (command.Description ?? string.Empty).Trim();
        if (description.Length < MinimumDescriptionLength)
        {
            // El guarda de longitud vive primero en la ViewModel; esto es
            // defensa en profundidad para cualquier otro caller (D-7).
            return Result.Failure("AiNotConfigured", "La descripción es demasiado corta para sugerir una categoría.");
        }

        if (description.Length > MaxDescriptionLength)
        {
            description = description[..MaxDescriptionLength];
        }

        AppSettings settings = await _settingsStore.GetAppSettingsAsync(ct).ConfigureAwait(false);

        LlmClientResolution resolution = _factory.Resolve(settings);
        if (resolution.Status != LlmConfigurationStatus.Configured || resolution.Client is null)
        {
            _logger.LogDebug(
                "AI categorization skipped: not configured. CorrelationId={CorrelationId} Status={Status}",
                correlationId,
                resolution.Status);
            return Result.Failure("AiNotConfigured", "La IA no está configurada.");
        }

        ILLMClient client = resolution.Client;

        IReadOnlyList<Category> categories = await _categories.GetAllAsync(ct).ConfigureAwait(false);
        if (categories.Count == 0)
        {
            // Una lista cerrada de cero opciones tiene exactamente una
            // respuesta posible; pagarle a un proveedor por descubrirla es
            // indefendible (D-7).
            return Result.Success(new CategorySuggestionDto(null, null, false, string.Empty));
        }

        string categoryList = BuildCategoryList(categories);
        string systemTemplate = _prompts.GetTemplate(PromptNames.CategorizeExpenseSystem, settings.Locale);
        string userTemplate = _prompts.GetTemplate(PromptNames.CategorizeExpenseUser, settings.Locale);

        string systemPrompt = systemTemplate.Replace("{{categories}}", categoryList, StringComparison.Ordinal);
        string userPrompt = userTemplate
            .Replace("{{description}}", description, StringComparison.Ordinal)
            .Replace("{{categories}}", categoryList, StringComparison.Ordinal);

        var request = new LlmRequest
        {
            Messages =
            [
                new LlmMessage(LlmRole.System, systemPrompt),
                new LlmMessage(LlmRole.User, userPrompt),
            ],
            Model = settings.AiModel,
            Temperature = ClassificationTemperature,
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

            LlmCategoryAnswer answer = LlmCategoryAnswerParser.Parse(response.Content);
            string rawTruncated = Truncate(response.Content, MaxDescriptionLength);

            _logger.LogDebug(
                "AI categorization completed. CorrelationId={CorrelationId} Provider={Provider} AnswerKind={AnswerKind}",
                correlationId,
                client.ProviderName,
                answer.Kind);

            if (answer.Kind != LlmAnswerKind.Name || answer.CategoryName is null)
            {
                return Result.Success(new CategorySuggestionDto(null, null, false, rawTruncated));
            }

            Category? matched = MatchCategory(answer.CategoryName, categories);
            CategorySuggestionDto dto = matched is null
                ? new CategorySuggestionDto(null, null, false, rawTruncated)
                : new CategorySuggestionDto(matched.Id, matched.Name, true, rawTruncated);

            return Result.Success(dto);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // El caller (típicamente la ViewModel) superseó esta solicitud.
            // Reportarlo como fallo encendería "IA no disponible" en un
            // sistema perfectamente sano — el caller lo descarta en
            // silencio (D-7).
            throw;
        }
        catch (LlmException ex)
        {
            _logger.LogWarning(
                ex,
                "AI categorization failed. CorrelationId={CorrelationId} Provider={Provider} Kind={Kind}",
                correlationId,
                client.ProviderName,
                ex.Kind);
            return Result.Failure("LLMUnavailable", "No se pudo obtener una sugerencia de categoría.");
        }
        catch (Exception ex)
        {
            // Amplio a propósito: ninguna excepción puede llegar a la UI
            // (D-7). Cubre timeout del CTS enlazado (15s) y cualquier
            // fallo de transporte/JSON no traducido a LlmException.
            _logger.LogWarning(
                ex,
                "AI categorization failed unexpectedly. CorrelationId={CorrelationId} Provider={Provider}",
                correlationId,
                client.ProviderName);
            return Result.Failure("LLMUnavailable", "No se pudo obtener una sugerencia de categoría.");
        }
    }

    /// <summary>
    /// Empareja <paramref name="candidateName"/> contra <paramref name="categories"/>
    /// en dos niveles (D-6): nivel 1 exacto ordinal-insensible-a-mayúsculas
    /// sobre <see cref="CategoryNameNormalizer.Clean"/>, evaluado sobre
    /// TODAS las categorías antes de intentar el nivel 2; nivel 2 exacto
    /// ordinal sobre <see cref="CategoryNameNormalizer.Fold"/>. Más de un
    /// match en el nivel evaluado es ambigüedad → sin match.
    /// </summary>
    private static Category? MatchCategory(string candidateName, IReadOnlyList<Category> categories)
    {
        string cleanCandidate = CategoryNameNormalizer.Clean(candidateName);
        var tier1Matches = categories
            .Where(c => string.Equals(CategoryNameNormalizer.Clean(c.Name), cleanCandidate, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (tier1Matches.Count == 1)
        {
            return tier1Matches[0];
        }

        if (tier1Matches.Count > 1)
        {
            return null;
        }

        string foldCandidate = CategoryNameNormalizer.Fold(candidateName);
        var tier2Matches = categories
            .Where(c => string.Equals(CategoryNameNormalizer.Fold(c.Name), foldCandidate, StringComparison.Ordinal))
            .ToList();

        return tier2Matches.Count == 1 ? tier2Matches[0] : null;
    }

    /// <summary>
    /// Renderiza la lista cerrada de categorías para el prompt de usuario:
    /// <c>Name</c> más <c>Icon</c> como pista débil cuando está presente
    /// (D-4), una por línea.
    /// </summary>
    private static string BuildCategoryList(IReadOnlyList<Category> categories)
    {
        IEnumerable<string> lines = categories.Select(c =>
            string.IsNullOrWhiteSpace(c.Icon) ? c.Name : $"{c.Name} ({c.Icon})");
        return string.Join(Environment.NewLine, lines);
    }

    private static string Truncate(string value, int maxLength)
    {
        string safe = value ?? string.Empty;
        return safe.Length > maxLength ? safe[..maxLength] : safe;
    }
}
