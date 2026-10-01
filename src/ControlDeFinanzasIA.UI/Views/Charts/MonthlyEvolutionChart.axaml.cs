using Avalonia;
using Avalonia.Controls;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;

namespace ControlDeFinanzasIA.UI.Views.Charts;

/// <summary>
/// Thin wrapper around LiveCharts2's <c>CartesianChart</c> for the
/// Dashboard's monthly-evolution widget (R-5, D-7). Owns its own
/// empty-state overlay so <c>DashboardView.axaml</c> does not need to
/// duplicate it.
/// </summary>
public partial class MonthlyEvolutionChart : UserControl
{
    /// <summary>Identifies the <see cref="Series"/> styled property.</summary>
    public static readonly StyledProperty<IEnumerable<ISeries>?> SeriesProperty =
        AvaloniaProperty.Register<MonthlyEvolutionChart, IEnumerable<ISeries>?>(nameof(Series));

    /// <summary>Identifies the <see cref="XAxes"/> styled property.</summary>
    public static readonly StyledProperty<IEnumerable<ICartesianAxis>?> XAxesProperty =
        AvaloniaProperty.Register<MonthlyEvolutionChart, IEnumerable<ICartesianAxis>?>(nameof(XAxes));

    /// <summary>Identifies the <see cref="YAxes"/> styled property.</summary>
    public static readonly StyledProperty<IEnumerable<ICartesianAxis>?> YAxesProperty =
        AvaloniaProperty.Register<MonthlyEvolutionChart, IEnumerable<ICartesianAxis>?>(nameof(YAxes));

    /// <summary>Identifies the <see cref="IsEmpty"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsEmptyProperty =
        AvaloniaProperty.Register<MonthlyEvolutionChart, bool>(nameof(IsEmpty));

    /// <summary>Identifies the <see cref="EmptyText"/> styled property.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<MonthlyEvolutionChart, string>(
            nameof(EmptyText), defaultValue: "Sin datos para los últimos meses.");

    /// <summary>
    /// Initializes a new instance of the <see cref="MonthlyEvolutionChart"/> class.
    /// </summary>
    public MonthlyEvolutionChart()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the line series rendered by the wrapped <c>CartesianChart</c>.
    /// </summary>
    public IEnumerable<ISeries>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <summary>
    /// Gets or sets the X axes (month labels) of the wrapped <c>CartesianChart</c>.
    /// </summary>
    public IEnumerable<ICartesianAxis>? XAxes
    {
        get => GetValue(XAxesProperty);
        set => SetValue(XAxesProperty, value);
    }

    /// <summary>
    /// Gets or sets the Y axes (compact amounts, soft gridlines) of the wrapped <c>CartesianChart</c>.
    /// </summary>
    public IEnumerable<ICartesianAxis>? YAxes
    {
        get => GetValue(YAxesProperty);
        set => SetValue(YAxesProperty, value);
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
