using System.Net.Http;
using System.Reflection;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateAiSettings;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Settings;

/// <summary>
/// Tests for <see cref="UpdateAiSettingsUseCase"/> and
/// <see cref="UpdateAiSettingsValidator"/> (F-012, R-5, S-6, S-7, D-9).
/// </summary>
public sealed class UpdateAiSettingsUseCaseTests
{
    private static UpdateAiSettingsUseCase CreateUseCase(FakeSettingsStore store) =>
        new(store, new UpdateAiSettingsValidator(), NullLogger<UpdateAiSettingsUseCase>.Instance);

    private static UpdateAiSettingsCommand ValidCommand() =>
        new(AiProvider.OpenAI, "sk-test", "gpt-4", null, 0.7, 2000);

    [Fact]
    public async Task ExecuteAsync_ValidCommand_WritesAllSixKeysInOrder()
    {
        var store = new FakeSettingsStore();
        UpdateAiSettingsUseCase useCase = CreateUseCase(store);

        Result result = await useCase.ExecuteAsync(ValidCommand());

        Assert.True(result.IsSuccess);
        Assert.Equal(6, store.SetCalls.Count);
        Assert.Equal(
            new[]
            {
                SettingKeys.AiProvider,
                SettingKeys.AiApiKey,
                SettingKeys.AiModel,
                SettingKeys.AiBaseUrl,
                SettingKeys.AiTemperature,
                SettingKeys.AiMaxTokensPerRequest,
            },
            store.SetCalls.Select(c => c.Key));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidProvider_ReturnsValidationFailureWithoutWriting()
    {
        var store = new FakeSettingsStore();
        UpdateAiSettingsUseCase useCase = CreateUseCase(store);
        UpdateAiSettingsCommand command = ValidCommand() with { Provider = (AiProvider)99 };

        Result result = await useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(store.SetCalls);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.01)]
    public async Task ExecuteAsync_OutOfRangeTemperature_ReturnsValidationFailureWithoutWriting(double temperature)
    {
        var store = new FakeSettingsStore();
        UpdateAiSettingsUseCase useCase = CreateUseCase(store);
        UpdateAiSettingsCommand command = ValidCommand() with { Temperature = temperature };

        Result result = await useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(store.SetCalls);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(2.0)]
    public async Task ExecuteAsync_BoundaryTemperature_IsAccepted(double temperature)
    {
        var store = new FakeSettingsStore();
        UpdateAiSettingsUseCase useCase = CreateUseCase(store);
        UpdateAiSettingsCommand command = ValidCommand() with { Temperature = temperature };

        Result result = await useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(128001)]
    public async Task ExecuteAsync_OutOfRangeMaxTokens_ReturnsValidationFailureWithoutWriting(int maxTokens)
    {
        var store = new FakeSettingsStore();
        UpdateAiSettingsUseCase useCase = CreateUseCase(store);
        UpdateAiSettingsCommand command = ValidCommand() with { MaxTokensPerRequest = maxTokens };

        Result result = await useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(store.SetCalls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(128000)]
    public async Task ExecuteAsync_BoundaryMaxTokens_IsAccepted(int maxTokens)
    {
        var store = new FakeSettingsStore();
        UpdateAiSettingsUseCase useCase = CreateUseCase(store);
        UpdateAiSettingsCommand command = ValidCommand() with { MaxTokensPerRequest = maxTokens };

        Result result = await useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_NonUriBaseUrl_ReturnsValidationFailureWithoutWriting()
    {
        var store = new FakeSettingsStore();
        UpdateAiSettingsUseCase useCase = CreateUseCase(store);
        UpdateAiSettingsCommand command = ValidCommand() with { BaseUrl = "not a url" };

        Result result = await useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(store.SetCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_EmptyOrWhitespaceBaseUrl_IsAcceptedAndNormalizedToNull(string? baseUrl)
    {
        var store = new FakeSettingsStore();
        UpdateAiSettingsUseCase useCase = CreateUseCase(store);
        UpdateAiSettingsCommand command = ValidCommand() with { BaseUrl = baseUrl };

        Result result = await useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        (string Key, string Value) baseUrlCall = store.SetCalls.Single(c => c.Key == SettingKeys.AiBaseUrl);
        Assert.Equal(string.Empty, baseUrlCall.Value);
    }

    [Fact]
    public async Task ExecuteAsync_ValidAbsoluteHttpsBaseUrl_IsAccepted()
    {
        var store = new FakeSettingsStore();
        UpdateAiSettingsUseCase useCase = CreateUseCase(store);
        UpdateAiSettingsCommand command = ValidCommand() with { BaseUrl = "https://api.example.com/v1" };

        Result result = await useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        (string Key, string Value) baseUrlCall = store.SetCalls.Single(c => c.Key == SettingKeys.AiBaseUrl);
        Assert.Equal("https://api.example.com/v1", baseUrlCall.Value);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyApiKey_IsAccepted()
    {
        // Clearing the key is a legitimate action — no NotEmpty rule (D-9).
        var store = new FakeSettingsStore();
        UpdateAiSettingsUseCase useCase = CreateUseCase(store);
        UpdateAiSettingsCommand command = ValidCommand() with { ApiKey = string.Empty };

        Result result = await useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_RepositoryFailureMidSequence_ReturnsFirstFailureImmediately()
    {
        var store = new FakeSettingsStore { FailOnKey = SettingKeys.AiModel };
        UpdateAiSettingsUseCase useCase = CreateUseCase(store);

        Result result = await useCase.ExecuteAsync(ValidCommand());

        Assert.False(result.IsSuccess);
        Assert.Equal("RepositoryError", result.Error!.Code);
        // Provider + ApiKey + Model attempted (Model failed); BaseUrl/Temperature/MaxTokens never reached.
        Assert.Equal(3, store.SetCalls.Count);
    }

    [Fact]
    public async Task ExecuteAsync_NullCommand_Throws()
    {
        var store = new FakeSettingsStore();
        UpdateAiSettingsUseCase useCase = CreateUseCase(store);

        await Assert.ThrowsAsync<ArgumentNullException>(() => useCase.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_HasNoHttpClientOrLlmClientDependency()
    {
        // Reflection assertion (R-5, S-6): the ctor signature IS the "zero AI
        // network calls" guarantee for this use case. No HttpClient,
        // IHttpClientFactory, or ILLMClient-anything is reachable from here.
        ConstructorInfo ctor = typeof(UpdateAiSettingsUseCase).GetConstructors().Single();
        ParameterInfo[] parameters = ctor.GetParameters();

        Assert.DoesNotContain(parameters, p => p.ParameterType.Namespace == typeof(HttpClient).Namespace);
        Assert.DoesNotContain(parameters, p => p.ParameterType.Name.Contains("LLMClient", StringComparison.Ordinal));
        Assert.DoesNotContain(parameters, p => p.ParameterType.Name.Contains("HttpClientFactory", StringComparison.Ordinal));

        bool anyLlmClientTypeExists = typeof(UpdateAiSettingsUseCase).Assembly.GetTypes()
            .Any(t => t.Name.Contains("LLMClient", StringComparison.Ordinal));
        Assert.False(anyLlmClientTypeExists);
    }
}
