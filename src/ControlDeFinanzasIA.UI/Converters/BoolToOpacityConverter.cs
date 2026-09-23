using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Converts a <see cref="bool"/> into a dimmed/full opacity value. Used by the AI suggestion chip
/// (F-013, slice 6, D-13) to signal "a request is in flight" without a loading spinner — the roadmap
/// requirement is "async, non-blocking", and a spinner beside a form field reads as "wait for me".
/// </summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    /// <summary>Opacity applied while the bound value is <see langword="true"/>.</summary>
    private const double _dimmedOpacity = 0.55;

    /// <summary>Opacity applied while the bound value is <see langword="false"/>.</summary>
    private const double _fullOpacity = 1.0;

    /// <summary>Shared instance for XAML <c>{x:Static}</c> usage.</summary>
    public static readonly BoolToOpacityConverter Instance = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? _dimmedOpacity : _fullOpacity;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new BindingNotification(new NotSupportedException(), BindingErrorType.Error);
}
