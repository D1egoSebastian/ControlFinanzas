using ControlDeFinanzasIA.Application.Settings;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Settings;

/// <summary>Tests para <see cref="AppSettings.Defaults"/> (F-012 D-2, R-1, S-10).</summary>
public sealed class AppSettingsTests
{
    [Fact]
    public void Defaults_MatchesDocumentedValues()
    {
        AppSettings defaults = AppSettings.Defaults;

        Assert.Equal(AppTheme.Light, defaults.Theme);
        Assert.Equal("es-DO", defaults.Locale);
        Assert.Equal(AiProvider.OpenAI, defaults.AiProvider);
        Assert.Equal(string.Empty, defaults.AiApiKey);
        Assert.Equal(string.Empty, defaults.AiModel);
        Assert.Null(defaults.AiBaseUrl);
        Assert.Equal(0.7d, defaults.AiTemperature);
        Assert.Equal(2_000, defaults.AiMaxTokensPerRequest);
    }

    [Fact]
    public void TemperatureBounds_AreZeroToTwoInclusive()
    {
        Assert.Equal(0.0d, AppSettings.MinAiTemperature);
        Assert.Equal(2.0d, AppSettings.MaxAiTemperature);
    }

    [Fact]
    public void MaxTokensBounds_AreOneTo128000Inclusive()
    {
        Assert.Equal(1, AppSettings.MinAiMaxTokensPerRequest);
        Assert.Equal(128_000, AppSettings.MaxAiMaxTokensPerRequest);
    }
}
