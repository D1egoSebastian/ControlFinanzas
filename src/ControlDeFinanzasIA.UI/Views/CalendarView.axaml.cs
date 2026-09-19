using Avalonia.Controls;

namespace ControlDeFinanzasIA.UI.Views;

/// <summary>
/// Code-behind for the expense calendar route view (F-009, slice 3): month
/// header with prev/next navigation, the weekday header row, and the
/// <see cref="Calendar.MonthGrid"/> host.
/// </summary>
public partial class CalendarView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarView"/> class.
    /// </summary>
    public CalendarView()
    {
        InitializeComponent();
    }
}
