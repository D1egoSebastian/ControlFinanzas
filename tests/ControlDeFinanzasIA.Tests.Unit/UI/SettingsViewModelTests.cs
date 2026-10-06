using System.Reflection;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Backup.CreateBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.InspectBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.RestoreBackup;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotificationSettings;
using ControlDeFinanzasIA.Application.UseCases.Notifications.UpdateNotificationSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.GetAppSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.TestAiConnection;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateAiSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateLocale;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateTheme;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Services;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for <see cref="SettingsViewModel"/> (F-012, slice 4a): the
/// Apariencia and Idioma sections. Covers initial load hydration, theme
/// save success/failure, locale save success (with
/// <see cref="SettingsViewModel.IsRestartRequired"/> becoming true) and
/// failure (unsupported locale), and confirms the view-model never
/// touches an Avalonia type (D-8).
/// </summary>
public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task Constructor_LoadsCurrentSettings_PopulatesPickersAndFields()
    {
        var store = new FakeSettingsStore
        {
            AppSettings = new AppSettings(
                AppTheme.Light,
                "en",
                AiProvider.Anthropic,
                "sk-test",
                "claude",
                "https://example.test",
                0.5,
                4000),
        };

        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(1); // allow the constructor's fire-and-forget LoadAsync to complete

        Assert.Equal(AppTheme.Light, viewModel.Theme);
        Assert.Equal("en", viewModel.Locale);
        Assert.Equal(AiProvider.Anthropic, viewModel.AiProvider);
        Assert.Equal("sk-test", viewModel.AiApiKey);
        Assert.Equal("claude", viewModel.AiModel);
        Assert.Equal("https://example.test", viewModel.AiBaseUrl);
        Assert.Equal(0.5, viewModel.AiTemperature);
        Assert.Equal(4000, viewModel.AiMaxTokensPerRequest);

        Assert.Contains(AppTheme.Dark, viewModel.Themes);
        Assert.Contains(AppTheme.Light, viewModel.Themes);
        Assert.Contains(SupportedLocales.SpanishDominican, viewModel.Locales);
        Assert.Contains(SupportedLocales.English, viewModel.Locales);
        Assert.False(viewModel.IsRestartRequired);
        Assert.False(viewModel.IsLoading);

        // Picker display projections resolve through Strings, not the raw
        // enum/locale-code text (R-9, D-12) — every option carries a
        // non-empty, distinct label.
        Assert.Equal(viewModel.Themes.Count, viewModel.ThemeOptions.Count);
        Assert.All(viewModel.ThemeOptions, option => Assert.NotEqual(string.Empty, option.Label));
        Assert.Equal(viewModel.Locales.Count, viewModel.LocaleOptions.Count);
        Assert.All(viewModel.LocaleOptions, option => Assert.NotEqual(string.Empty, option.Label));
    }

    [Fact]
    public async Task SaveThemeAsync_Success_SetsStatusMessageAndDoesNotTouchRestartFlag()
    {
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(1);
        viewModel.Theme = AppTheme.Light;

        await viewModel.SaveThemeCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.FormError);
        Assert.NotEqual(string.Empty, viewModel.StatusMessage);
        Assert.False(viewModel.IsRestartRequired);
        Assert.Contains(store.SetCalls, c => c.Key == SettingKeys.Theme && c.Value == "Light");
    }

    [Fact]
    public async Task SaveThemeAsync_EnumValidationFailure_SetsFormErrorAndDoesNotClearPicker()
    {
        // UpdateThemeUseCase's own Enum.IsDefined guard rejects an
        // out-of-range value before it ever reaches the store — no
        // FakeSettingsStore configuration is needed to drive this failure.
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(1);
        var invalidTheme = (AppTheme)99;
        viewModel.Theme = invalidTheme;

        await viewModel.SaveThemeCommand.ExecuteAsync(null);

        Assert.NotEqual(string.Empty, viewModel.FormError);
        Assert.Equal(invalidTheme, viewModel.Theme); // picker not cleared on failure
        Assert.Empty(store.SetCalls); // UpdateThemeUseCase's own guard rejects before SetAsync
    }

    [Fact]
    public async Task SaveLocaleAsync_Success_SetsIsRestartRequiredTrue()
    {
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(1);
        viewModel.Locale = SupportedLocales.English;

        Assert.False(viewModel.IsRestartRequired);

        await viewModel.SaveLocaleCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsRestartRequired);
        Assert.Equal(string.Empty, viewModel.FormError);
        Assert.Contains(store.SetCalls, c => c.Key == SettingKeys.Locale && c.Value == "en");
    }

    [Fact]
    public async Task SaveLocaleAsync_UnsupportedLocale_SetsFormErrorAndLeavesRestartFlagFalse()
    {
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(1);
        viewModel.Locale = "fr-FR";

        await viewModel.SaveLocaleCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsRestartRequired);
        Assert.NotEqual(string.Empty, viewModel.FormError);
        Assert.Empty(store.SetCalls); // UpdateLocaleUseCase's allowlist guard rejects before SetAsync
    }

    [Fact]
    public async Task SaveThemeAsync_NeverSetsIsRestartRequired()
    {
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(1);
        viewModel.Theme = AppTheme.Dark;

        await viewModel.SaveThemeCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsRestartRequired);
    }

    [Fact]
    public async Task SaveAiAsync_Success_BuildsCommandFromFormFieldsAndSetsStatusMessage()
    {
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(1);
        viewModel.AiProvider = AiProvider.Anthropic;
        viewModel.AiApiKey = "sk-live-test";
        viewModel.AiModel = "claude-sonnet";
        viewModel.AiBaseUrl = "https://api.example.com/v1";
        viewModel.AiTemperature = 0.9;
        viewModel.AiMaxTokensPerRequest = 4096;

        await viewModel.SaveAiCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.FormError);
        Assert.NotEqual(string.Empty, viewModel.StatusMessage);
        Assert.False(viewModel.IsRestartRequired);
        Assert.Equal(6, store.SetCalls.Count);
        Assert.Contains(store.SetCalls, c => c.Key == SettingKeys.AiProvider && c.Value == "Anthropic");
        Assert.Contains(store.SetCalls, c => c.Key == SettingKeys.AiApiKey && c.Value == "sk-live-test");
        Assert.Contains(store.SetCalls, c => c.Key == SettingKeys.AiModel && c.Value == "claude-sonnet");
        Assert.Contains(store.SetCalls, c => c.Key == SettingKeys.AiBaseUrl && c.Value == "https://api.example.com/v1");
        Assert.Contains(store.SetCalls, c => c.Key == SettingKeys.AiTemperature && c.Value == "0.9");
        Assert.Contains(store.SetCalls, c => c.Key == SettingKeys.AiMaxTokensPerRequest && c.Value == "4096");
    }

    [Theory]
    [InlineData(2.01)]
    [InlineData(-0.1)]
    public async Task SaveAiAsync_OutOfRangeTemperature_SetsFormErrorAndWritesNothing(double temperature)
    {
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(1);
        viewModel.AiTemperature = temperature;

        await viewModel.SaveAiCommand.ExecuteAsync(null);

        Assert.NotEqual(string.Empty, viewModel.FormError);
        Assert.Empty(store.SetCalls);
    }

    [Fact]
    public async Task SaveAiAsync_BoundaryTemperature_IsAccepted()
    {
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(1);
        viewModel.AiTemperature = 2.0;

        await viewModel.SaveAiCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.FormError);
        Assert.NotEmpty(store.SetCalls);
    }

    [Theory]
    [InlineData(128001)]
    [InlineData(0)]
    public async Task SaveAiAsync_OutOfRangeMaxTokens_SetsFormErrorAndWritesNothing(int maxTokens)
    {
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(1);
        viewModel.AiMaxTokensPerRequest = maxTokens;

        await viewModel.SaveAiCommand.ExecuteAsync(null);

        Assert.NotEqual(string.Empty, viewModel.FormError);
        Assert.Empty(store.SetCalls);
    }

    [Fact]
    public async Task SaveAiAsync_BoundaryMaxTokens_IsAccepted()
    {
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(1);
        viewModel.AiMaxTokensPerRequest = 128000;

        await viewModel.SaveAiCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.FormError);
        Assert.NotEmpty(store.SetCalls);
    }

    [Fact]
    public async Task SaveAiAsync_NeverSetsIsRestartRequired()
    {
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(1);

        await viewModel.SaveAiCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsRestartRequired);
    }

    [Fact]
    public void AppVersion_IsNonEmpty()
    {
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);

        Assert.NotEqual(string.Empty, viewModel.AppVersion);
    }

    [Fact]
    public async Task IsAiApiKeyWarningVisible_AlwaysTrue_RegardlessOfApiKeyValue()
    {
        // R-5, S-8: the cleartext-storage warning MUST be visible whether
        // AiApiKey is empty or populated — never conditional on the field.
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(1);

        Assert.Equal(string.Empty, viewModel.AiApiKey);
        Assert.True(viewModel.IsAiApiKeyWarningVisible);

        viewModel.AiApiKey = "sk-now-populated";

        Assert.True(viewModel.IsAiApiKeyWarningVisible);
    }

    // --- Notificaciones (F-017, slice S6: R-14, R-17, D-11, D-15) ---

    [Fact]
    public async Task Constructor_LoadsNotificationSettings_PopulatesTogglesAndFrequency()
    {
        var store = new InMemorySettingsStore();
        store.Seed(SettingKeys.NotificationsDebtDueEnabled, "false");
        store.Seed(SettingKeys.NotificationsAnomalyEnabled, "false");
        store.Seed(SettingKeys.NotificationsDigestFrequency, nameof(DigestFrequency.Daily));

        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(20);

        Assert.False(viewModel.NotifyDebtDue);
        Assert.True(viewModel.NotifyRecurringDue);
        Assert.True(viewModel.NotifyBudget);
        Assert.False(viewModel.NotifyAnomaly);
        Assert.Equal(DigestFrequency.Daily, viewModel.DigestFrequency);
    }

    [Fact]
    public async Task Constructor_NothingPersisted_UsesNotificationDefaults()
    {
        SettingsViewModel viewModel = CreateViewModel(new InMemorySettingsStore());
        await Task.Delay(20);

        Assert.True(viewModel.NotifyDebtDue);
        Assert.True(viewModel.NotifyRecurringDue);
        Assert.True(viewModel.NotifyBudget);
        Assert.True(viewModel.NotifyAnomaly);
        Assert.Equal(DigestFrequency.Weekly, viewModel.DigestFrequency);
    }

    [Fact]
    public void DigestFrequencyOptions_ExposeEveryFrequencyWithDistinctLocalizedLabels()
    {
        SettingsViewModel viewModel = CreateViewModel(new InMemorySettingsStore());

        Assert.Equal(
            new[] { DigestFrequency.Off, DigestFrequency.Daily, DigestFrequency.Weekly },
            viewModel.DigestFrequencyOptions.Select(o => o.Value).ToArray());
        Assert.All(viewModel.DigestFrequencyOptions, o => Assert.NotEqual(string.Empty, o.Label));
        Assert.Equal(
            viewModel.DigestFrequencyOptions.Count,
            viewModel.DigestFrequencyOptions.Select(o => o.Label).Distinct().Count());
    }

    [Fact]
    public async Task SaveNotificationsAsync_Success_PersistsAllFiveKeysAndRefreshesExactlyOnce()
    {
        var store = new InMemorySettingsStore();
        var center = new FakeNotificationCenter();
        SettingsViewModel viewModel = CreateViewModel(store, center);
        await Task.Delay(20);
        viewModel.FormError = "stale error";
        viewModel.NotifyDebtDue = false;
        viewModel.NotifyBudget = false;
        viewModel.DigestFrequency = DigestFrequency.Off;

        await viewModel.SaveNotificationsCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.FormError);
        Assert.NotEqual(string.Empty, viewModel.StatusMessage);
        Assert.False(viewModel.IsRestartRequired);
        Assert.Equal(1, center.RefreshCalls);
        Assert.Equal(0, center.TryRefreshCalls);
        Assert.Equal("false", await store.GetStringAsync(SettingKeys.NotificationsDebtDueEnabled));
        Assert.Equal("true", await store.GetStringAsync(SettingKeys.NotificationsRecurringDueEnabled));
        Assert.Equal("false", await store.GetStringAsync(SettingKeys.NotificationsBudgetEnabled));
        Assert.Equal("true", await store.GetStringAsync(SettingKeys.NotificationsAnomalyEnabled));
        Assert.Equal(nameof(DigestFrequency.Off), await store.GetStringAsync(SettingKeys.NotificationsDigestFrequency));
    }

    [Fact]
    public async Task SaveNotificationsAsync_Failure_SetsFormErrorAndTriggersNoRefresh()
    {
        var store = new FakeSettingsStore { FailOnKey = SettingKeys.NotificationsBudgetEnabled };
        var center = new FakeNotificationCenter();
        SettingsViewModel viewModel = CreateViewModel(store, center);
        await Task.Delay(20);
        viewModel.StatusMessage = "stale status";

        await viewModel.SaveNotificationsCommand.ExecuteAsync(null);

        Assert.NotEqual(string.Empty, viewModel.FormError);
        Assert.Equal(string.Empty, viewModel.StatusMessage);
        Assert.Equal(0, center.RefreshCalls);
        Assert.Equal(0, center.TryRefreshCalls);
    }

    [Fact]
    public async Task SaveNotificationsAsync_WritesOnlyNotificationKeys()
    {
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(store);
        await Task.Delay(20);

        await viewModel.SaveNotificationsCommand.ExecuteAsync(null);

        Assert.Equal(5, store.SetCalls.Count);
        Assert.All(store.SetCalls, c => Assert.StartsWith("notifications.", c.Key, StringComparison.Ordinal));
    }

    [Fact]
    public void Constructor_TakesNoAvaloniaType()
    {
        // Reflection assertion mirroring D-8: the view-model's constructor
        // parameters must not reference any Avalonia namespace type, and
        // the type must not expose a settable property of an Avalonia type.
        ConstructorInfo ctor = typeof(SettingsViewModel).GetConstructors().Single();
        Assert.DoesNotContain(
            ctor.GetParameters(),
            p => p.ParameterType.Namespace?.StartsWith("Avalonia", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Backup_IsNull_WhenNoBackupViewModelIsProvided()
    {
        SettingsViewModel viewModel = CreateViewModel(new FakeSettingsStore());

        Assert.Null(viewModel.Backup);
    }

    [Fact]
    public void Backup_IsExposed_WhenABackupViewModelIsProvided()
    {
        var backupService = new FakeBackupService();
        var backup = new BackupSettingsViewModel(
            new CreateBackupUseCase(backupService, NullLogger<CreateBackupUseCase>.Instance),
            new InspectBackupUseCase(backupService, NullLogger<InspectBackupUseCase>.Instance),
            new RestoreBackupUseCase(backupService, NullLogger<RestoreBackupUseCase>.Instance),
            new FakeFileSaveDialog(),
            new FakeFileOpenDialog(),
            new FakeNotificationCenter(),
            new FakeAppRestartService(),
            new FakeClock(),
            NullLogger<BackupSettingsViewModel>.Instance);

        SettingsViewModel viewModel = CreateViewModel(new FakeSettingsStore(), backup: backup);

        Assert.Same(backup, viewModel.Backup);
    }

    internal static SettingsViewModel CreateViewModel(
        ISettingsStore store,
        FakeNotificationCenter? center = null,
        BackupSettingsViewModel? backup = null,
        IThemeService? themeService = null)
    {
        var getAppSettingsQuery = new GetAppSettingsQuery(store);
        var updateThemeUseCase = new UpdateThemeUseCase(store, NullLogger<UpdateThemeUseCase>.Instance);
        var updateLocaleUseCase = new UpdateLocaleUseCase(store, NullLogger<UpdateLocaleUseCase>.Instance);
        var updateAiSettingsUseCase = new UpdateAiSettingsUseCase(
            store, new UpdateAiSettingsValidator(), NullLogger<UpdateAiSettingsUseCase>.Instance);
        var testAiConnectionUseCase = new TestAiConnectionUseCase(
            store, new FakeLlmClientFactory(), NullLogger<TestAiConnectionUseCase>.Instance);

        return new SettingsViewModel(
            getAppSettingsQuery,
            updateThemeUseCase,
            updateLocaleUseCase,
            updateAiSettingsUseCase,
            testAiConnectionUseCase,
            new GetNotificationSettingsQuery(store),
            new UpdateNotificationSettingsUseCase(store),
            center ?? new FakeNotificationCenter(),
            NullLogger<SettingsViewModel>.Instance,
            backup,
            themeService);
    }
}
