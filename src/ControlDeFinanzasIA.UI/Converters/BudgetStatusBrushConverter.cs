using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using ControlDeFinanzasIA.Domain.Budgets;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Maps a nullable <see cref="BudgetStatus"/> to the same design-token color
/// the Phase 10 <c>budget-progress</c>/<c>.near</c>/<c>.exceeded</c> bar
/// classes already use, for the Dashboard's forecast-vs-budget line (F-014,
/// S8c): <see cref="BudgetStatus.Ok"/> or <see langword="null"/> →
/// SageBrush, <see cref="BudgetStatus.Near"/> → AmberBrush,
/// <see cref="BudgetStatus.Exceeded"/> → TerracottaBrush.
/// </summary>
public sealed class BudgetStatusBrushConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static BudgetStatusBrushConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value switch
        {
            BudgetStatus.Near => "AmberBrush",
            BudgetStatus.Exceeded => "TerracottaBrush",
            _ => "SageBrush",
        };

        if (Avalonia.Application.Current is { } app
            && app.TryGetResource(resourceKey, ThemeVariant.Default, out object? resource)
            && resource is IBrush brush)
        {
            return brush;
        }

        return Brushes.Transparent;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
