using System.Globalization;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for <see cref="App.ResolveUiCulture(AppSettings, Microsoft.Extensions.Logging.ILogger?)"/>,
/// the extracted, side-effect-free portion of the F-012 D-7 startup culture read.
/// </summary>
public sealed class AppUiCultureTests
{
    [Fact]
    public void ResolveUiCulture_ValidPersistedLocale_ResolvesThatCulture()
    {
        // Arrange
        AppSettings settings = AppSettings.Defaults with { Locale = "en" };

        // Act
        CultureInfo culture = App.ResolveUiCulture(settings);

        // Assert
        Assert.Equal("en", culture.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveUiCulture_EmptyOrWhitespaceLocale_FallsBackToDefault(string locale)
    {
        // Arrange
        AppSettings settings = AppSettings.Defaults with { Locale = locale };

        // Act
        CultureInfo culture = App.ResolveUiCulture(settings, NullLogger.Instance);

        // Assert
        Assert.Equal(SupportedLocales.Default, culture.Name);
    }

    [Fact]
    public void ResolveUiCulture_InvalidLocale_FallsBackToDefaultWithoutThrowing()
    {
        // Arrange
        AppSettings settings = AppSettings.Defaults with { Locale = "not-a-real-culture" };

        // Act
        CultureInfo culture = App.ResolveUiCulture(settings, NullLogger.Instance);

        // Assert
        Assert.Equal(SupportedLocales.Default, culture.Name);
    }

    [Fact]
    public void ResolveUiCulture_NullSettings_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => App.ResolveUiCulture(null!));
}
