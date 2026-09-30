using Avalonia.Controls;

namespace ControlDeFinanzasIA.UI.Views;

/// <summary>
/// The in-window unexpected-error overlay (F-021, DD-4). All layout and behavior are declared in
/// <c>ErrorOverlayView.axaml</c> and <c>ErrorOverlayViewModel</c>; this code-behind contains no logic.
/// </summary>
public partial class ErrorOverlayView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="ErrorOverlayView"/> class.</summary>
    public ErrorOverlayView()
    {
        InitializeComponent();
    }
}
