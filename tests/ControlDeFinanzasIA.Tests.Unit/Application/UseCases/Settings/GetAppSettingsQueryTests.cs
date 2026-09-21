using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Settings.GetAppSettings;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Settings;

/// <summary>
/// Tests for <see cref="GetAppSettingsQuery"/> (F-012, D-9).
/// </summary>
public sealed class GetAppSettingsQueryTests
{
    [Fact]
    public async Task ExecuteAsync_Always_ReturnsSuccessWithStoreValue()
    {
        var store = new FakeSettingsStore
        {
            AppSettings = AppSettings.Defaults with { Locale = "en", AiApiKey = "sk-test" },
        };
        var query = new GetAppSettingsQuery(store);

        Result result = await query.ExecuteAsync();

        Assert.True(result.IsSuccess);
        AppSettings settings = Assert.IsType<AppSettings>(result.Value);
        Assert.Equal("en", settings.Locale);
        Assert.Equal("sk-test", settings.AiApiKey);
    }

    [Fact]
    public async Task ExecuteAsync_Always_DelegatesToStoreWithoutRederivingDefault()
    {
        var custom = new AppSettings(AppTheme.Light, "en", AiProvider.Anthropic, "key", "model", "https://x.test", 1.2, 500);
        var store = new FakeSettingsStore { AppSettings = custom };
        var query = new GetAppSettingsQuery(store);

        Result result = await query.ExecuteAsync();

        Assert.Equal(custom, result.Value);
    }
}
