using Avalonia.Controls;

namespace ControlDeFinanzasIA.UI.Views;

/// <summary>
/// View for the Dashboard screen (F-008, slice 2): period navigation,
/// summary card and the two charts. All content is data-bound to
/// <see cref="DashboardViewModel"/>; this code-behind contains no logic.
/// </summary>
public partial class DashboardView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardView"/> class.
    /// </summary>
    public DashboardView()
    {
        InitializeComponent();
    }
}
