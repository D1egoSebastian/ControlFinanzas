using ControlDeFinanzasIA.Application.Settings;
using SkiaSharp;

namespace ControlDeFinanzasIA.UI.Views.Charts;

/// <summary>
/// The colors of the dashboard charts for ONE theme variant. LiveCharts paints with SkiaSharp, not with Avalonia brushes,
/// so the chart colors cannot come from the token dictionaries at run time; they are mirrored here per variant and
/// <c>ContrastTests</c> pins that the semantic ones equal the tokens of that variant and that every slice color reaches 3:1
/// on the card of that variant.
/// </summary>
/// <param name="IncomeHex">The income color (the <c>Income</c> token).</param>
/// <param name="ExpenseHex">The expense color (the <c>Expense</c> token).</param>
/// <param name="AxisTextHex">The axis and legend text color (the <c>TextSecondary</c> token).</param>
/// <param name="GridHex">The gridline color (the <c>Divider</c> token).</param>
/// <param name="SurfaceHex">The card color (the <c>Surface</c> token): the gap between pie slices and the fill of a line marker.</param>
/// <param name="SliceHexes">The category-slice colors, in the order slices are assigned.</param>
public sealed record ChartColors(
    string IncomeHex,
    string ExpenseHex,
    string AxisTextHex,
    string GridHex,
    string SurfaceHex,
    IReadOnlyList<string> SliceHexes)
{
    /// <summary>Gets the income color as a Skia color.</summary>
    public SKColor Income => SKColor.Parse(IncomeHex);

    /// <summary>Gets the expense color as a Skia color.</summary>
    public SKColor Expense => SKColor.Parse(ExpenseHex);

    /// <summary>Gets the axis text color as a Skia color.</summary>
    public SKColor AxisText => SKColor.Parse(AxisTextHex);

    /// <summary>Gets the gridline color as a Skia color.</summary>
    public SKColor Grid => SKColor.Parse(GridHex);

    /// <summary>Gets the card color as a Skia color.</summary>
    public SKColor Surface => SKColor.Parse(SurfaceHex);

    /// <summary>Gets the slice color for the zero-based slice <paramref name="index"/>, cycling when there are more slices than colors.</summary>
    /// <param name="index">The slice index.</param>
    /// <returns>The color.</returns>
    public SKColor Slice(int index) => SKColor.Parse(SliceHexes[index % SliceHexes.Count]);

    /// <summary>Gets the slice color of the zero-based slice <paramref name="index"/> as hex text.</summary>
    /// <param name="index">The slice index.</param>
    /// <returns>The hex color.</returns>
    public string SliceHex(int index) => SliceHexes[index % SliceHexes.Count];
}

/// <summary>The chart palettes of the two theme variants.</summary>
public static class ChartPalette
{
    private static readonly ChartColors _light = new(
        IncomeHex: "#0B7048",
        ExpenseHex: "#B82A44",
        AxisTextHex: "#444B5C",
        GridHex: "#E6E2D8",
        SurfaceHex: "#FFFFFF",
        SliceHexes:
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
        ]);

    private static readonly ChartColors _dark = new(
        IncomeHex: "#4CC38A",
        ExpenseHex: "#F27B93",
        AxisTextHex: "#CBC7BC",
        GridHex: "#37352D",
        SurfaceHex: "#1D1C17",
        SliceHexes:
        [
            "#7088F0",
            "#F0913F",
            "#35B8B8",
            "#B98AF0",
            "#E86FA3",
            "#D4A82E",
            "#A3B83C",
            "#C49A6C",
            "#9097A8",
        ]);

    /// <summary>Gets the income color of the light variant (the <c>Income</c> token).</summary>
    public static string IncomeHex => _light.IncomeHex;

    /// <summary>Gets the expense color of the light variant (the <c>Expense</c> token).</summary>
    public static string ExpenseHex => _light.ExpenseHex;

    /// <summary>Gets the axis and legend text color of the light variant (the <c>TextSecondary</c> token).</summary>
    public static string AxisTextHex => _light.AxisTextHex;

    /// <summary>Gets the gridline color of the light variant (the <c>Divider</c> token).</summary>
    public static string GridHex => _light.GridHex;

    /// <summary>Gets the category-slice colors of the light variant, in the order slices are assigned.</summary>
    public static IReadOnlyList<string> SliceHexes => _light.SliceHexes;

    /// <summary>Gets the chart colors of a theme variant.</summary>
    /// <param name="theme">The variant.</param>
    /// <returns>The palette; <see cref="AppTheme.Dark"/> gets the dark one, anything else the light one.</returns>
    public static ChartColors For(AppTheme theme) => theme == AppTheme.Dark ? _dark : _light;

    /// <summary>Gets the slice color for the zero-based slice <paramref name="index"/> of the light variant.</summary>
    /// <param name="index">The slice index.</param>
    /// <returns>The color.</returns>
    public static SKColor Slice(int index) => _light.Slice(index);

    /// <summary>Gets the income color of the light variant as a Skia color.</summary>
    public static SKColor Income => _light.Income;

    /// <summary>Gets the expense color of the light variant as a Skia color.</summary>
    public static SKColor Expense => _light.Expense;

    /// <summary>Gets the axis text color of the light variant as a Skia color.</summary>
    public static SKColor AxisText => _light.AxisText;

    /// <summary>Gets the gridline color of the light variant as a Skia color.</summary>
    public static SKColor Grid => _light.Grid;
}
