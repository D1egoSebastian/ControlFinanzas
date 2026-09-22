using System.Net;
using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Infrastructure.Llm;
using ControlDeFinanzasIA.Tests.Integration.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Integration.Llm;

/// <summary>
/// Tests de integración que abren el circuit breaker a través del pipeline
/// REAL registrado por <see cref="LlmServiceCollectionExtensions.AddLlmClients"/>
/// y confirman que ambos adaptadores traducen el breaker abierto a
/// <see cref="LlmFailureKind.CircuitOpen"/> en lugar de dejar escapar
/// <c>Polly.CircuitBreaker.BrokenCircuitException</c> (F-013, slice 4, R-4,
/// D-1, D-10, Task 4.9). Sin este mapeo, <see cref="LlmFailureKind.CircuitOpen"/>
/// es un miembro de enum muerto: Polly no fue una referencia de paquete
/// hasta esta slice, así que nada lo pudo mapear antes.
/// </summary>
public sealed class CircuitBreakerMappingTests
{
    private const string _sentinelApiKey = "sk-test-sentinel";

    [Fact]
    public async Task OpenAICompatibleClient_OpenCircuitBreaker_MapsToCircuitOpen()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        ILLMClient client = BuildRealPipelineClient(
            $"llm-{AiProvider.OpenAI}",
            handler,
            (http, settings, baseUrl) => new OpenAICompatibleClient(
                http, settings, baseUrl, NullLogger<OpenAICompatibleClient>.Instance),
            AiProvider.OpenAI);

        LlmException exception = await OpenTheBreakerAsync(client);

        Assert.Equal(LlmFailureKind.CircuitOpen, exception.Kind);
    }

    [Fact]
    public async Task AnthropicClient_OpenCircuitBreaker_MapsToCircuitOpen()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        ILLMClient client = BuildRealPipelineClient(
            $"llm-{AiProvider.Anthropic}",
            handler,
            (http, settings, baseUrl) => new AnthropicClient(
                http, settings, baseUrl, NullLogger<AnthropicClient>.Instance),
            AiProvider.Anthropic);

        LlmException exception = await OpenTheBreakerAsync(client);

        Assert.Equal(LlmFailureKind.CircuitOpen, exception.Kind);
    }

    private static async Task<LlmException> OpenTheBreakerAsync(ILLMClient client)
    {
        // MinimumThroughput=4, FailureRatio=0.5 (D-9): every failing call
        // is retried once (2 attempts), so two calls accumulate the 4
        // failing samples the breaker needs before it can flip open. A
        // bounded loop tolerates the pipeline needing one extra call to
        // observe the transition rather than hard-coding an exact count.
        LlmException? lastException = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            lastException = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(BuildRequest()));
            if (lastException.Kind == LlmFailureKind.CircuitOpen)
            {
                return lastException;
            }
        }

        Assert.Fail(
            $"The circuit breaker never opened after 5 failing calls. Last observed kind: {lastException?.Kind}.");
        return lastException!;
    }

    private static ILLMClient BuildRealPipelineClient(
        string clientName,
        FakeHttpMessageHandler handler,
        Func<HttpClient, AppSettings, string, ILLMClient> buildClient,
        AiProvider provider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLlmClients();
        services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => handler);

        ServiceProvider provider1 = services.BuildServiceProvider();
        IHttpClientFactory httpClientFactory = provider1.GetRequiredService<IHttpClientFactory>();
        HttpClient http = httpClientFactory.CreateClient(clientName);

        var settings = new AppSettings(
            AppTheme.Dark,
            SupportedLocales.Default,
            provider,
            _sentinelApiKey,
            "test-model",
            "https://provider.test/v1",
            AppSettings.DefaultAiTemperature,
            AppSettings.DefaultAiMaxTokensPerRequest);

        return buildClient(http, settings, "https://provider.test/v1");
    }

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
}
