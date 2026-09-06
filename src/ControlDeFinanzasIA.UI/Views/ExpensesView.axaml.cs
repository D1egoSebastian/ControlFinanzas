using Avalonia.Controls;

namespace ControlDeFinanzasIA.UI.Views;

/// <summary>
/// View for the expenses screen. All behavior is data-bound to
/// <see cref="ViewModels.ExpensesViewModel"/>; this code-behind contains no logic.
/// </summary>
public partial class ExpensesView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpensesView"/> class.
    /// </summary>
    public ExpensesView()
    {
        InitializeComponent();
    }
}
