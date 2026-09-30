namespace ControlDeFinanzasIA.UI.Input;

/// <summary>
/// Display texts of catalog shortcuts for XAML (<c>x:Static</c>), produced by
/// <see cref="KeyboardShortcutCatalog.Format"/> so a chip never repeats a gesture by hand (F-021, DD-6).
/// </summary>
public static class ShortcutTexts
{
    /// <summary>Gets the display text of the new-expense shortcut for this platform.</summary>
    public static string NewExpense => Of(ShortcutAction.NewExpense);

    /// <summary>Gets the display text of the new-income shortcut for this platform.</summary>
    public static string NewIncome => Of(ShortcutAction.NewIncome);

    private static string Of(ShortcutAction action) =>
        KeyboardShortcutCatalog.Format(
            KeyboardShortcutCatalog.All.Single(s => s.Action == action),
            OperatingSystem.IsMacOS());
}
