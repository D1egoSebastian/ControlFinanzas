using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Settings;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;

namespace ControlDeFinanzasIA.Infrastructure.Llm;

/// <summary>
/// Adaptador <see cref="ILLMClient"/> para proveedores con formato de
/// wire compatible con OpenAI: <see cref="AiProvider.OpenAI"/>,
/// <see cref="AiProvider.OpenRouter"/> y <see cref="AiProvider.Custom"/>
/// (F-013, slice 2, D-10). Construido por la factory (slice 4), no por
/// DI directa: el <see cref="HttpClient"/>, los <see cref="AppSettings"/>
/// y la base URL son valores por-resolución.
/// </summary>
public sealed class OpenAICompatibleClient : ILLMClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly AppSettings _settings;
    private readonly string _baseUrl;
    private readonly ILogger<OpenAICompatibleClient> _logger;

    /// <summary>Crea un nuevo <see cref="OpenAICompatibleClient"/>.</summary>
    /// <param name="http">Cliente HTTP resuelto por la factory (D-9).</param>
    /// <param name="settings">Preferencias del usuario en el momento de la resolución.</param>
    /// <param name="baseUrl">Base URL ya resuelta (D-3), sin barra final obligatoria.</param>
    /// <param name="logger">Logger de diagnóstico. Nunca registra la API key.</param>
    public OpenAICompatibleClient(
        HttpClient http,
        AppSettings settings,
        string baseUrl,
        ILogger<OpenAICompatibleClient> logger)
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
            ChatCompletionRequestDto dto = BuildRequestDto(request);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildUri("chat/completions"))
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json"),
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AiApiKey);

            using HttpResponseMessage response = await _http.SendAsync(httpRequest, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw BuildFailureException(response);
            }

            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            ChatCompletionResponseDto? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<ChatCompletionResponseDto>(body, _jsonOptions);
            }
            catch (JsonException ex)
            {
                throw new LlmException(LlmFailureKind.Malformed, "The provider response could not be parsed.", ex);
            }

            string? content = envelope?.Choices is { Count: > 0 } choices ? choices[0].Message?.Content : null;
            if (content is null)
            {
                throw new LlmException(LlmFailureKind.Malformed, "The provider response had no completion content.");
            }

            stopwatch.Stop();
            _logger.LogDebug(
                "OpenAI-compatible completion ok. CorrelationId={CorrelationId} Provider={Provider} LatencyMs={LatencyMs}",
                correlationId,
                ProviderName,
                stopwatch.ElapsedMilliseconds);

            return new LlmResponse(content, envelope!.Usage?.TotalTokens ?? 0, stopwatch.ElapsedMilliseconds);
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
            var dto = new ChatCompletionRequestDto(
                _settings.AiModel,
                [new ChatMessageDto("user", "ping")],
                0.0,
                1,
                null);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildUri("chat/completions"))
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json"),
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AiApiKey);

            using HttpResponseMessage response = await _http.SendAsync(httpRequest, ct).ConfigureAwait(false);

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
            _logger.LogDebug(ex, "OpenAI-compatible test connection failed. Provider={Provider}", ProviderName);
            return new LlmConnectionOutcome(LlmConnectionStatus.Unavailable, ProviderName);
        }
    }

    private Uri BuildUri(string path) => new($"{_baseUrl.TrimEnd('/')}/{path}");

    private ChatCompletionRequestDto BuildRequestDto(LlmRequest request)
    {
        var messages = request.Messages
            .Select(m => new ChatMessageDto(MapRole(m.Role), m.Content))
            .ToList();

        // response_format is sent only for AiProvider.OpenAI: OpenRouter
        // routes to many free models that 400 on it, and Custom's wire
        // shape is unknown (D-10).
        ResponseFormatDto? responseFormat = _settings.AiProvider == AiProvider.OpenAI
            ? new ResponseFormatDto("json_object")
            : null;

        return new ChatCompletionRequestDto(
            request.Model ?? _settings.AiModel,
            messages,
            request.Temperature,
            request.MaxTokens,
            responseFormat);
    }

    private static string MapRole(LlmRole role) => role switch
    {
        LlmRole.System => "system",
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown LlmRole."),
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

    private sealed record ChatCompletionRequestDto(
        string Model,
        IReadOnlyList<ChatMessageDto> Messages,
        double Temperature,
        int MaxTokens,
        ResponseFormatDto? ResponseFormat);

    private sealed record ChatMessageDto(string Role, string Content);

    private sealed record ResponseFormatDto(string Type);

    private sealed record ChatCompletionResponseDto(
        IReadOnlyList<ChatChoiceDto>? Choices,
        ChatUsageDto? Usage);

    private sealed record ChatChoiceDto(ChatMessageDto? Message);

    private sealed record ChatUsageDto(int TotalTokens);
}
