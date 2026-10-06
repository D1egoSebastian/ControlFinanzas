using Avalonia.Styling;
using Avalonia.Threading;
using ControlDeFinanzasIA.Application.Settings;

namespace ControlDeFinanzasIA.UI.Services;

/// <summary>
/// <see cref="IThemeService"/> over the running Avalonia application: <see cref="Apply"/> maps the preference to an
/// Avalonia <see cref="ThemeVariant"/> and sets <c>Application.RequestedThemeVariant</c> on the UI thread.
/// </summary>
public sealed class AvaloniaThemeService : IThemeService
{
    private AppTheme _current = AppSettings.DefaultTheme;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public AppTheme Current => _current;

    /// <summary>Maps a theme preference to the Avalonia variant that renders it.</summary>
    /// <param name="theme">The preference.</param>
    /// <returns><see cref="ThemeVariant.Dark"/> for <see cref="AppTheme.Dark"/>, otherwise <see cref="ThemeVariant.Light"/>.</returns>
    public static ThemeVariant ToVariant(AppTheme theme) => theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

    /// <inheritdoc />
    public void Apply(AppTheme theme)
    {
        if (!Enum.IsDefined(theme))
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyOnUiThread(theme);
            return;
        }

        Dispatcher.UIThread.Post(() => ApplyOnUiThread(theme));
    }

    private void ApplyOnUiThread(AppTheme theme)
    {
        if (Avalonia.Application.Current is { } app)
        {
            app.RequestedThemeVariant = ToVariant(theme);
        }

        if (_current == theme)
        {
            return;
        }

        _current = theme;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
