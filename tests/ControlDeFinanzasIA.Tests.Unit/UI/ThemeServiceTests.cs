using Avalonia.Styling;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI;
using ControlDeFinanzasIA.UI.Services;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Dark mode: the theme service sets the REAL application's requested variant (live, no restart), the mapping covers every
/// stored value, and the startup path applies the persisted choice without ever throwing.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class ThemeServiceTests
{
    [Theory]
    [InlineData(AppTheme.Dark, "Dark")]
    [InlineData(AppTheme.Light, "Light")]
    public void TheMapping_CoversEveryStoredValue(AppTheme theme, string expected) =>
        Assert.Equal(expected, AvaloniaThemeService.ToVariant(theme).Key.ToString());

    [Fact]
    public async Task Apply_Dark_ThenLight_ChangesTheApplicationVariant_AndRaisesChangedOnlyOnARealChange()
    {
        (ThemeVariant? afterDark, ThemeVariant? afterLight, int changes, AppTheme current) = await HeadlessUi.OnUiThread(() =>
        {
            var service = new AvaloniaThemeService();
            int raised = 0;
            service.Changed += (_, _) => raised++;
            Avalonia.Application app = Avalonia.Application.Current!;
            ThemeVariant? original = app.RequestedThemeVariant;
            try
            {
                service.Apply(AppTheme.Light);
                service.Apply(AppTheme.Dark);
                service.Apply(AppTheme.Dark);
                ThemeVariant? dark = app.RequestedThemeVariant;
                service.Apply(AppTheme.Light);
                return Task.FromResult<(ThemeVariant?, ThemeVariant?, int, AppTheme)>((dark, app.RequestedThemeVariant, raised, service.Current));
            }
            finally
            {
                app.RequestedThemeVariant = original;
            }
        });

        Assert.Equal(ThemeVariant.Dark, afterDark);
        Assert.Equal(ThemeVariant.Light, afterLight);
        Assert.Equal(2, changes);
        Assert.Equal(AppTheme.Light, current);
    }

    [Fact]
    public async Task Apply_AnUndefinedValue_IsIgnored()
    {
        (ThemeVariant? variant, AppTheme current) = await HeadlessUi.OnUiThread(() =>
        {
            var service = new AvaloniaThemeService();
            Avalonia.Application app = Avalonia.Application.Current!;
            ThemeVariant? original = app.RequestedThemeVariant;
            try
            {
                service.Apply(AppTheme.Dark);
                service.Apply((AppTheme)99);
                return Task.FromResult<(ThemeVariant?, AppTheme)>((app.RequestedThemeVariant, service.Current));
            }
            finally
            {
                app.RequestedThemeVariant = original;
            }
        });

        Assert.Equal(ThemeVariant.Dark, variant);
        Assert.Equal(AppTheme.Dark, current);
    }

    [Fact]
    public async Task Apply_FromABackgroundThread_LandsOnTheUiThread()
    {
        ThemeVariant? variant = await HeadlessUi.OnUiThread(async () =>
        {
            var service = new AvaloniaThemeService();
            Avalonia.Application app = Avalonia.Application.Current!;
            ThemeVariant? original = app.RequestedThemeVariant;
            try
            {
                await Task.Run(() => service.Apply(AppTheme.Dark));
                HeadlessUi.Flush();
                return app.RequestedThemeVariant;
            }
            finally
            {
                app.RequestedThemeVariant = original;
            }
        });

        Assert.Equal(ThemeVariant.Dark, variant);
    }

    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.Light)]
    public void TheStartupPath_AppliesThePersistedTheme(AppTheme persisted)
    {
        var service = new FakeThemeService();

        App.ApplyStartupTheme(service, AppSettings.Defaults with { Theme = persisted });

        Assert.Equal([persisted], service.Applied);
    }

    [Fact]
    public void TheStartupPath_NeverThrows_WhenTheServiceFails()
    {
        var service = new ThrowingThemeService();

        Exception? failure = Record.Exception(() => App.ApplyStartupTheme(service, AppSettings.Defaults));

        Assert.Null(failure);
    }

    private sealed class ThrowingThemeService : IThemeService
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public AppTheme Current => AppTheme.Light;

        public void Apply(AppTheme theme) => throw new InvalidOperationException("boom");
    }
}
