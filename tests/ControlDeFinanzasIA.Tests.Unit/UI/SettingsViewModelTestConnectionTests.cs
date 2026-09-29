using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotificationSettings;
using ControlDeFinanzasIA.Application.UseCases.Notifications.UpdateNotificationSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.GetAppSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.TestAiConnection;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateAiSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateLocale;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateTheme;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for <see cref="SettingsViewModel.TestConnectionCommand"/>
/// (F-013, slice 7, R-8, D-11, S-4). Placed alongside the existing
/// <see cref="SettingsViewModelTests"/> file, per this repo's established
/// one-file-per-feature-slice convention for the same view-model (slice
/// 4a/4b).
/// </summary>
public sealed class SettingsViewModelTestConnectionTests
{
    [Fact]
    public async Task TestConnectionAsync_Ok_SetsLocalizedOkMessageWithProviderName()
    {
        var client = new FakeLlmClient { ConnectionOutcome = new LlmConnectionOutcome(LlmConnectionStatus.Ok, "OpenAI") };
        var factory = new FakeLlmClientFactory { ResolveDiagnosticResult = LlmClientResolution.Configured(client) };
        SettingsViewModel viewModel = CreateViewModel(factory: factory);

        await viewModel.TestConnectionCommand.ExecuteAsync(null);

        string expected = string.Format(System.Globalization.CultureInfo.InvariantCulture, Strings.SettingsAiTestConnectionOk, "OpenAI");
        Assert.Equal(expected, viewModel.AiTestConnectionMessage);
    }

    [Fact]
    public async Task TestConnectionAsync_NotConfigured_SetsLocalizedNotConfiguredMessage()
    {
        var factory = new FakeLlmClientFactory
        {
            ResolveDiagnosticResult = LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingApiKey),
        };
        SettingsViewModel viewModel = CreateViewModel(factory: factory);

        await viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal(Strings.SettingsAiTestConnectionNotConfigured, viewModel.AiTestConnectionMessage);
    }

    [Fact]
    public async Task TestConnectionAsync_InvalidCredentials_SetsLocalizedInvalidCredentialsMessage()
    {
        var client = new FakeLlmClient { ConnectionOutcome = new LlmConnectionOutcome(LlmConnectionStatus.InvalidCredentials, "OpenAI") };
        var factory = new FakeLlmClientFactory { ResolveDiagnosticResult = LlmClientResolution.Configured(client) };
        SettingsViewModel viewModel = CreateViewModel(factory: factory);

        await viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal(Strings.SettingsAiTestConnectionInvalidCredentials, viewModel.AiTestConnectionMessage);
    }

    [Fact]
    public async Task TestConnectionAsync_Unavailable_SetsLocalizedUnavailableMessage()
    {
        var client = new FakeLlmClient { ConnectionOutcome = new LlmConnectionOutcome(LlmConnectionStatus.Unavailable, "OpenAI") };
        var factory = new FakeLlmClientFactory { ResolveDiagnosticResult = LlmClientResolution.Configured(client) };
        SettingsViewModel viewModel = CreateViewModel(factory: factory);

        await viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal(Strings.SettingsAiTestConnectionUnavailable, viewModel.AiTestConnectionMessage);
    }

    [Fact]
    public async Task TestConnectionAsync_UnexpectedUseCaseFailure_SetsUnavailableMessageAndNeverThrows()
    {
        var factory = new FakeLlmClientFactory
        {
            ResolveDiagnosticResult = LlmClientResolution.Configured(new FakeLlmClient()),
            ThrowOnResolveDiagnostic = new InvalidOperationException("boom"),
        };
        SettingsViewModel viewModel = CreateViewModel(factory: factory);

        await viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal(Strings.SettingsAiTestConnectionUnavailable, viewModel.AiTestConnectionMessage);
        Assert.False(viewModel.IsTestingConnection);
        Assert.Equal(string.Empty, viewModel.FormError);
    }

    [Fact]
    public async Task TestConnectionAsync_WhileInFlight_IsTestingConnectionIsTrueThenFalseAfter()
    {
        var client = new FakeLlmClient
        {
            ConnectionOutcome = new LlmConnectionOutcome(LlmConnectionStatus.Ok, "OpenAI"),
            TestConnectionGate = new TaskCompletionSource<bool>(),
        };
        var factory = new FakeLlmClientFactory { ResolveDiagnosticResult = LlmClientResolution.Configured(client) };
        SettingsViewModel viewModel = CreateViewModel(factory: factory);

        Assert.False(viewModel.IsTestingConnection);

        Task inFlight = viewModel.TestConnectionCommand.ExecuteAsync(null);
        await Task.Delay(20); // let the command reach the await before asserting

        Assert.True(viewModel.IsTestingConnection);

        client.TestConnectionGate.SetResult(true);
        await inFlight;

        Assert.False(viewModel.IsTestingConnection);
    }

    [Fact]
    public async Task TestConnectionAsync_Always_NeverWritesFormError()
    {
        var client = new FakeLlmClient { ConnectionOutcome = new LlmConnectionOutcome(LlmConnectionStatus.Ok, "OpenAI") };
        var factory = new FakeLlmClientFactory { ResolveDiagnosticResult = LlmClientResolution.Configured(client) };
        SettingsViewModel viewModel = CreateViewModel(factory: factory);
        viewModel.FormError = "stale error from an unrelated save";

        await viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal("stale error from an unrelated save", viewModel.FormError);
    }

    private static SettingsViewModel CreateViewModel(FakeLlmClientFactory factory, FakeSettingsStore? store = null)
    {
        store ??= new FakeSettingsStore
        {
            AppSettings = AppSettings.Defaults with { AiApiKey = "sk-configured", AiProvider = AiProvider.OpenAI },
        };

        var getAppSettingsQuery = new GetAppSettingsQuery(store);
        var updateThemeUseCase = new UpdateThemeUseCase(store, NullLogger<UpdateThemeUseCase>.Instance);
        var updateLocaleUseCase = new UpdateLocaleUseCase(store, NullLogger<UpdateLocaleUseCase>.Instance);
        var updateAiSettingsUseCase = new UpdateAiSettingsUseCase(
            store, new UpdateAiSettingsValidator(), NullLogger<UpdateAiSettingsUseCase>.Instance);
        var testAiConnectionUseCase = new TestAiConnectionUseCase(store, factory, NullLogger<TestAiConnectionUseCase>.Instance);

        return new SettingsViewModel(
            getAppSettingsQuery,
            updateThemeUseCase,
            updateLocaleUseCase,
            updateAiSettingsUseCase,
            testAiConnectionUseCase,
            new GetNotificationSettingsQuery(store),
            new UpdateNotificationSettingsUseCase(store),
            new FakeNotificationCenter(),
            NullLogger<SettingsViewModel>.Instance);
    }
}
