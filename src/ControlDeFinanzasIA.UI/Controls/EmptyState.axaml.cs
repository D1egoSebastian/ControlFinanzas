using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace ControlDeFinanzasIA.UI.Controls;

/// <summary>
/// Reusable "blank ledger page" empty state (F-021, DD-9). A pure presentation control: views bind its styled
/// properties and own the visibility rules; there is no logic here.
/// </summary>
public partial class EmptyState : UserControl
{
    /// <summary>Identifies the <see cref="Title"/> styled property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));

    /// <summary>Identifies the <see cref="Hint"/> styled property.</summary>
    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Hint));

    /// <summary>Identifies the <see cref="ActionText"/> styled property.</summary>
    public static readonly StyledProperty<string?> ActionTextProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(ActionText));

    /// <summary>Identifies the <see cref="ActionCommand"/> styled property.</summary>
    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<EmptyState, ICommand?>(nameof(ActionCommand));

    /// <summary>Identifies the <see cref="ShortcutText"/> styled property.</summary>
    public static readonly StyledProperty<string?> ShortcutTextProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(ShortcutText));

    /// <summary>
    /// Initializes a new instance of the <see cref="EmptyState"/> class.
    /// </summary>
    public EmptyState()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the headline.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the supporting sentence under the headline.</summary>
    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    /// <summary>Gets or sets the caption of the action button; no caption means no button.</summary>
    public string? ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    /// <summary>Gets or sets the command invoked by the action button.</summary>
    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    /// <summary>Gets or sets the keyboard shortcut shown as a mono chip inside the action button.</summary>
    public string? ShortcutText
    {
        get => GetValue(ShortcutTextProperty);
        set => SetValue(ShortcutTextProperty, value);
    }
}
