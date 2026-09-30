using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace ControlDeFinanzasIA.UI.Input;

/// <summary>
/// View-only attached behavior (F-021, DD-6) that GENERATES the key bindings of an element from
/// <see cref="KeyboardShortcutCatalog"/>: one <see cref="KeyBinding"/> per entry of the current platform, each bound to
/// the command an <see cref="IShortcutCommandSource"/> resolves for the entry's action. The source is the explicit
/// <c>Source</c> when set, otherwise the element's data context. No bindings are written by hand anywhere, so the
/// catalog, the window and the Settings list cannot drift. Pure attached properties: no code-behind.
/// </summary>
public static class KeyboardShortcuts
{
    /// <summary>Attached property: when <see langword="true"/> the element carries the generated bindings.</summary>
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<InputElement, bool>("Enabled", typeof(KeyboardShortcuts));

    /// <summary>
    /// Attached property: the command source. Needed where the element's data context is not the shell (the
    /// notifications popup card); otherwise the data context is used.
    /// </summary>
    public static readonly AttachedProperty<IShortcutCommandSource?> SourceProperty =
        AvaloniaProperty.RegisterAttached<InputElement, IShortcutCommandSource?>("Source", typeof(KeyboardShortcuts));

    /// <summary>Attached property: restricts the generated bindings to the Escape entry (popup-local binding).</summary>
    public static readonly AttachedProperty<bool> EscapeOnlyProperty =
        AvaloniaProperty.RegisterAttached<InputElement, bool>("EscapeOnly", typeof(KeyboardShortcuts));

    private static readonly ConditionalWeakTable<InputElement, Registration> _registrations = new();

    static KeyboardShortcuts()
    {
        EnabledProperty.Changed.AddClassHandler<InputElement>((element, e) =>
        {
            if (e.NewValue is true)
            {
                _registrations.GetValue(element, el => new Registration(el)).Refresh();
            }
            else if (_registrations.TryGetValue(element, out Registration? registration))
            {
                registration.Dispose();
                _registrations.Remove(element);
            }
        });

        SourceProperty.Changed.AddClassHandler<InputElement>((element, _) => RefreshIfEnabled(element));
        EscapeOnlyProperty.Changed.AddClassHandler<InputElement>((element, _) => RefreshIfEnabled(element));
    }

    /// <summary>Sets <see cref="EnabledProperty"/>.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The value.</param>
    public static void SetEnabled(InputElement element, bool value) => element.SetValue(EnabledProperty, value);

    /// <summary>Gets <see cref="EnabledProperty"/>.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The value.</returns>
    public static bool GetEnabled(InputElement element) => element.GetValue(EnabledProperty);

    /// <summary>Sets <see cref="SourceProperty"/>.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The value.</param>
    public static void SetSource(InputElement element, IShortcutCommandSource? value) => element.SetValue(SourceProperty, value);

    /// <summary>Gets <see cref="SourceProperty"/>.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The value.</returns>
    public static IShortcutCommandSource? GetSource(InputElement element) => element.GetValue(SourceProperty);

    /// <summary>Sets <see cref="EscapeOnlyProperty"/>.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The value.</param>
    public static void SetEscapeOnly(InputElement element, bool value) => element.SetValue(EscapeOnlyProperty, value);

    /// <summary>Gets <see cref="EscapeOnlyProperty"/>.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The value.</returns>
    public static bool GetEscapeOnly(InputElement element) => element.GetValue(EscapeOnlyProperty);

    private static void RefreshIfEnabled(InputElement element)
    {
        if (_registrations.TryGetValue(element, out Registration? registration))
        {
            registration.Refresh();
        }
    }

    /// <summary>The bindings generated for one element, replaced whenever the source or the options change.</summary>
    private sealed class Registration : IDisposable
    {
        private readonly InputElement _element;
        private readonly List<KeyBinding> _bindings = [];

        public Registration(InputElement element)
        {
            _element = element;
            _element.DataContextChanged += OnDataContextChanged;
        }

        public void Refresh()
        {
            Clear();

            IShortcutCommandSource? source = GetSource(_element) ?? _element.DataContext as IShortcutCommandSource;
            bool escapeOnly = GetEscapeOnly(_element);

            foreach (KeyboardShortcut shortcut in KeyboardShortcutCatalog.ForPlatform(OperatingSystem.IsMacOS()))
            {
                if (escapeOnly && shortcut.Action != ShortcutAction.Escape)
                {
                    continue;
                }

                var binding = new KeyBinding { Gesture = new KeyGesture(shortcut.Key, shortcut.Modifiers) };
                if (source is not null)
                {
                    binding.Command = source.Resolve(shortcut.Action);
                }

                if (shortcut.SectionIndex is int digit)
                {
                    binding.CommandParameter = digit;
                }

                _bindings.Add(binding);
                _element.KeyBindings.Add(binding);
            }
        }

        public void Dispose()
        {
            _element.DataContextChanged -= OnDataContextChanged;
            Clear();
        }

        private void OnDataContextChanged(object? sender, EventArgs e) => Refresh();

        private void Clear()
        {
            foreach (KeyBinding binding in _bindings)
            {
                _element.KeyBindings.Remove(binding);
            }

            _bindings.Clear();
        }
    }
}
