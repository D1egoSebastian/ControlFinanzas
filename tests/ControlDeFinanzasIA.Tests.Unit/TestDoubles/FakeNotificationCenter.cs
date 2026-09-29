using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;
using ControlDeFinanzasIA.UI.Services;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Phase 17-owned double for <see cref="INotificationCenter"/> (F-017, D-15): records every call,
/// lets a test gate <see cref="TryRefreshAsync"/> and raise <see cref="Changed"/> on demand.
/// </summary>
public sealed class FakeNotificationCenter : INotificationCenter
{
    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Gets every member call in order (for example <c>Suspend</c>, <c>SuspendDisposed</c>), so a test can assert sequencing.</summary>
    public List<string> CallLog { get; } = new();

    /// <summary>Gets the number of <see cref="SuspendAsync"/> calls.</summary>
    public int SuspendCalls { get; private set; }

    /// <summary>Gets the number of returned suspension handles that were disposed (once each).</summary>
    public int SuspensionsDisposed { get; private set; }

    /// <summary>Gets a value indicating whether a returned suspension handle is still undisposed.</summary>
    public bool IsSuspended => SuspendCalls > SuspensionsDisposed;

    /// <summary>Gets or sets an exception thrown by <see cref="SuspendAsync"/> when set.</summary>
    public Exception? SuspendException { get; set; }

    /// <summary>Gets the ids passed to each <see cref="MarkReadAsync"/> call.</summary>
    public List<IReadOnlyCollection<Guid>> MarkReadCalls { get; } = new();

    /// <summary>Gets the ids passed to <see cref="DismissAsync"/>.</summary>
    public List<Guid> DismissCalls { get; } = new();

    /// <summary>Gets the number of <see cref="RefreshAsync"/> calls.</summary>
    public int RefreshCalls { get; private set; }

    /// <summary>Gets the number of <see cref="TryRefreshAsync"/> calls.</summary>
    public int TryRefreshCalls { get; private set; }

    /// <summary>Gets the number of <see cref="GetAsync"/> calls.</summary>
    public int GetCalls { get; private set; }

    /// <summary>Gets or sets the list returned by <see cref="GetAsync"/>; <see langword="null"/> simulates a failed read.</summary>
    public NotificationListDto? ListToReturn { get; set; } = new NotificationListDto(Array.Empty<NotificationDto>(), 0);

    /// <summary>Gets or sets a gate that <see cref="TryRefreshAsync"/> awaits before completing, when set.</summary>
    public TaskCompletionSource<bool>? TryRefreshGate { get; set; }

    /// <summary>Gets or sets a value indicating whether <see cref="DismissAsync"/> raises <see cref="Changed"/>.</summary>
    public bool RaiseChangedOnDismiss { get; set; } = true;

    /// <summary>Raises <see cref="Changed"/>.</summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public Task RefreshAsync(CancellationToken ct = default)
    {
        RefreshCalls++;
        CallLog.Add("Refresh");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> TryRefreshAsync(CancellationToken ct = default)
    {
        TryRefreshCalls++;
        CallLog.Add("TryRefresh");
        if (TryRefreshGate is not null)
        {
            await TryRefreshGate.Task;
        }

        return true;
    }

    /// <inheritdoc />
    public Task<NotificationListDto?> GetAsync(CancellationToken ct = default)
    {
        GetCalls++;
        CallLog.Add("Get");
        return Task.FromResult(ListToReturn);
    }

    /// <inheritdoc />
    public Task MarkReadAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        MarkReadCalls.Add(ids.ToArray());
        CallLog.Add("MarkRead");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DismissAsync(Guid id, CancellationToken ct = default)
    {
        DismissCalls.Add(id);
        CallLog.Add("Dismiss");
        if (RaiseChangedOnDismiss)
        {
            RaiseChanged();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IDisposable> SuspendAsync(CancellationToken ct = default)
    {
        CallLog.Add("Suspend");
        if (SuspendException is not null)
        {
            return Task.FromException<IDisposable>(SuspendException);
        }

        SuspendCalls++;
        return Task.FromResult<IDisposable>(new Handle(this));
    }

    private sealed class Handle(FakeNotificationCenter owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.SuspensionsDisposed++;
            owner.CallLog.Add("SuspendDisposed");
        }
    }
}
