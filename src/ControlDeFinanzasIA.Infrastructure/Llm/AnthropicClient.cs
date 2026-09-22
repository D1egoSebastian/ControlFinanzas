using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Settings;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;

namespace ControlDeFinanzasIA.Infrastructure.Llm;

/// <summary>
/// Adaptador <see cref="ILLMClient"/> para la Messages API de Anthropic
/// (<see cref="AiProvider.Anthropic"/>) (F-013, slice 3, D-10). Construido
/// por la factory (slice 4), no por DI directa: el <see cref="HttpClient"/>,
/// los <see cref="AppSettings"/> y la base URL son valores por-resolución.
/// Deliberadamente NO es <see cref="OpenAICompatibleClient"/> con la URL
/// cambiada: el wire format de la Messages API difiere en la autenticación,
/// en cómo viaja el prompt de sistema y en la forma de la respuesta — ver
/// D-10 para la tabla completa de diferencias.
/// </summary>
public sealed class AnthropicClient : ILLMClient
{
    /// <summary>
    /// Versión de la Messages API anunciada en el header
    /// <c>anthropic-version</c>, requerido en cada request (D-10).
    /// </summary>
    private const string _anthropicVersion = "2023-06-01";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly AppSettings _settings;
    private readonly string _baseUrl;
    private readonly ILogger<AnthropicClient> _logger;

    /// <summary>Crea un nuevo <see cref="AnthropicClient"/>.</summary>
    /// <param name="http">Cliente HTTP resuelto por la factory (D-9).</param>
    /// <param name="settings">Preferencias del usuario en el momento de la resolución.</param>
    /// <param name="baseUrl">Base URL ya resuelta (D-3), sin barra final obligatoria.</param>
    /// <param name="logger">Logger de diagnóstico. Nunca registra la API key.</param>
    public AnthropicClient(
        HttpClient http,
        AppSettings settings,
        string baseUrl,
        ILogger<AnthropicClient> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(logger);

        _http = http;
        _settings = settings;
        _baseUrl = baseUrl;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderName => _settings.AiProvider.ToString();

    /// <inheritdoc />
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Metadata.TryGetValue("correlationId", out string? correlationId);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            MessagesRequestDto dto = BuildRequestDto(request);

            using HttpResponseMessage response = await SendAsync(dto, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw BuildFailureException(response);
            }

            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            MessagesResponseDto? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<MessagesResponseDto>(body, _jsonOptions);
            }
            catch (JsonException ex)
            {
                throw new LlmException(LlmFailureKind.Malformed, "The provider response could not be parsed.", ex);
            }

            string? content = envelope?.Content is { Count: > 0 } blocks ? blocks[0].Text : null;
            if (content is null)
            {
                throw new LlmException(LlmFailureKind.Malformed, "The provider response had no completion content.");
            }

            stopwatch.Stop();
            int tokensUsed = (envelope!.Usage?.InputTokens ?? 0) + (envelope.Usage?.OutputTokens ?? 0);
            _logger.LogDebug(
                "Anthropic completion ok. CorrelationId={CorrelationId} Provider={Provider} LatencyMs={LatencyMs}",
                correlationId,
                ProviderName,
                stopwatch.ElapsedMilliseconds);

