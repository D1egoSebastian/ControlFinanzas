using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Settings;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Infrastructure.Llm;

/// <summary>
/// Implementación de <see cref="ILLMClientFactory"/>: resolutor síncrono y
/// puro sobre <see cref="AppSettings"/> (F-013, slice 4, R-2, D-3). No lee
/// settings por sí misma — los recibe del caller — y no realiza ninguna
/// llamada HTTP: solo construye el adaptador correcto sobre un
/// <see cref="HttpClient"/> ya nombrado, resuelto vía
/// <see cref="IHttpClientFactory"/>.
/// </summary>
public sealed class LLMClientFactory : ILLMClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Crea una nueva <see cref="LLMClientFactory"/>.</summary>
    /// <param name="httpClientFactory">
    /// Fábrica de <see cref="HttpClient"/>s nombrados, registrada por
    /// <see cref="LlmServiceCollectionExtensions.AddLlmClients"/> (D-9).
    /// </param>
    /// <param name="loggerFactory">Fábrica de loggers para los adaptadores construidos.</param>
    public LLMClientFactory(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public LlmClientResolution Resolve(AppSettings settings) => ResolveCore(settings, diagnostic: false);

    /// <inheritdoc />
    public LlmClientResolution ResolveDiagnostic(AppSettings settings) => ResolveCore(settings, diagnostic: true);

    /// <summary>
    /// Resuelve la base URL efectiva para <paramref name="provider"/>: la
    /// configurada si no está vacía, o el default fijo por proveedor
    /// (F-013, D-3). <c>null</c> para <see cref="AiProvider.Custom"/> sin
    /// base URL configurada — un error de configuración, nunca una
    /// llamada HTTP.
    /// </summary>
    /// <param name="provider">Proveedor configurado.</param>
    /// <param name="configuredBaseUrl">Base URL persistida, puede ser <c>null</c>/vacía.</param>
    internal static string? ResolveBaseUrl(AiProvider provider, string? configuredBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl;
        }

        return provider switch
        {
            AiProvider.OpenAI => "https://api.openai.com/v1",
            AiProvider.OpenRouter => "https://openrouter.ai/api/v1",
            AiProvider.Anthropic => "https://api.anthropic.com/v1",
            AiProvider.Custom => null,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown AiProvider."),
        };
    }

    /// <summary>
    /// Nombre del <see cref="HttpClient"/> registrado por
    /// <see cref="LlmServiceCollectionExtensions.AddLlmClients"/> para
    /// <paramref name="provider"/> (D-9): <c>llm-{provider}</c> para
    /// tráfico normal, <c>llm-{provider}-diagnostic</c> para el pipeline
    /// sin retry/breaker del botón "Probar conexión".
    /// </summary>
    /// <param name="provider">Proveedor configurado.</param>
    /// <param name="diagnostic">
    /// <see langword="true"/> para el cliente de diagnóstico.
    /// </param>
    internal static string BuildClientName(AiProvider provider, bool diagnostic) =>
        diagnostic ? $"llm-{provider}-diagnostic" : $"llm-{provider}";

    private LlmClientResolution ResolveCore(AppSettings settings, bool diagnostic)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Checked before the base-URL branch, so a Custom provider with
        // neither reports the missing key first — the thing the user is
        // most likely to have forgotten (D-3).
        if (string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            return LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingApiKey);
        }

        string? baseUrl = ResolveBaseUrl(settings.AiProvider, settings.AiBaseUrl);
        if (baseUrl is null)
        {
            return LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingBaseUrl);
        }

        string clientName = BuildClientName(settings.AiProvider, diagnostic);
        HttpClient http = _httpClientFactory.CreateClient(clientName);

        ILLMClient client = settings.AiProvider == AiProvider.Anthropic
            ? new AnthropicClient(http, settings, baseUrl, _loggerFactory.CreateLogger<AnthropicClient>())
            : new OpenAICompatibleClient(http, settings, baseUrl, _loggerFactory.CreateLogger<OpenAICompatibleClient>());

        return LlmClientResolution.Configured(client);
    }
}
