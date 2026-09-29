using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Notifications;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="INotificationRepository"/> used in unit
/// tests (F-017, slice S3, D-15). Phase-17-owned — distinct from any
/// shared fake. Stores notifications in memory and exposes a configurable
/// result for <see cref="ApplyReconciliationAsync"/> plus a call counter so
/// tests can assert the reconcile ran (and did not overlap other reads).
/// </summary>
public sealed class FakeNotificationRepository : INotificationRepository
{
    private readonly List<Notification> _notifications = new();

    /// <summary>Gets the notifications currently stored.</summary>
    public IReadOnlyList<Notification> Notifications => _notifications;

    /// <summary>Gets or sets the result returned by <see cref="ApplyReconciliationAsync"/>.</summary>
    public Result ApplyReconciliationResult { get; set; } = Result.Success(null!);

    /// <summary>Number of times <see cref="ApplyReconciliationAsync"/> was invoked.</summary>
    public int ApplyReconciliationCallCount { get; private set; }

    /// <summary>The last plan passed to <see cref="ApplyReconciliationAsync"/>, or <see langword="null"/>.</summary>
    public NotificationReconciliationPlan? LastAppliedPlan { get; private set; }

    public void Seed(Notification notification) => _notifications.Add(notification);

    public void SeedRange(IEnumerable<Notification> notifications) => _notifications.AddRange(notifications);

    /// <inheritdoc />
    public Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Notification>>(_notifications.ToList());

    /// <inheritdoc />
    public Task<IReadOnlyList<Notification>> GetUndismissedAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Notification>>(_notifications
            .Where(n => !n.IsDismissed)
            .OrderBy(n => n.ReferenceDate)
            .ThenBy(n => n.Id)
            .ToList());

    /// <inheritdoc />
    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_notifications.FirstOrDefault(n => n.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<Notification>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return Task.FromResult<IReadOnlyList<Notification>>(
            _notifications.Where(n => ids.Contains(n.Id)).ToList());
    }

    /// <inheritdoc />
    public Task<Result> UpdateRangeAsync(IReadOnlyList<Notification> notifications, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(notifications);

        foreach (Notification notification in notifications)
        {
            int index = _notifications.FindIndex(n => n.Id == notification.Id);
            if (index >= 0)
            {
                _notifications[index] = notification;
            }
        }

        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Result> ApplyReconciliationAsync(NotificationReconciliationPlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ApplyReconciliationCallCount++;
        LastAppliedPlan = plan;

        if (!ApplyReconciliationResult.IsSuccess)
        {
            return Task.FromResult(ApplyReconciliationResult);
        }

        foreach (Notification notification in plan.ToAdd)
        {
            _notifications.Add(notification);
        }

        foreach (Notification notification in plan.ToUpdate)
        {
            int index = _notifications.FindIndex(n => n.Id == notification.Id);
            if (index >= 0)
            {
                _notifications[index] = notification;
            }
        }

        foreach (Guid id in plan.ToRemove)
        {
            _notifications.RemoveAll(n => n.Id == id);
        }

        return Task.FromResult(ApplyReconciliationResult);
    }
}
