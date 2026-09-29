using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;

namespace ControlDeFinanzasIA.UI.Services;

/// <summary>
/// UI-layer port over the notification use cases (F-017, D-9, R-15).
/// </summary>
/// <remarks>
/// Lives in <c>UI/Services</c> (next to <see cref="IClipboardService"/>) because it exists to
/// bridge singleton ViewModels to the per-scope <c>AppDbContext</c>: every operation runs in
/// its own dependency-injection scope. No member ever throws for a data or persistence
/// failure -- those are logged and swallowed (R-15); only caller cancellation propagates.
/// </remarks>
public interface INotificationCenter
{
    /// <summary>
    /// Raised on the UI synchronization context after every refresh, dismiss and mark-read,
    /// including a failed one, so listeners re-read whatever exists (S-12).
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Runs detection, queuing behind any in-flight operation (startup, settings save).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task RefreshAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs detection unless another operation is already in flight (manual refresh).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="false"/> when an operation was in flight and nothing ran.</returns>
    Task<bool> TryRefreshAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads the current notification list.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The list, or <see langword="null"/> when the read failed (logged).</returns>
    Task<NotificationListDto?> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Marks the given notifications as read.
    /// </summary>
    /// <param name="ids">Identifiers of the notifications to mark.</param>
    /// <param name="ct">Cancellation token.</param>
    Task MarkReadAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Dismisses one notification.
    /// </summary>
    /// <param name="id">Identifier of the notification to dismiss.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DismissAsync(Guid id, CancellationToken ct = default);
}
