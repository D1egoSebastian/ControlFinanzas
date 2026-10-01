using SkiaSharp;

namespace ControlDeFinanzasIA.UI.Views.Charts;

/// <summary>
/// The colors of the dashboard charts. LiveCharts paints with SkiaSharp, not with Avalonia brushes, so the chart
/// colors cannot come from <c>Tokens.axaml</c> at run time; they are mirrored here and <c>ContrastTests</c> pins that
/// the semantic ones equal the tokens and that every slice color reaches 3:1 on the white card.
/// </summary>
public static class ChartPalette
{
    /// <summary>Gets the income color (the <c>Income</c> token).</summary>
    public static string IncomeHex => "#0B7048";

    /// <summary>Gets the expense color (the <c>Expense</c> token).</summary>
    public static string ExpenseHex => "#B82A44";

    /// <summary>Gets the axis and legend text color (the <c>TextSecondary</c> token).</summary>
    public static string AxisTextHex => "#444B5C";

    /// <summary>Gets the gridline color (the <c>Divider</c> token).</summary>
    public static string GridHex => "#E6E2D8";

    /// <summary>Gets the category-slice colors, in the order slices are assigned.</summary>
    public static IReadOnlyList<string> SliceHexes { get; } =
    [
        "#3459E6",
        "#D56F12",
        "#0E8C8C",
        "#8A4FD0",
        "#C23B7A",
        "#A67C00",
        "#6B7A1E",
        "#8B6B4A",
        "#5B6272",
    ];

    /// <summary>Gets the slice color for the zero-based slice <paramref name="index"/>, cycling when there are more slices than colors.</summary>
    /// <param name="index">The slice index.</param>
    /// <returns>The color.</returns>
    public static SKColor Slice(int index) => SKColor.Parse(SliceHexes[index % SliceHexes.Count]);

    /// <summary>Gets the income color as a Skia color.</summary>
    public static SKColor Income => SKColor.Parse(IncomeHex);

    /// <summary>Gets the expense color as a Skia color.</summary>
    public static SKColor Expense => SKColor.Parse(ExpenseHex);

    /// <summary>Gets the axis text color as a Skia color.</summary>
    public static SKColor AxisText => SKColor.Parse(AxisTextHex);

    /// <summary>Gets the gridline color as a Skia color.</summary>
    public static SKColor Grid => SKColor.Parse(GridHex);
}
