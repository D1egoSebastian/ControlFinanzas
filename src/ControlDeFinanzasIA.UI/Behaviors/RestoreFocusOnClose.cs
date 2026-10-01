using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ControlDeFinanzasIA.UI.Behaviors;

/// <summary>
/// View-only attached behavior (F-021 S8) for a modal layer such as the error overlay: it remembers the element that
/// had keyboard focus before the layer opened and returns focus to it when the layer closes, so the next key press
/// acts on the screen the user was on instead of on a hidden button. If that element is gone, hidden or disabled,
/// focus is left alone. Pure attached properties, set declaratively from XAML: no code-behind.
/// </summary>
/// <remarks>
/// The state is process-wide on purpose: the app has one main window, and a hidden layer is not attached to the visual
/// tree when it opens, so the memory cannot be looked up through the layer's <see cref="TopLevel"/>.
/// </remarks>
public static class RestoreFocusOnClose
{
    /// <summary>Attached property: bind to the "layer is open" flag of the modal layer this is set on.</summary>
    public static readonly AttachedProperty<bool?> IsOpenProperty =
        AvaloniaProperty.RegisterAttached<Control, bool?>("IsOpen", typeof(RestoreFocusOnClose));

    private static readonly List<WeakReference<Control>> _openLayers = [];

    private static WeakReference<Control>? _lastOutside;

    static RestoreFocusOnClose()
    {
        // Every window and popup root reports focus; only focus that lands OUTSIDE an open layer is remembered.
        InputElement.GotFocusEvent.AddClassHandler<TopLevel>(OnGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);

        IsOpenProperty.Changed.AddClassHandler<Control>((layer, e) =>
        {
            if (e.NewValue is true)
            {
                _openLayers.Add(new WeakReference<Control>(layer));
                return;
            }

            if (_openLayers.RemoveAll(r => !r.TryGetTarget(out Control? l) || ReferenceEquals(l, layer)) > 0)
            {
                Restore();
            }
        });
    }

    /// <summary>Sets the open flag on <paramref name="element"/>.</summary>
    /// <param name="element">The modal layer.</param>
    /// <param name="value">Whether the layer is open.</param>
    public static void SetIsOpen(Control element, bool? value) => element.SetValue(IsOpenProperty, value);

    /// <summary>Gets the open flag of <paramref name="element"/>.</summary>
    /// <param name="element">The modal layer.</param>
    /// <returns>Whether the layer is open.</returns>
    public static bool? GetIsOpen(Control element) => element.GetValue(IsOpenProperty);

    private static void OnGotFocus(TopLevel topLevel, GotFocusEventArgs e)
    {
        if (e.Source is Control source && !IsInsideAnOpenLayer(source))
        {
            _lastOutside = new WeakReference<Control>(source);
        }
    }

    private static bool IsInsideAnOpenLayer(Control source) =>
        _openLayers.Any(r => r.TryGetTarget(out Control? layer) && (ReferenceEquals(layer, source) || layer.IsVisualAncestorOf(source)));

    private static void Restore()
    {
        if (_lastOutside is null || !_lastOutside.TryGetTarget(out Control? target))
        {
            return;
        }

        // The shell is re-enabled by its own binding when the layer closes, so focus is returned after that.
        Dispatcher.UIThread.Post(
            () =>
            {
                if (target.IsAttachedToVisualTree() && target.IsEffectivelyVisible && target.IsEffectivelyEnabled && target.Focusable)
                {
                    target.Focus(NavigationMethod.Unspecified);
                }
            },
            DispatcherPriority.Loaded);
    }
}
