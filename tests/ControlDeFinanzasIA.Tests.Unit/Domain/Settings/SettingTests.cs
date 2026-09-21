using ControlDeFinanzasIA.Domain.Settings;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Settings;

/// <summary>
/// Tests para la guarda de <see cref="Setting.Key"/> en su accessor
/// <see langword="init"/> (F-012, D-1).
/// </summary>
public sealed class SettingTests
{
    [Fact]
    public void Key_ValidValue_IsAccepted()
    {
        var setting = new Setting { Id = Guid.NewGuid(), Key = "theme", Value = "Dark" };

        Assert.Equal("theme", setting.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Key_EmptyOrWhitespace_ThrowsArgumentException(string key) =>
        Assert.Throws<ArgumentException>(() => new Setting { Id = Guid.NewGuid(), Key = key });

    [Fact]
    public void Key_Null_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => new Setting { Id = Guid.NewGuid(), Key = null! });

    [Fact]
    public void Key_SurroundedByWhitespace_IsTrimmed()
    {
        var setting = new Setting { Id = Guid.NewGuid(), Key = " theme " };

        Assert.Equal("theme", setting.Key);
    }

    [Fact]
    public void Value_Empty_IsAccepted()
    {
        var setting = new Setting { Id = Guid.NewGuid(), Key = "ai.apiKey", Value = string.Empty };

        Assert.Equal(string.Empty, setting.Value);
    }

    [Fact]
    public void Id_Default_IsGuidEmptyAndDoesNotThrow()
    {
        var setting = new Setting { Key = "theme" };

        Assert.Equal(Guid.Empty, setting.Id);
    }

    [Fact]
    public void With_ValueChange_PreservesIdAndKey()
    {
        var id = Guid.NewGuid();
        var setting = new Setting { Id = id, Key = "theme", Value = "Dark" };

        Setting updated = setting with { Value = "Light" };

        Assert.Equal(id, updated.Id);
        Assert.Equal("theme", updated.Key);
        Assert.Equal("Light", updated.Value);
    }
}
