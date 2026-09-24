using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;

namespace ControlDeFinanzasIA.UI.Behaviors;

/// <summary>
/// View-only attached behavior (F-015, D-10) that auto-scrolls a <see cref="ScrollViewer"/> to its
/// end whenever the <see cref="INotifyCollectionChanged"/> collection assigned to
/// <see cref="ObserveProperty"/> changes — used to keep the chat transcript pinned to the latest
/// message. No code-behind logic: this is a pure attached property, set declaratively from XAML.
/// </summary>
public static class ScrollToEndOnCollectionChanged
{
    /// <summary>
    /// Attached property holding the collection to observe. Setting it to a new
    /// <see cref="INotifyCollectionChanged"/> subscribes to its <c>CollectionChanged</c> event;
    /// setting it away from one unsubscribes first, so a <see cref="ScrollViewer"/> reused across
    /// bindings never leaks a handler onto a stale collection.
    /// </summary>
    public static readonly AttachedProperty<INotifyCollectionChanged?> ObserveProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, INotifyCollectionChanged?>(
            "Observe", typeof(ScrollToEndOnCollectionChanged));

    private static readonly ConditionalWeakTable<ScrollViewer, NotifyCollectionChangedEventHandler> _handlers = new();

    static ScrollToEndOnCollectionChanged()
    {
        ObserveProperty.Changed.AddClassHandler<ScrollViewer>((scrollViewer, e) => OnObserveChanged(scrollViewer, e));
    }

    /// <summary>Sets the collection to observe on <paramref name="element"/>.</summary>
    public static void SetObserve(ScrollViewer element, INotifyCollectionChanged? value) =>
        element.SetValue(ObserveProperty, value);

    /// <summary>Gets the collection currently observed on <paramref name="element"/>.</summary>
    public static INotifyCollectionChanged? GetObserve(ScrollViewer element) =>
        element.GetValue(ObserveProperty);

    private static void OnObserveChanged(ScrollViewer scrollViewer, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyCollectionChanged oldCollection
            && _handlers.TryGetValue(scrollViewer, out NotifyCollectionChangedEventHandler? oldHandler))
        {
            oldCollection.CollectionChanged -= oldHandler;
            _handlers.Remove(scrollViewer);
        }

        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            NotifyCollectionChangedEventHandler handler = (_, _) => scrollViewer.ScrollToEnd();
            newCollection.CollectionChanged += handler;
            _handlers.AddOrUpdate(scrollViewer, handler);
        }
    }
}
