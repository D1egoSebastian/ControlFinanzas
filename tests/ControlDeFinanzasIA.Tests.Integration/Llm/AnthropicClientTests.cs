using System.Net;
using System.Text;
using System.Text.Json;
using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Infrastructure.Llm;
using ControlDeFinanzasIA.Tests.Integration.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Integration.Llm;

/// <summary>
/// Tests de integración para <see cref="AnthropicClient"/> (F-013, slice 3,
/// D-10). A diferencia de <see cref="OpenAICompatibleClientTests"/>, este
/// archivo existe para probar que la abstracción <see cref="ILLMClient"/>
/// no tiene forma de OpenAI: cada assert cubre explícitamente un punto en
/// el que el wire format de Anthropic difiere — vía
/// <see cref="FakeHttpMessageHandler"/>, sin red real.
/// </summary>
public class AnthropicClientTests
{
    private const string _baseUrl = "https://api.anthropic.test/v1";
    private const string _sentinelApiKey = "sk-ant-test-sentinel";

    private static AppSettings BuildSettings() => new(
        AppTheme.Dark,
        SupportedLocales.Default,
        AiProvider.Anthropic,
        _sentinelApiKey,
        "claude-test-model",
        _baseUrl,
        AppSettings.DefaultAiTemperature,
        AppSettings.DefaultAiMaxTokensPerRequest);

    private static LlmRequest BuildRequest() => new()
    {
        Messages =
        [
            new LlmMessage(LlmRole.System, "You are a classifier."),
            new LlmMessage(LlmRole.User, "pizza 25"),
        ],
        Temperature = 0.0,
        MaxTokens = 128,
    };

    private static string SuccessBody(string content = "{\"category\":\"Comida\"}")
    {
        string escapedContent = content.Replace("\"", "\\\"", StringComparison.Ordinal);
        return "{\"content\":[{\"type\":\"text\",\"text\":\""
            + escapedContent
            + "\"}],\"usage\":{\"input_tokens\":10,\"output_tokens\":32}}";
    }

    [Fact]
    public async Task CompleteAsync_SendsAbsoluteUriWithApiKeyHeader_AndNoAuthorizationHeader()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        using var http = new HttpClient(handler);
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        await client.CompleteAsync(BuildRequest());

