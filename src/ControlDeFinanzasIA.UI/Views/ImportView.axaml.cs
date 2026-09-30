using Avalonia.Controls;

namespace ControlDeFinanzasIA.UI.Views;

/// <summary>
/// View of the import panel hosted by the Expenses screen. All behavior is data-bound to
/// <see cref="ViewModels.ImportViewModel"/>; this code-behind contains no logic.
/// </summary>
public partial class ImportView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImportView"/> class.
    /// </summary>
    public ImportView()
    {
        InitializeComponent();
    }
}
