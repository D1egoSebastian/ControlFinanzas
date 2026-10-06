using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.ViewModels;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Dark mode: choosing a theme in the Settings picker applies it to the running application AT ONCE (through
/// <c>IThemeService</c>) and saves it, with no restart and no Save click; loading the stored value into the picker never
/// writes it back.
/// </summary>
public sealed class SettingsViewModelThemeTests
{
    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task ChoosingATheme_AppliesItLive_AndSavesIt()
    {
        var store = new FakeSettingsStore();
        var theme = new FakeThemeService();
        SettingsViewModel viewModel = SettingsViewModelTests.CreateViewModel(store, themeService: theme);
        await WaitForAsync(() => !viewModel.IsLoading);
        int appliedBefore = theme.Applied.Count;

        viewModel.Theme = AppTheme.Dark;
        await WaitForAsync(() => store.SetCalls.Any(c => c.Key == SettingKeys.Theme));

        Assert.Equal(AppTheme.Dark, theme.Applied[^1]);
        Assert.True(theme.Applied.Count > appliedBefore);
        Assert.Contains(store.SetCalls, c => c.Key == SettingKeys.Theme && c.Value == "Dark");
        Assert.Equal(string.Empty, viewModel.FormError);
        Assert.NotEqual(string.Empty, viewModel.StatusMessage);
        Assert.False(viewModel.IsRestartRequired);
    }

    [Fact]
    public async Task ChoosingLightAfterDark_AppliesLightLive()
    {
        var store = new FakeSettingsStore { AppSettings = AppSettings.Defaults with { Theme = AppTheme.Dark } };
        var theme = new FakeThemeService();
        SettingsViewModel viewModel = SettingsViewModelTests.CreateViewModel(store, themeService: theme);
        await WaitForAsync(() => !viewModel.IsLoading);

        viewModel.Theme = AppTheme.Light;
        await WaitForAsync(() => store.SetCalls.Any(c => c.Key == SettingKeys.Theme));

        Assert.Equal(AppTheme.Light, theme.Applied[^1]);
        Assert.Contains(store.SetCalls, c => c.Key == SettingKeys.Theme && c.Value == "Light");
    }

    [Fact]
    public async Task LoadingTheStoredTheme_PopulatesThePicker_WithoutWritingOrReapplyingIt()
    {
        var store = new FakeSettingsStore { AppSettings = AppSettings.Defaults with { Theme = AppTheme.Dark } };
        var theme = new FakeThemeService();

        SettingsViewModel viewModel = SettingsViewModelTests.CreateViewModel(store, themeService: theme);
        await WaitForAsync(() => !viewModel.IsLoading && viewModel.Theme == AppTheme.Dark);
        await Task.Delay(50);

        Assert.Equal(AppTheme.Dark, viewModel.Theme);
        Assert.DoesNotContain(store.SetCalls, c => c.Key == SettingKeys.Theme);
        Assert.Empty(theme.Applied);
    }

    [Fact]
    public async Task WhenSavingFails_TheChoiceStaysApplied_AndTheErrorIsShown()
    {
        var store = new FakeSettingsStore { FailOnKey = SettingKeys.Theme };
        var theme = new FakeThemeService();
        SettingsViewModel viewModel = SettingsViewModelTests.CreateViewModel(store, themeService: theme);
        await WaitForAsync(() => !viewModel.IsLoading);

        viewModel.Theme = AppTheme.Dark;
        await WaitForAsync(() => viewModel.FormError.Length > 0);

        Assert.NotEqual(string.Empty, viewModel.FormError);
        Assert.Equal(AppTheme.Dark, theme.Applied[^1]);
    }

    [Fact]
    public async Task WithoutAThemeService_ChoosingAThemeStillSaves()
    {
        var store = new FakeSettingsStore();
        SettingsViewModel viewModel = SettingsViewModelTests.CreateViewModel(store);
        await WaitForAsync(() => !viewModel.IsLoading);

        viewModel.Theme = AppTheme.Dark;
        await WaitForAsync(() => store.SetCalls.Any(c => c.Key == SettingKeys.Theme));

        Assert.Contains(store.SetCalls, c => c.Key == SettingKeys.Theme && c.Value == "Dark");
    }

    [Fact]
    public void TheFirstRunTheme_IsLight_SoAnUntouchedInstallKeepsItsLook()
    {
        Assert.Equal(AppTheme.Light, AppSettings.DefaultTheme);
        Assert.Equal(AppTheme.Light, AppSettings.Defaults.Theme);
    }
}
