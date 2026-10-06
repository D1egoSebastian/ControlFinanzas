using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ControlDeFinanzasIA.Domain.Budgets;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Maps a nullable <see cref="BudgetStatus"/> to the same design-token color
/// the Phase 10 <c>budget-progress</c>/<c>.near</c>/<c>.exceeded</c> bar
/// classes already use, for the Dashboard's forecast-vs-budget line (F-014,
/// S8c): <see cref="BudgetStatus.Ok"/> or <see langword="null"/> →
/// IncomeBrush, <see cref="BudgetStatus.Near"/> → WarningBrush,
/// <see cref="BudgetStatus.Exceeded"/> → ExpenseBrush (text-safe tokens, measured by ContrastTests).
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
            BudgetStatus.Near => "WarningBrush",
            BudgetStatus.Exceeded => "ExpenseBrush",
            _ => "IncomeBrush",
        };

        return ThemeBrushLookup.Find(resourceKey) ?? Brushes.Transparent;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
