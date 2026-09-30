using System.Globalization;
using Avalonia.Input;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.UI.Input;

/// <summary>
/// The single list of window keyboard shortcuts (F-021, DD-6). The window key bindings, the tooltips and the Settings
/// list are all generated from it, so behavior and documentation cannot drift.
/// </summary>
public static class KeyboardShortcutCatalog
{
    private static readonly Key[] _sectionKeys = [Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9, Key.D0];

    private static readonly IReadOnlyList<KeyboardShortcut> _all = BuildAll();

    private static readonly IReadOnlyList<KeyboardShortcut> _macOS = BuildMacOS();

    /// <summary>
    /// Gets the 15 Control-based entries: new expense, new income, sections <c>1</c>..<c>9</c> and <c>0</c>, settings,
    /// notifications and Escape.
    /// </summary>
    public static IReadOnlyList<KeyboardShortcut> All => _all;

    /// <summary>
    /// Gets the entries that apply on a platform: <see cref="All"/>, plus on macOS one <c>Meta</c> (command) twin of
    /// every Control entry. Windows and Linux get no <c>Meta</c> entry because <c>Win+digit</c> belongs to the
    /// operating system.
    /// </summary>
    /// <param name="isMacOS">Whether the platform is macOS.</param>
    /// <returns>The entries for the platform.</returns>
    public static IReadOnlyList<KeyboardShortcut> ForPlatform(bool isMacOS) => isMacOS ? _macOS : _all;

    /// <summary>
    /// Formats a shortcut for display: <c>Ctrl+Shift+N</c> on Windows and Linux (the Shift label comes from the
    /// resources), a symbol form such as <c>⌘⇧N</c> on macOS.
    /// </summary>
    /// <param name="shortcut">The shortcut.</param>
    /// <param name="isMacOS">Whether to format for macOS.</param>
    /// <returns>The display text.</returns>
    public static string Format(KeyboardShortcut shortcut, bool isMacOS)
    {
        ArgumentNullException.ThrowIfNull(shortcut);

        string key = KeyLabel(shortcut);
        if (isMacOS)
        {
            bool command = shortcut.Modifiers.HasFlag(KeyModifiers.Control) || shortcut.Modifiers.HasFlag(KeyModifiers.Meta);
            return (command ? "⌘" : string.Empty)
                + (shortcut.Modifiers.HasFlag(KeyModifiers.Shift) ? "⇧" : string.Empty)
                + key;
        }

        var parts = new List<string>(3);
        if (shortcut.Modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (shortcut.Modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add(Strings.ShortcutKeyShift);
        }

        parts.Add(key);
        return string.Join('+', parts);
    }

    private static string KeyLabel(KeyboardShortcut shortcut) => shortcut.Key switch
    {
        Key.Escape => "Esc",
        Key.OemComma => ",",
        _ when shortcut.SectionIndex is int digit => digit.ToString(CultureInfo.InvariantCulture),
        _ => shortcut.Key.ToString(),
    };

    private static List<KeyboardShortcut> BuildAll()
    {
        var entries = new List<KeyboardShortcut>(15)
        {
            new(ShortcutAction.NewExpense, Key.N, KeyModifiers.Control, null, "ShortcutNewExpense"),
            new(ShortcutAction.NewIncome, Key.N, KeyModifiers.Control | KeyModifiers.Shift, null, "ShortcutNewIncome"),
        };

        for (int i = 0; i < _sectionKeys.Length; i++)
        {
            // Ctrl+1 .. Ctrl+9 are the first nine sections; Ctrl+0 is the tenth.
            int digit = (i + 1) % 10;
            entries.Add(new(ShortcutAction.GoToSection, _sectionKeys[i], KeyModifiers.Control, digit, "ShortcutGoToSection"));
        }

        entries.Add(new(ShortcutAction.OpenSettings, Key.OemComma, KeyModifiers.Control, null, "ShortcutOpenSettings"));
        entries.Add(new(ShortcutAction.ToggleNotifications, Key.B, KeyModifiers.Control | KeyModifiers.Shift, null, "ShortcutToggleNotifications"));
        entries.Add(new(ShortcutAction.Escape, Key.Escape, KeyModifiers.None, null, "ShortcutEscape"));
        return entries;
    }

    private static List<KeyboardShortcut> BuildMacOS()
    {
        var entries = new List<KeyboardShortcut>(_all);
        foreach (KeyboardShortcut original in _all.Where(s => s.Modifiers.HasFlag(KeyModifiers.Control)))
        {
            entries.Add(original with { Modifiers = (original.Modifiers & ~KeyModifiers.Control) | KeyModifiers.Meta });
        }

        return entries;
    }
}
