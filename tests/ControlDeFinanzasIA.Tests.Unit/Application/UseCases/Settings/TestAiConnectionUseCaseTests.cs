using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Settings.TestAiConnection;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Settings;

/// <summary>
/// Unit tests for <see cref="TestAiConnectionUseCase"/> (F-013, slice 7, R-8,
/// D-11). No mocking library — hand-rolled fakes only. Placed under
/// <c>Application/UseCases/Settings/</c> to match this repo's established
/// sibling location (<see cref="GetAppSettingsQueryTests"/>), the same
/// precedent slices 5b/6 recorded for their own test files.
/// </summary>
public sealed class TestAiConnectionUseCaseTests
{
    private readonly FakeSettingsStore _settingsStore;
    private readonly FakeLlmClientFactory _factory;
    private readonly FakeLlmClient _client;
    private readonly RecordingLogger<TestAiConnectionUseCase> _logger;
    private readonly TestAiConnectionUseCase _useCase;

    public TestAiConnectionUseCaseTests()
    {
        _settingsStore = new FakeSettingsStore
        {
            AppSettings = AppSettings.Defaults with { AiApiKey = "sk-configured", AiProvider = AiProvider.OpenAI },
        };

        _client = new FakeLlmClient { ProviderName = "OpenAI" };

        _factory = new FakeLlmClientFactory
        {
            ResolveDiagnosticResult = LlmClientResolution.Configured(_client),
        };

        _logger = new RecordingLogger<TestAiConnectionUseCase>();

        _useCase = new TestAiConnectionUseCase(_settingsStore, _factory, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderReturnsOk_ReturnsSuccessWithOkKind()
    {
        _client.ConnectionOutcome = new LlmConnectionOutcome(LlmConnectionStatus.Ok, "OpenAI");

        Result result = await _useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        TestAiConnectionDto dto = Assert.IsType<TestAiConnectionDto>(result.Value);
        Assert.Equal(AiConnectionResultKind.Ok, dto.Kind);
        Assert.Equal("OpenAI", dto.ProviderName);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderRejectsCredentials_ReturnsSuccessWithInvalidCredentialsKind()
    {
        _client.ConnectionOutcome = new LlmConnectionOutcome(LlmConnectionStatus.InvalidCredentials, "OpenAI");

        Result result = await _useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        TestAiConnectionDto dto = Assert.IsType<TestAiConnectionDto>(result.Value);
        Assert.Equal(AiConnectionResultKind.InvalidCredentials, dto.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderUnreachable_ReturnsSuccessWithUnavailableKind()
    {
        _client.ConnectionOutcome = new LlmConnectionOutcome(LlmConnectionStatus.Unavailable, "OpenAI");

        Result result = await _useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        TestAiConnectionDto dto = Assert.IsType<TestAiConnectionDto>(result.Value);
        Assert.Equal(AiConnectionResultKind.Unavailable, dto.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_NotConfigured_ReturnsSuccessWithNotConfiguredKindAndZeroClientCalls()
    {
        _factory.ResolveDiagnosticResult = LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingApiKey);

        Result result = await _useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        TestAiConnectionDto dto = Assert.IsType<TestAiConnectionDto>(result.Value);
        Assert.Equal(AiConnectionResultKind.NotConfigured, dto.Kind);
        Assert.Equal(0, _client.TestConnectionCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_Always_CallsResolveDiagnosticNotResolve()
    {
        _client.ConnectionOutcome = new LlmConnectionOutcome(LlmConnectionStatus.Ok, "OpenAI");

        await _useCase.ExecuteAsync();

        Assert.Single(_factory.ResolveDiagnosticCalls);
        Assert.Empty(_factory.ResolveCalls);
    }

    [Fact]
    public async Task ExecuteAsync_Configured_CallsTestConnectionExactlyOnce()
    {
        _client.ConnectionOutcome = new LlmConnectionOutcome(LlmConnectionStatus.Ok, "OpenAI");

        await _useCase.ExecuteAsync();

        Assert.Equal(1, _client.TestConnectionCallCount);
        Assert.Equal(0, _client.CompleteCallCount);
    }
}
