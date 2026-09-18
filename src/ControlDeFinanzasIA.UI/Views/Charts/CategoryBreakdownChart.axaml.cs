using Avalonia;
using Avalonia.Controls;
using LiveChartsCore;

namespace ControlDeFinanzasIA.UI.Views.Charts;

/// <summary>
/// Thin wrapper around LiveCharts2's <c>PieChart</c> for the Dashboard's
/// category-breakdown widget (R-4, D-7). Owns its own empty-state overlay
/// so <c>DashboardView.axaml</c> does not need to duplicate it.
/// </summary>
public partial class CategoryBreakdownChart : UserControl
{
    /// <summary>Identifies the <see cref="Series"/> styled property.</summary>
    public static readonly StyledProperty<IEnumerable<ISeries>?> SeriesProperty =
        AvaloniaProperty.Register<CategoryBreakdownChart, IEnumerable<ISeries>?>(nameof(Series));

    /// <summary>Identifies the <see cref="IsEmpty"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsEmptyProperty =
        AvaloniaProperty.Register<CategoryBreakdownChart, bool>(nameof(IsEmpty));

    /// <summary>Identifies the <see cref="EmptyText"/> styled property.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<CategoryBreakdownChart, string>(
            nameof(EmptyText), defaultValue: "Sin gastos para este periodo.");

    /// <summary>
    /// Initializes a new instance of the <see cref="CategoryBreakdownChart"/> class.
    /// </summary>
    public CategoryBreakdownChart()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the pie series rendered by the wrapped <c>PieChart</c>.
    /// </summary>
    public IEnumerable<ISeries>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the empty-state overlay
    /// should be shown instead of the chart (R-7, S-3).
    /// </summary>
    public bool IsEmpty
    {
        get => GetValue(IsEmptyProperty);
        set => SetValue(IsEmptyProperty, value);
    }

    /// <summary>
    /// Gets or sets the text shown by the empty-state overlay.
    /// </summary>
    public string EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }
}
