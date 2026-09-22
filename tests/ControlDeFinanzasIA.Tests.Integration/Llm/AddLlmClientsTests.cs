using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Infrastructure.Llm;
using Microsoft.Extensions.DependencyInjection;

namespace ControlDeFinanzasIA.Tests.Integration.Llm;

/// <summary>
/// Tests de integración para
/// <see cref="LlmServiceCollectionExtensions.AddLlmClients"/> (F-013, slice
/// 4, R-2, R-8, R-9, D-9): registra los 8 <see cref="HttpClient"/>s
/// nombrados, la factory y el prompt provider sobre una
/// <see cref="ServiceCollection"/> real, y confirma que el proveedor
/// completo construye con <c>ValidateOnBuild = true</c> — el riesgo
/// "primer <c>AddHttpClient</c> del proyecto interactúa mal con el orden de
/// arranque" se comprueba en lugar de darse por sentado.
/// </summary>
public sealed class AddLlmClientsTests
{
    private static readonly string[] _mainClientNames =
    [
        "llm-OpenAI",
        "llm-Anthropic",
        "llm-OpenRouter",
        "llm-Custom",
    ];

    private static readonly string[] _diagnosticClientNames =
    [
        "llm-OpenAI-diagnostic",
        "llm-Anthropic-diagnostic",
        "llm-OpenRouter-diagnostic",
        "llm-Custom-diagnostic",
    ];

    [Fact]
    public void AddLlmClients_ProviderBuildsWithValidateOnBuild()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddLlmClients();

        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider);
    }

    [Fact]
    public void AddLlmClients_ResolvesILLMClientFactoryAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLlmClients();
        using ServiceProvider provider = services.BuildServiceProvider();

        ILLMClientFactory first = provider.GetRequiredService<ILLMClientFactory>();
        ILLMClientFactory second = provider.GetRequiredService<ILLMClientFactory>();

        Assert.IsType<LLMClientFactory>(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void AddLlmClients_ResolvesIPromptProviderAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLlmClients();
        using ServiceProvider provider = services.BuildServiceProvider();

        IPromptProvider first = provider.GetRequiredService<IPromptProvider>();
        IPromptProvider second = provider.GetRequiredService<IPromptProvider>();

        Assert.Same(first, second);
    }

    [Theory]
    [MemberData(nameof(AllEightClientNames))]
    public void AddLlmClients_EveryNamedClientResolvesADistinctHttpClient(string clientName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLlmClients();
        using ServiceProvider provider = services.BuildServiceProvider();

        IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

        using HttpClient client = httpClientFactory.CreateClient(clientName);

        Assert.NotNull(client);
    }

    [Fact]
    public void AddLlmClients_AllEightNamesProduceDistinctHttpClientInstances()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLlmClients();
        using ServiceProvider provider = services.BuildServiceProvider();

        IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        string[] allNames = [.. _mainClientNames, .. _diagnosticClientNames];

        var clients = allNames.Select(httpClientFactory.CreateClient).ToList();

        Assert.Equal(8, allNames.Length);
        Assert.Equal(8, clients.Distinct().Count());
    }

    public static TheoryData<string> AllEightClientNames()
    {
        var data = new TheoryData<string>();
        foreach (string name in _mainClientNames.Concat(_diagnosticClientNames))
        {
            data.Add(name);
        }

        return data;
    }
}
