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
/// Tests de integración para <see cref="OpenAICompatibleClient"/> (F-013,
/// slice 2, D-10). Cubre OpenAI / OpenRouter / Custom vía
/// <see cref="FakeHttpMessageHandler"/> — sin red real.
/// </summary>
public class OpenAICompatibleClientTests
{
    private const string _baseUrl = "https://api.openai.test/v1";
    private const string _sentinelApiKey = "sk-test-sentinel";

    private static AppSettings BuildSettings(AiProvider provider) => new(
        AppTheme.Dark,
        SupportedLocales.Default,
        provider,
        _sentinelApiKey,
        "gpt-test-model",
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
        return "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\""
            + escapedContent
            + "\"}}],\"usage\":{\"total_tokens\":42}}";
    }

    [Fact]
    public async Task CompleteAsync_SendsAbsoluteUriAndBearerAuthorization()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        using var http = new HttpClient(handler);
        var client = new OpenAICompatibleClient(
            http, BuildSettings(AiProvider.OpenAI), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

        await client.CompleteAsync(BuildRequest());

        CapturedRequest captured = Assert.Single(handler.Requests);
        Assert.Equal(new Uri($"{_baseUrl}/chat/completions"), captured.Uri);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal($"Bearer {_sentinelApiKey}", captured.Headers["Authorization"]);
    }

    [Fact]
    public async Task CompleteAsync_BodyHasModelMessagesTemperatureAndMaxTokens()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        using var http = new HttpClient(handler);
        var client = new OpenAICompatibleClient(
            http, BuildSettings(AiProvider.OpenAI), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

        await client.CompleteAsync(BuildRequest());

        using var document = JsonDocument.Parse(handler.Requests[0].Body);
        JsonElement root = document.RootElement;

        Assert.Equal("gpt-test-model", root.GetProperty("model").GetString());
        Assert.Equal(0.0, root.GetProperty("temperature").GetDouble());
        Assert.Equal(128, root.GetProperty("max_tokens").GetInt32());

        JsonElement messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("You are a classifier.", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("pizza 25", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task CompleteAsync_OpenAiProvider_SendsResponseFormat()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        using var http = new HttpClient(handler);
        var client = new OpenAICompatibleClient(
            http, BuildSettings(AiProvider.OpenAI), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

        await client.CompleteAsync(BuildRequest());

        using var document = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.True(document.RootElement.TryGetProperty("response_format", out JsonElement format));
        Assert.Equal("json_object", format.GetProperty("type").GetString());
    }

    [Theory]
    [InlineData(AiProvider.OpenRouter)]
    [InlineData(AiProvider.Custom)]
    public async Task CompleteAsync_NonOpenAiProvider_OmitsResponseFormat(AiProvider provider)
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        using var http = new HttpClient(handler);
        var client = new OpenAICompatibleClient(
            http, BuildSettings(provider), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

        await client.CompleteAsync(BuildRequest());

        using var document = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.False(document.RootElement.TryGetProperty("response_format", out _));
    }

    [Fact]
    public async Task CompleteAsync_SuccessResponse_ReadsContentTokensAndPositiveLatency()
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
        var client = new OpenAICompatibleClient(
            http, BuildSettings(AiProvider.OpenAI), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

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
        var client = new OpenAICompatibleClient(
            http, BuildSettings(AiProvider.OpenAI), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

        LlmException ex = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(BuildRequest()));

        Assert.Equal(LlmFailureKind.InvalidCredentials, ex.Kind);
    }

    [Fact]
    public async Task CompleteAsync_429_ThrowsRateLimited()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.TooManyRequests, "{\"error\":\"slow down\"}");
        using var http = new HttpClient(handler);
        var client = new OpenAICompatibleClient(
            http, BuildSettings(AiProvider.OpenAI), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

        LlmException ex = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(BuildRequest()));

        Assert.Equal(LlmFailureKind.RateLimited, ex.Kind);
    }

    [Fact]
    public async Task CompleteAsync_500_ThrowsServerError()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}");
        using var http = new HttpClient(handler);
        var client = new OpenAICompatibleClient(
            http, BuildSettings(AiProvider.OpenAI), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

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
        var client = new OpenAICompatibleClient(
            http, BuildSettings(AiProvider.OpenAI), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

        // CancellationToken.None: the timeout comes from HttpClient.Timeout,
        // not from our own token, which is exactly the "ct is NOT cancelled"
        // branch of the D-10 status-mapping table.
        LlmException ex = await Assert.ThrowsAsync<LlmException>(
            () => client.CompleteAsync(BuildRequest(), CancellationToken.None));

        Assert.Equal(LlmFailureKind.Timeout, ex.Kind);
    }

    [Fact]
    public async Task CompleteAsync_200WithNoChoices_ThrowsMalformed()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{\"usage\":{\"total_tokens\":1}}");
        using var http = new HttpClient(handler);
        var client = new OpenAICompatibleClient(
            http, BuildSettings(AiProvider.OpenAI), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

        LlmException ex = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(BuildRequest()));

        Assert.Equal(LlmFailureKind.Malformed, ex.Kind);
    }

    [Fact]
    public async Task TestConnectionAsync_Success_ReturnsOk()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody());
        using var http = new HttpClient(handler);
        var client = new OpenAICompatibleClient(
            http, BuildSettings(AiProvider.OpenAI), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

        LlmConnectionOutcome outcome = await client.TestConnectionAsync();

        Assert.Equal(LlmConnectionStatus.Ok, outcome.Status);
        Assert.True(outcome.IsOk);
    }

    [Fact]
    public async Task TestConnectionAsync_401_ReturnsInvalidCredentials_AndDoesNotThrow()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, "{\"error\":\"bad key\"}");
        using var http = new HttpClient(handler);
        var client = new OpenAICompatibleClient(
            http, BuildSettings(AiProvider.OpenAI), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

        LlmConnectionOutcome outcome = await client.TestConnectionAsync();

        Assert.Equal(LlmConnectionStatus.InvalidCredentials, outcome.Status);
    }

    [Fact]
    public async Task TestConnectionAsync_TransportException_ReturnsUnavailable_AndDoesNotThrow()
    {
        var handler = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("no network"));
        using var http = new HttpClient(handler);
        var client = new OpenAICompatibleClient(
            http, BuildSettings(AiProvider.OpenAI), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

        LlmConnectionOutcome outcome = await client.TestConnectionAsync();

        Assert.Equal(LlmConnectionStatus.Unavailable, outcome.Status);
    }

    [Fact]
    public void ProviderName_ReflectsConfiguredProvider()
    {
        using var http = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, SuccessBody()));
        var client = new OpenAICompatibleClient(
            http, BuildSettings(AiProvider.OpenRouter), _baseUrl, NullLogger<OpenAICompatibleClient>.Instance);

        Assert.Equal("OpenRouter", client.ProviderName);
    }
}
