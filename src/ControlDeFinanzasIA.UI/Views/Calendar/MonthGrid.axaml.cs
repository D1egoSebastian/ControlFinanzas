using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.UI.Views.Calendar;

/// <summary>
/// Day-grid <see cref="UserControl"/> for the expense calendar (F-009,
/// slice 3, D-6). Owns a single shared <c>ItemTemplate</c> over a
/// <c>UniformGrid</c> driven by <see cref="Cells"/> and its own
/// empty-state overlay, following the <c>Views/Charts/*</c> convention:
/// <see cref="StyledProperty{T}"/> plumbing only, no logic in code-behind.
/// </summary>
public partial class MonthGrid : UserControl
{
    /// <summary>Identifies the <see cref="Cells"/> styled property.</summary>
    public static readonly StyledProperty<IEnumerable<CalendarCell>?> CellsProperty =
        AvaloniaProperty.Register<MonthGrid, IEnumerable<CalendarCell>?>(nameof(Cells));

    /// <summary>Identifies the <see cref="DayCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> DayCommandProperty =
        AvaloniaProperty.Register<MonthGrid, ICommand?>(nameof(DayCommand));

    /// <summary>Identifies the <see cref="EmptyText"/> styled property.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<MonthGrid, string>(nameof(EmptyText), defaultValue: string.Empty);

    /// <summary>
    /// Initializes a new instance of the <see cref="MonthGrid"/> class.
    /// </summary>
    public MonthGrid()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the grid cells rendered by the shared <c>ItemTemplate</c>.
    /// </summary>
    public IEnumerable<CalendarCell>? Cells
    {
        get => GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    /// <summary>
    /// Gets or sets the command executed when a day cell is clicked, with
    /// the clicked <see cref="CalendarCell"/> as its parameter.
    /// </summary>
    public ICommand? DayCommand
    {
        get => GetValue(DayCommandProperty);
        set => SetValue(DayCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the text shown by the empty-state overlay when
    /// <see cref="Cells"/> is <see langword="null"/> or empty.
    /// </summary>
    public string EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }
}
