namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// One entry of the category-chart legend: the slice label, its palette color, its DOP amount and its share, so the pie
/// never relies on color alone.
/// </summary>
/// <param name="Label">The category label.</param>
/// <param name="ColorHex">The slice color as <c>#RRGGBB</c> (from <c>ChartPalette</c>).</param>
/// <param name="AmountDisplay">The pre-formatted DOP amount.</param>
/// <param name="PercentDisplay">The share of the month total, for example <c>75%</c>.</param>
public sealed record CategoryLegendItem(string Label, string ColorHex, string AmountDisplay, string PercentDisplay);
