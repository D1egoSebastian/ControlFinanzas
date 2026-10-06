using Avalonia.Media;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Finds a theme token brush for the converters that return a fixed brush. The token dictionaries are theme-variant
/// dictionaries (Light / Dark), so the lookup must name the variant that is active NOW: asking for the default variant
/// finds nothing. A converter runs when its binding is evaluated, i.e. when a screen is created or its data changes; every
/// screen is created after the theme was applied, and a screen that already exists is not re-evaluated by a theme switch
/// (the switch happens in Settings, and the other screens are rebuilt when the user navigates back to them).
/// </summary>
internal static class ThemeBrushLookup
{
    /// <summary>Finds the brush resource <paramref name="key"/> in the active theme variant.</summary>
    /// <param name="key">The resource key, for example <c>IncomeBrush</c>.</param>
    /// <returns>The brush, or <see langword="null"/> when the application or the resource is not available.</returns>
    public static IBrush? Find(string key)
    {
        if (Avalonia.Application.Current is { } app
            && app.TryGetResource(key, app.ActualThemeVariant, out object? resource)
            && resource is IBrush brush)
        {
            return brush;
        }

        return null;
    }
}
