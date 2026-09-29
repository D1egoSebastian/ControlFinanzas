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
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> TryRefreshAsync(CancellationToken ct = default)
    {
        TryRefreshCalls++;
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
        return Task.FromResult(ListToReturn);
    }

    /// <inheritdoc />
    public Task MarkReadAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        MarkReadCalls.Add(ids.ToArray());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DismissAsync(Guid id, CancellationToken ct = default)
    {
        DismissCalls.Add(id);
        if (RaiseChangedOnDismiss)
        {
            RaiseChanged();
        }

        return Task.CompletedTask;
    }
}
