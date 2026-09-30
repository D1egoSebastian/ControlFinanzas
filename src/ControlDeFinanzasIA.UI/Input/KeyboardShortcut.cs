using Avalonia.Input;

namespace ControlDeFinanzasIA.UI.Input;

/// <summary>
/// One entry of the shortcut catalog (F-021, DD-6): a gesture, the action it triggers and the resource key of its
/// description.
/// </summary>
/// <param name="Action">The action the gesture triggers.</param>
/// <param name="Key">The key of the gesture.</param>
/// <param name="Modifiers">The modifiers of the gesture (Control everywhere; Meta only for the macOS duplicates).</param>
/// <param name="SectionIndex">For <see cref="ShortcutAction.GoToSection"/>, the digit (1..9, 0 for the tenth); otherwise <see langword="null"/>.</param>
/// <param name="DescriptionKey">The <c>Strings</c> resource key naming the action.</param>
public sealed record KeyboardShortcut(
    ShortcutAction Action,
    Key Key,
    KeyModifiers Modifiers,
    int? SectionIndex,
    string DescriptionKey);
