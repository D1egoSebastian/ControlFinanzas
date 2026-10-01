using CommunityToolkit.Mvvm.ComponentModel;
using ControlDeFinanzasIA.UI.Navigation;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// One flat sidebar entry (F-021, DD-7): a single interactive element that knows its label, the digit that opens it
/// with <c>Ctrl+digit</c>, the gesture text for its tooltip and whether it is the section on screen.
/// </summary>
public sealed partial class SidebarItemViewModel : ObservableObject
{
    private readonly string _currentStatusText;

    /// <summary>
    /// Initializes a new instance of the <see cref="SidebarItemViewModel"/> class.
    /// </summary>
    /// <param name="section">The section the item opens.</param>
    /// <param name="label">The localized label.</param>
    /// <param name="digitHint">The digit that opens the section, or empty when it has none.</param>
    /// <param name="shortcutText">The gesture text for the tooltip and accelerator key, generated from the catalog.</param>
    /// <param name="currentStatusText">The localized "current section" text exposed to automation while current.</param>
    /// <param name="positionInSet">The 1-based position of the item in the sidebar.</param>
    /// <param name="sizeOfSet">The number of items in the sidebar.</param>
    public SidebarItemViewModel(
        NavigationSection section,
        string label,
        string digitHint,
        string shortcutText,
        string currentStatusText,
        int positionInSet,
        int sizeOfSet)
    {
        ArgumentNullException.ThrowIfNull(section);

        Section = section;
        Label = label;
        DigitHint = digitHint;
        ShortcutText = shortcutText;
        PositionInSet = positionInSet;
        SizeOfSet = sizeOfSet;
        _currentStatusText = currentStatusText;
    }

    /// <summary>Gets the section the item opens.</summary>
    public NavigationSection Section { get; }

    /// <summary>Gets the route of the section.</summary>
    public string Route => Section.Route;

    /// <summary>Gets the localized label.</summary>
    public string Label { get; }

    /// <summary>Gets the digit that opens the section with <c>Ctrl+digit</c>; empty when it has none.</summary>
    public string DigitHint { get; }

    /// <summary>Gets the catalog-generated gesture text for the tooltip and the accelerator key.</summary>
    public string ShortcutText { get; }

    /// <summary>Gets the 1-based position of the item in the sidebar.</summary>
    public int PositionInSet { get; }

    /// <summary>Gets the number of items in the sidebar.</summary>
    public int SizeOfSet { get; }

    /// <summary>Gets or sets a value indicating whether the section is the one on screen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemStatus))]
    private bool _isCurrent;

    /// <summary>Gets the automation status: the localized "current section" while current, otherwise empty.</summary>
    public string ItemStatus => IsCurrent ? _currentStatusText : string.Empty;
}
