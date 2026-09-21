using ControlDeFinanzasIA.Application.Settings;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Settings;

/// <summary>Tests para la lista blanca <see cref="SupportedLocales"/> (F-012 D-4).</summary>
public sealed class SupportedLocalesTests
{
    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public void IsSupported_SupportedLocale_ReturnsTrue(string locale) =>
        Assert.True(SupportedLocales.IsSupported(locale));

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("")]
    [InlineData(null)]
    public void IsSupported_UnsupportedLocale_ReturnsFalse(string? locale) =>
        Assert.False(SupportedLocales.IsSupported(locale));

    [Fact]
    public void Default_IsSpanishDominican() => Assert.Equal("es-DO", SupportedLocales.Default);

    [Fact]
    public void All_ContainsBothSupportedLocales() =>
        Assert.Equal(["es-DO", "en"], SupportedLocales.All);
}
