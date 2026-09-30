using System.Windows.Input;

namespace ControlDeFinanzasIA.UI.Input;

/// <summary>
/// Supplies the command behind each catalog action so the <see cref="KeyboardShortcuts"/> behavior can generate the
/// window key bindings without knowing the view-model (F-021, DD-6). Implemented by the shell view-model.
/// </summary>
public interface IShortcutCommandSource
{
    /// <summary>Gets the command for <paramref name="action"/>; the same instance on every call.</summary>
    /// <param name="action">The catalog action.</param>
    /// <returns>The command. <see cref="ShortcutAction.GoToSection"/> takes the section digit as its parameter.</returns>
    ICommand Resolve(ShortcutAction action);
}
