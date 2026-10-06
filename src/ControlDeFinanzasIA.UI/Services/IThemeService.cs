using ControlDeFinanzasIA.Application.Settings;

namespace ControlDeFinanzasIA.UI.Services;

/// <summary>
/// Applies the user's theme preference to the running application and tells interested view-models (the dashboard
/// charts, which paint with Skia colours rather than resources) when it changes. The Avalonia implementation sets
/// <c>Application.RequestedThemeVariant</c>, so every control that references a theme token with a dynamic resource is
/// re-skinned without a restart.
/// </summary>
public interface IThemeService
{
    /// <summary>Occurs, on the UI thread, after a different theme has been applied.</summary>
    event EventHandler? Changed;

    /// <summary>Gets the theme that is currently applied.</summary>
    AppTheme Current { get; }

    /// <summary>Applies <paramref name="theme"/>; a value that is not a defined <see cref="AppTheme"/> is ignored.</summary>
    /// <param name="theme">The theme to apply.</param>
    void Apply(AppTheme theme);
}
