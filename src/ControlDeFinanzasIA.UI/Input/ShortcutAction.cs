namespace ControlDeFinanzasIA.UI.Input;

/// <summary>
/// The actions reachable by a window-level keyboard shortcut (F-021, DD-6). <c>Enter</c> is deliberately absent: it is
/// the <c>IsDefault</c> button of each form, not a window binding.
/// </summary>
public enum ShortcutAction
{
    /// <summary>Start a new expense entry.</summary>
    NewExpense,

    /// <summary>Start a new income entry.</summary>
    NewIncome,

    /// <summary>Open the sidebar section at the digit's position.</summary>
    GoToSection,

    /// <summary>Open the Settings section.</summary>
    OpenSettings,

    /// <summary>Open or close the notifications popup.</summary>
    ToggleNotifications,

    /// <summary>Close the topmost layer: the error overlay, else the notifications popup.</summary>
    Escape,
}