            return new LlmResponse(content, tokensUsed, stopwatch.ElapsedMilliseconds);
        }
        catch (BrokenCircuitException ex)
        {
            // Catches both BrokenCircuitException and its subtype
            // IsolatedCircuitException (F-013, slice 4, Task 4.9, D-1,
            // D-10). Without this mapping, an open breaker would escape as
            // an unmapped Polly exception type, violating R-4's "every
            // pipeline failure is translated, no exception escapes to the
            // UI".
            throw new LlmException(LlmFailureKind.CircuitOpen, "The circuit breaker is open for this provider.", ex);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // The passed ct itself was NOT what fired: some other internal
            // mechanism (e.g. HttpClient.Timeout, a resilience attempt
            // timeout added in a later slice) did. That is a genuine
            // timeout, not the caller's own cancellation (D-10).
            throw new LlmException(LlmFailureKind.Timeout, "The request to the provider timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmException(LlmFailureKind.Transport, "A transport error occurred while calling the provider.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<LlmConnectionOutcome> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var dto = new MessagesRequestDto(
                _settings.AiModel,
                null,
                [new AnthropicMessageDto("user", "ping")],
                0.0,
                1);

            using HttpResponseMessage response = await SendAsync(dto, ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return new LlmConnectionOutcome(LlmConnectionStatus.Ok, ProviderName);
            }

            int status = (int)response.StatusCode;
            return status is 401 or 403
                ? new LlmConnectionOutcome(LlmConnectionStatus.InvalidCredentials, ProviderName)
                : new LlmConnectionOutcome(LlmConnectionStatus.Unavailable, ProviderName);
        }
        catch (Exception ex)
        {
            // Never throws (D-2): any transport, protocol or cancellation
            // failure comes back as Unavailable.
            _logger.LogDebug(ex, "Anthropic test connection failed. Provider={Provider}", ProviderName);
            return new LlmConnectionOutcome(LlmConnectionStatus.Unavailable, ProviderName);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(MessagesRequestDto dto, CancellationToken ct)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildUri("messages"))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json"),
        };

        // Anthropic authenticates via a dedicated header, never
        // Authorization: Bearer — a deliberate divergence from the
        // OpenAI-compatible adapter (D-10).
        httpRequest.Headers.Add("x-api-key", _settings.AiApiKey);
        httpRequest.Headers.Add("anthropic-version", _anthropicVersion);

        return await _http.SendAsync(httpRequest, ct).ConfigureAwait(false);
    }

    private Uri BuildUri(string path) => new($"{_baseUrl.TrimEnd('/')}/{path}");

    private MessagesRequestDto BuildRequestDto(LlmRequest request)
    {
        // The system prompt travels as a top-level `system` field, never as
        // a `role: "system"` message — the Messages API has no such role
        // (D-10). Anything else is a plain user/assistant turn.
        //
        // `LlmRequest` permits a flat list with any number of System
        // messages, but only one can be represented here. Dropping the
        // extras silently would lose caller data with no signal, so an
        // unrepresentable shape fails loudly instead. Phase 13's only
        // caller sends exactly one system message; the multi-turn shapes
        // of the chat phase are where this would otherwise have bitten.
        if (request.Messages.Count(m => m.Role == LlmRole.System) > 1)
        {
            throw new ArgumentException(
                "The Messages API represents the system prompt as a single top-level field, "
                + "so an LlmRequest carrying more than one System message cannot be sent faithfully.",
                nameof(request));
        }

        string? system = request.Messages
            .FirstOrDefault(m => m.Role == LlmRole.System)?.Content;

        var messages = request.Messages
            .Where(m => m.Role != LlmRole.System)
            .Select(m => new AnthropicMessageDto(MapRole(m.Role), m.Content))
            .ToList();

        return new MessagesRequestDto(
            request.Model ?? _settings.AiModel,
            system,
            messages,
            request.Temperature,
            request.MaxTokens);
    }

    private static string MapRole(LlmRole role) => role switch
    {
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown LlmRole for a Messages API turn."),
    };

    private static LlmException BuildFailureException(HttpResponseMessage response)
    {
        int status = (int)response.StatusCode;
        LlmFailureKind kind = status switch
        {
            401 or 403 => LlmFailureKind.InvalidCredentials,
            429 => LlmFailureKind.RateLimited,
            >= 500 and < 600 => LlmFailureKind.ServerError,
            _ => LlmFailureKind.Transport,
        };

        return new LlmException(kind, $"The provider returned HTTP {status}.");
    }

    private sealed record MessagesRequestDto(
        string Model,
        string? System,
        IReadOnlyList<AnthropicMessageDto> Messages,
        double Temperature,
        int MaxTokens);

    private sealed record AnthropicMessageDto(string Role, string Content);

    private sealed record MessagesResponseDto(
        IReadOnlyList<ContentBlockDto>? Content,
        UsageDto? Usage);

    private sealed record ContentBlockDto(string? Type, string? Text);

    private sealed record UsageDto(int InputTokens, int OutputTokens);
}
