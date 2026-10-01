using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Maps a sidebar route (<c>RouteNames</c>) to its vector icon: the <see cref="Geometry"/> resource named
/// <c>"Icon" + route</c> in <c>Styles/Icons.axaml</c>. A route without an icon yields no geometry, so the
/// sidebar item simply shows no glyph instead of failing.
/// </summary>
public sealed class RouteIconConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static RouteIconConverter Instance { get; } = new();

    /// <summary>Gets the resource key of the icon for <paramref name="route"/>.</summary>
    /// <param name="route">The route name.</param>
    /// <returns>The resource key.</returns>
    public static string KeyFor(string route) => "Icon" + route;

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string route
            && Avalonia.Application.Current is { } app
            && app.TryGetResource(KeyFor(route), ThemeVariant.Default, out object? resource)
            && resource is Geometry geometry)
        {
            return geometry;
        }

        return BindingOperations.DoNothing;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
