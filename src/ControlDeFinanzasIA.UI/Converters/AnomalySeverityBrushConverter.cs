using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using ControlDeFinanzasIA.Domain.Alerts;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Maps an <see cref="AnomalySeverity"/> to its design-token severity-chip
/// brush (F-014, S8c): <see cref="AnomalySeverity.Info"/> → SageBrush,
/// <see cref="AnomalySeverity.Warning"/> → AmberBrush,
/// <see cref="AnomalySeverity.Critical"/> → TerracottaBrush. No new colors —
/// the same three tokens the budget-progress bar already uses.
/// </summary>
public sealed class AnomalySeverityBrushConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static AnomalySeverityBrushConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value switch
        {
            AnomalySeverity.Info => "SageBrush",
            AnomalySeverity.Warning => "AmberBrush",
            AnomalySeverity.Critical => "TerracottaBrush",
            _ => null!,
        };

        if (resourceKey is not null
            && Avalonia.Application.Current is { } app
            && app.TryGetResource(resourceKey, ThemeVariant.Default, out object? resource)
            && resource is IBrush brush)
        {
            return brush;
        }

        return new BindingNotification(new InvalidOperationException($"Unsupported severity: {value}"), BindingErrorType.Error);
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new BindingNotification(new NotSupportedException(), BindingErrorType.Error);
}
