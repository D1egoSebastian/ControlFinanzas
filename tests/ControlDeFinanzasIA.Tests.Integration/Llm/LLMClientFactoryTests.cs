using System.Net;
using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Infrastructure.Llm;
using ControlDeFinanzasIA.Tests.Integration.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Integration.Llm;

/// <summary>
/// Tests de integración para <see cref="LLMClientFactory"/> (F-013, slice 4,
/// R-2, D-3): tabla `(AppSettings) → (adapter type, base URL, status)` para
/// los 4 proveedores, las ramas de base URL y los dos motivos de
/// no-configurado. Sin DI: <see cref="RecordingHttpClientFactory"/> es un
/// doble hecho a mano (convención del repo — sin mocking library).
/// </summary>
public sealed class LLMClientFactoryTests
{
    private const string _sentinelApiKey = "sk-test-sentinel";

    private static AppSettings BuildSettings(AiProvider provider, string apiKey, string? baseUrl) => new(
        AppTheme.Dark,
        SupportedLocales.Default,
        provider,
        apiKey,
        "test-model",
        baseUrl,
        AppSettings.DefaultAiTemperature,
        AppSettings.DefaultAiMaxTokensPerRequest);

    [Theory]
    [InlineData(AiProvider.OpenAI, typeof(OpenAICompatibleClient), "https://api.openai.com/v1")]
    [InlineData(AiProvider.OpenRouter, typeof(OpenAICompatibleClient), "https://openrouter.ai/api/v1")]
    [InlineData(AiProvider.Anthropic, typeof(AnthropicClient), "https://api.anthropic.com/v1")]
    public async Task Resolve_WithNoConfiguredBaseUrl_UsesProviderDefault(
        AiProvider provider, Type expectedClientType, string expectedBaseUrl)
    {
        var httpClientFactory = new RecordingHttpClientFactory();
        var factory = new LLMClientFactory(httpClientFactory, NullLoggerFactory.Instance);

        LlmClientResolution resolution = factory.Resolve(BuildSettings(provider, _sentinelApiKey, baseUrl: null));

        Assert.Equal(LlmConfigurationStatus.Configured, resolution.Status);
        Assert.NotNull(resolution.Client);
        Assert.IsType(expectedClientType, resolution.Client);
        Uri requestUri = await GetRequestedUriAsync(resolution.Client!, httpClientFactory);
        Assert.StartsWith(expectedBaseUrl + "/", requestUri.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolve_CustomWithConfiguredBaseUrl_UsesConfiguredValue()
    {
        var httpClientFactory = new RecordingHttpClientFactory();
        var factory = new LLMClientFactory(httpClientFactory, NullLoggerFactory.Instance);

        LlmClientResolution resolution = factory.Resolve(
            BuildSettings(AiProvider.Custom, _sentinelApiKey, "https://custom.example/v1"));

        Assert.Equal(LlmConfigurationStatus.Configured, resolution.Status);
        Assert.IsType<OpenAICompatibleClient>(resolution.Client);
        Uri requestUri = await GetRequestedUriAsync(resolution.Client!, httpClientFactory);
        Assert.StartsWith("https://custom.example/v1/", requestUri.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AiProvider.OpenAI)]
    [InlineData(AiProvider.OpenRouter)]
    [InlineData(AiProvider.Anthropic)]
    [InlineData(AiProvider.Custom)]
    public void Resolve_EmptyApiKey_ReturnsMissingApiKeyForEveryProvider(AiProvider provider)
    {
        var httpClientFactory = new RecordingHttpClientFactory();
        var factory = new LLMClientFactory(httpClientFactory, NullLoggerFactory.Instance);

        LlmClientResolution resolution = factory.Resolve(
            BuildSettings(provider, apiKey: string.Empty, baseUrl: "https://custom.example/v1"));

        Assert.Equal(LlmConfigurationStatus.MissingApiKey, resolution.Status);
        Assert.Null(resolution.Client);
        Assert.Empty(httpClientFactory.RequestedNames);
    }

    [Fact]
    public void Resolve_CustomWithEmptyBaseUrl_ReturnsMissingBaseUrl()
    {
        var httpClientFactory = new RecordingHttpClientFactory();
        var factory = new LLMClientFactory(httpClientFactory, NullLoggerFactory.Instance);

        LlmClientResolution resolution = factory.Resolve(
            BuildSettings(AiProvider.Custom, _sentinelApiKey, baseUrl: null));

        Assert.Equal(LlmConfigurationStatus.MissingBaseUrl, resolution.Status);
        Assert.Null(resolution.Client);
        Assert.Empty(httpClientFactory.RequestedNames);
    }

    [Fact]
    public void Resolve_CustomWithEmptyApiKeyAndEmptyBaseUrl_ReportsMissingApiKeyFirst()
    {
        var httpClientFactory = new RecordingHttpClientFactory();
        var factory = new LLMClientFactory(httpClientFactory, NullLoggerFactory.Instance);

        LlmClientResolution resolution = factory.Resolve(
            BuildSettings(AiProvider.Custom, apiKey: string.Empty, baseUrl: null));

        Assert.Equal(LlmConfigurationStatus.MissingApiKey, resolution.Status);
    }

    [Fact]
    public void Resolve_BindsTheMainNamedClient()
    {
        var httpClientFactory = new RecordingHttpClientFactory();
        var factory = new LLMClientFactory(httpClientFactory, NullLoggerFactory.Instance);

        factory.Resolve(BuildSettings(AiProvider.OpenAI, _sentinelApiKey, baseUrl: null));

        Assert.Equal(["llm-OpenAI"], httpClientFactory.RequestedNames);
    }

    [Fact]
    public void ResolveDiagnostic_BindsTheDiagnosticNamedClient()
    {
        var httpClientFactory = new RecordingHttpClientFactory();
        var factory = new LLMClientFactory(httpClientFactory, NullLoggerFactory.Instance);

        factory.ResolveDiagnostic(BuildSettings(AiProvider.Anthropic, _sentinelApiKey, baseUrl: null));

        Assert.Equal(["llm-Anthropic-diagnostic"], httpClientFactory.RequestedNames);
    }

    private static async Task<Uri> GetRequestedUriAsync(
        ILLMClient client, RecordingHttpClientFactory httpClientFactory)
    {
        // TestConnectionAsync never throws (D-2) and always sends a request
        // through the resolved base URL — the simplest way to observe which
        // base URL the factory actually wired into the client, since the
        // adapters do not expose it as a public property.
        await client.TestConnectionAsync();
        CapturedRequest request = Assert.Single(httpClientFactory.LastHandler!.Requests);
        return request.Uri!;
    }

    /// <summary>
    /// Hand-rolled <see cref="IHttpClientFactory"/> double: records every
    /// requested client name and hands back an <see cref="HttpClient"/>
    /// backed by a fresh <see cref="FakeHttpMessageHandler"/> so the
    /// resolved base URL can be observed from the captured request.
    /// </summary>
    private sealed class RecordingHttpClientFactory : IHttpClientFactory
    {
        public List<string> RequestedNames { get; } = new();

        public FakeHttpMessageHandler? LastHandler { get; private set; }

        public HttpClient CreateClient(string name)
        {
            RequestedNames.Add(name);
            LastHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}");
            return new HttpClient(LastHandler);
        }
    }
}