        CapturedRequest captured = Assert.Single(handler.Requests);
        Assert.Equal(new Uri($"{_baseUrl}/messages"), captured.Uri);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal(_sentinelApiKey, captured.Headers["x-api-key"]);
        Assert.False(captured.Headers.ContainsKey("Authorization"));
    }

    [Fact]
    public async Task CompleteAsync_MultipleSystemMessages_ThrowsInsteadOfSilentlyDroppingThem()
    {
        // El `system` de la Messages API es un único campo de nivel
        // superior, así que un LlmRequest con dos mensajes System no es
        // representable. Descartar el segundo en silencio perdería datos
        // del llamador sin ninguna señal; falla ruidosamente en su lugar.
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        using var http = new HttpClient(handler);
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        var request = new LlmRequest
        {
            Messages =
            [
                new LlmMessage(LlmRole.System, "You are a classifier."),
                new LlmMessage(LlmRole.System, "Answer only in Spanish."),
                new LlmMessage(LlmRole.User, "pizza 25"),
            ],
            Temperature = 0.0,
            MaxTokens = 128,
        };

        await Assert.ThrowsAsync<ArgumentException>(() => client.CompleteAsync(request));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CompleteAsync_SendsAnthropicVersionHeader()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        using var http = new HttpClient(handler);
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        await client.CompleteAsync(BuildRequest());

        CapturedRequest captured = Assert.Single(handler.Requests);
        Assert.True(captured.Headers.ContainsKey("anthropic-version"));
        Assert.False(string.IsNullOrWhiteSpace(captured.Headers["anthropic-version"]));
    }

    [Fact]
    public async Task CompleteAsync_BodyHasTopLevelSystemField_AndNoSystemRoleMessage()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        using var http = new HttpClient(handler);
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        await client.CompleteAsync(BuildRequest());

        using var document = JsonDocument.Parse(handler.Requests[0].Body);
        JsonElement root = document.RootElement;

        Assert.Equal("You are a classifier.", root.GetProperty("system").GetString());

        JsonElement messages = root.GetProperty("messages");
        Assert.Equal(1, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("pizza 25", messages[0].GetProperty("content").GetString());
        Assert.DoesNotContain(
            messages.EnumerateArray(),
            m => string.Equals(m.GetProperty("role").GetString(), "system", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompleteAsync_BodyHasModelTemperatureAndMaxTokens()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        using var http = new HttpClient(handler);
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        await client.CompleteAsync(BuildRequest());

        using var document = JsonDocument.Parse(handler.Requests[0].Body);
        JsonElement root = document.RootElement;

        Assert.Equal("claude-test-model", root.GetProperty("model").GetString());
        Assert.Equal(0.0, root.GetProperty("temperature").GetDouble());
        Assert.Equal(128, root.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task CompleteAsync_SuccessResponse_ReadsContentArrayTextAndSummedTokens()
    {
        // A tiny artificial delay: an in-process fake handler otherwise
        // responds fast enough that Stopwatch.ElapsedMilliseconds can
        // legitimately read 0, which would make this assertion flaky
        // rather than load-bearing.
        var handler = new FakeHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(5, ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    SuccessBody("{\"category\":\"Comida\"}"), Encoding.UTF8, "application/json"),
            };
        });
        using var http = new HttpClient(handler);
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        LlmResponse response = await client.CompleteAsync(BuildRequest());

        Assert.Equal("{\"category\":\"Comida\"}", response.Content);
        Assert.Equal(42, response.TokensUsed);
        Assert.True(response.LatencyMs > 0);
    }

    [Fact]
    public async Task CompleteAsync_401_ThrowsInvalidCredentials()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, "{\"error\":\"bad key\"}");
        using var http = new HttpClient(handler);
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        LlmException ex = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(BuildRequest()));

        Assert.Equal(LlmFailureKind.InvalidCredentials, ex.Kind);
    }

    [Fact]
    public async Task CompleteAsync_429_ThrowsRateLimited()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.TooManyRequests, "{\"error\":\"slow down\"}");
        using var http = new HttpClient(handler);
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        LlmException ex = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(BuildRequest()));

        Assert.Equal(LlmFailureKind.RateLimited, ex.Kind);
    }

    [Fact]
    public async Task CompleteAsync_500_ThrowsServerError()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}");
        using var http = new HttpClient(handler);
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        LlmException ex = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(BuildRequest()));

        Assert.Equal(LlmFailureKind.ServerError, ex.Kind);
    }

    [Fact]
    public async Task CompleteAsync_DelayedPastClientTimeout_ThrowsTimeout()
    {
        var handler = new FakeHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessBody(), Encoding.UTF8, "application/json"),
            };
        });
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(50) };
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        // CancellationToken.None: the timeout comes from HttpClient.Timeout,
        // not from our own token, which is exactly the "ct is NOT cancelled"
        // branch of the D-10 status-mapping table.
        LlmException ex = await Assert.ThrowsAsync<LlmException>(
            () => client.CompleteAsync(BuildRequest(), CancellationToken.None));

        Assert.Equal(LlmFailureKind.Timeout, ex.Kind);
    }

    [Fact]
    public async Task CompleteAsync_200WithNoContent_ThrowsMalformed()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{\"usage\":{\"input_tokens\":1,\"output_tokens\":1}}");
        using var http = new HttpClient(handler);
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        LlmException ex = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(BuildRequest()));

        Assert.Equal(LlmFailureKind.Malformed, ex.Kind);
    }

    [Fact]
    public async Task TestConnectionAsync_Success_ReturnsOk()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        using var http = new HttpClient(handler);
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        LlmConnectionOutcome outcome = await client.TestConnectionAsync();

        Assert.Equal(LlmConnectionStatus.Ok, outcome.Status);
        Assert.True(outcome.IsOk);
    }

    [Fact]
    public async Task TestConnectionAsync_401_ReturnsInvalidCredentials_AndDoesNotThrow()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, "{\"error\":\"bad key\"}");
        using var http = new HttpClient(handler);
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        LlmConnectionOutcome outcome = await client.TestConnectionAsync();

        Assert.Equal(LlmConnectionStatus.InvalidCredentials, outcome.Status);
    }

    [Fact]
    public async Task TestConnectionAsync_TransportException_ReturnsUnavailable_AndDoesNotThrow()
    {
        var handler = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("no network"));
        using var http = new HttpClient(handler);
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        LlmConnectionOutcome outcome = await client.TestConnectionAsync();

        Assert.Equal(LlmConnectionStatus.Unavailable, outcome.Status);
    }

    [Fact]
    public void ProviderName_ReflectsConfiguredProvider()
    {
        using var http = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody()));
        var client = new AnthropicClient(http, BuildSettings(), _baseUrl, NullLogger<AnthropicClient>.Instance);

        Assert.Equal("Anthropic", client.ProviderName);
    }
}
