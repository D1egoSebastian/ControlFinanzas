using System.Net;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Infrastructure.Llm;
using ControlDeFinanzasIA.Tests.Integration.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace ControlDeFinanzasIA.Tests.Integration.Llm;

/// <summary>
/// Tests de integración que verifican intentos <em>contados</em> contra el
/// pipeline REAL registrado por
/// <see cref="LlmServiceCollectionExtensions.AddLlmClients"/> (F-013,
/// slice 4, R-9, S-12, D-9) — no los defaults documentados del framework.
/// Task 4.8: (a) un 401 y un 403 producen exactamente 1 intento cada uno;
/// (b) un 500 reintentable produce exactamente 2 intentos — la aserción
/// (b) es lo que evita que (a) pase de forma vacía: sin ella, un pipeline
/// donde el retry nunca se configuró también reportaría 1 intento para un
/// 401. El cliente de diagnóstico produce exactamente 1 intento para un
/// 500, probando que es genuinamente sin retry.
/// </summary>
public sealed class ResiliencePipelineTests
{
    private const AiProvider _provider = AiProvider.OpenAI;

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task MainClient_401Or403_ProducesExactlyOneAttempt(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler(status, "{}");
        using HttpClient client = BuildMainClient(handler);

        using HttpResponseMessage response = await client.GetAsync("https://example.test/ping");

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task MainClient_RetryableServerError_ProducesExactlyTwoAttempts()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        using HttpClient client = BuildMainClient(handler);

        using HttpResponseMessage response = await client.GetAsync("https://example.test/ping");

        // This is the assertion that stops the 401/403 test above from
        // passing vacuously: without proof retry is configured at all
        // (2 attempts here), a pipeline where retry was never wired would
        // also report 1 attempt for a 401 (Task 4.8).
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DiagnosticClient_RetryableServerError_ProducesExactlyOneAttempt()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        using HttpClient client = BuildDiagnosticClient(handler);

        using HttpResponseMessage response = await client.GetAsync("https://example.test/ping");

        // Proves the diagnostic pipeline is genuinely retry-free (R-8),
        // distinct from the main client's 2-attempt behaviour above.
        Assert.Single(handler.Requests);
    }

    private static HttpClient BuildMainClient(FakeHttpMessageHandler handler) =>
        BuildClient($"llm-{_provider}", handler);

    private static HttpClient BuildDiagnosticClient(FakeHttpMessageHandler handler) =>
        BuildClient($"llm-{_provider}-diagnostic", handler);

    private static HttpClient BuildClient(string clientName, FakeHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLlmClients();

        // Swaps only the primary transport handler for the named client,
        // keeping the resilience pipeline AddLlmClients already wired on
        // top of it (D-9) — the real registered pipeline, not a
        // freestanding one built by the test.
        services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => handler);

        ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        return httpClientFactory.CreateClient(clientName);
    }
}
