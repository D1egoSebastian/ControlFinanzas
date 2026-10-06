using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ControlDeFinanzasIA.Domain.Alerts;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Maps an <see cref="AnomalySeverity"/> to its design-token severity-chip brush (F-014, S8c). By default it returns
/// the chip WASH (background): <see cref="AnomalySeverity.Info"/> → AccentSoftBrush,
/// <see cref="AnomalySeverity.Warning"/> → WarningSoftBrush, <see cref="AnomalySeverity.Critical"/> →
/// ExpenseSoftBrush. With the converter parameter <c>text</c> it returns the matching text-safe token
/// (AccentBrush, WarningBrush, ExpenseBrush), each measured against its wash by <c>ContrastTests</c>. No new colors.
/// </summary>
public sealed class AnomalySeverityBrushConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static AnomalySeverityBrushConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool text = string.Equals(parameter as string, "text", StringComparison.Ordinal);
        string resourceKey = value switch
        {
            AnomalySeverity.Info => text ? "AccentBrush" : "AccentSoftBrush",
            AnomalySeverity.Warning => text ? "WarningBrush" : "WarningSoftBrush",
            AnomalySeverity.Critical => text ? "ExpenseBrush" : "ExpenseSoftBrush",
            _ => null!,
        };

        if (resourceKey is not null && ThemeBrushLookup.Find(resourceKey) is { } brush)
        {
            return brush;
        }

        return new BindingNotification(new InvalidOperationException($"Unsupported severity: {value}"), BindingErrorType.Error);
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new BindingNotification(new NotSupportedException(), BindingErrorType.Error);
}
