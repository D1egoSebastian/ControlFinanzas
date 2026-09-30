using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.UI.Input;

namespace ControlDeFinanzasIA.UI.Behaviors;

/// <summary>
/// View-only attached behavior (F-021, DD-5) that moves keyboard focus when a view-model raises a
/// <see cref="FocusRequestToken"/>. A request made before the control is attached to the visual tree (the target
/// view is created after navigation) waits for the attachment; a disabled or hidden control is simply not focused.
/// Pure attached properties, set declaratively from XAML: no code-behind.
/// </summary>
public static class FocusOnRequest
{
    /// <summary>
    /// Attached property: assigning a NEW token focuses the control it is set on. The token must be a fresh
    /// instance per request so the change notification is always raised.
    /// </summary>
    public static readonly AttachedProperty<FocusRequestToken?> TokenProperty =
        AvaloniaProperty.RegisterAttached<Control, FocusRequestToken?>("Token", typeof(FocusOnRequest));

    /// <summary>
    /// Attached property: assigning a NEW token focuses the first tab stop inside the content host the property
    /// is set on (used to land keyboard focus in a freshly navigated view).
    /// </summary>
    public static readonly AttachedProperty<FocusRequestToken?> FirstFocusableTokenProperty =
        AvaloniaProperty.RegisterAttached<ContentControl, FocusRequestToken?>("FirstFocusableToken", typeof(FocusOnRequest));

    private static readonly ConditionalWeakTable<Control, PendingRequest> _pending = new();

    static FocusOnRequest()
    {
        TokenProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            if (e.NewValue is FocusRequestToken)
            {
                Request(control, () => control.Focus(NavigationMethod.Tab));
            }
        });

        FirstFocusableTokenProperty.Changed.AddClassHandler<ContentControl>((host, e) =>
        {
            if (e.NewValue is FocusRequestToken)
            {
                Request(host, () => FocusFirstTabStop(host));
            }
        });
    }

    /// <summary>Sets the focus-request token on <paramref name="element"/>.</summary>
    /// <param name="element">The control to focus.</param>
    /// <param name="value">A fresh token, or <see langword="null"/>.</param>
    public static void SetToken(Control element, FocusRequestToken? value) =>
        element.SetValue(TokenProperty, value);

    /// <summary>Gets the focus-request token of <paramref name="element"/>.</summary>
    /// <param name="element">The control.</param>
    /// <returns>The last token, if any.</returns>
    public static FocusRequestToken? GetToken(Control element) =>
        element.GetValue(TokenProperty);

    /// <summary>Sets the first-focusable token on <paramref name="element"/>.</summary>
    /// <param name="element">The content host.</param>
    /// <param name="value">A fresh token, or <see langword="null"/>.</param>
    public static void SetFirstFocusableToken(ContentControl element, FocusRequestToken? value) =>
        element.SetValue(FirstFocusableTokenProperty, value);

    /// <summary>Gets the first-focusable token of <paramref name="element"/>.</summary>
    /// <param name="element">The content host.</param>
    /// <returns>The last token, if any.</returns>
    public static FocusRequestToken? GetFirstFocusableToken(ContentControl element) =>
        element.GetValue(FirstFocusableTokenProperty);

    private static void FocusFirstTabStop(ContentControl host)
    {
        IInputElement? first = KeyboardNavigationHandler.GetNext(host, NavigationDirection.Next);
        if (first is Visual visual && host.IsVisualAncestorOf(visual))
        {
            first.Focus(NavigationMethod.Tab);
        }
    }

    private static void Request(Control control, Action focus)
    {
        if (control.IsAttachedToVisualTree())
        {
            Dispatcher.UIThread.Post(focus, DispatcherPriority.Loaded);
            return;
        }

        // Not attached yet: keep only the latest request and run it once on attachment.
        if (_pending.TryGetValue(control, out PendingRequest? existing))
        {
            existing.Focus = focus;
            return;
        }

        var request = new PendingRequest { Focus = focus };
        _pending.Add(control, request);
        control.AttachedToVisualTree += OnAttached;

        void OnAttached(object? sender, VisualTreeAttachmentEventArgs args)
        {
            control.AttachedToVisualTree -= OnAttached;
            _pending.Remove(control);
            Dispatcher.UIThread.Post(request.Focus, DispatcherPriority.Loaded);
        }
    }

    private sealed class PendingRequest
    {
        public required Action Focus { get; set; }
    }
}
