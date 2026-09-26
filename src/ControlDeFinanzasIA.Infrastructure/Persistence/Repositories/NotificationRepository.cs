using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementacion EF Core de <see cref="INotificationRepository"/> (F-017, D-8).
/// </summary>
public sealed class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;

    public NotificationRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Notifications.AsNoTracking()
            .OrderBy(notification => notification.ReferenceDate).ThenBy(notification => notification.Id)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<Notification>> GetUndismissedAsync(CancellationToken ct = default) =>
        await _db.Notifications.AsNoTracking()
            .Where(notification => notification.DismissedAt == null)
            .OrderBy(notification => notification.ReferenceDate).ThenBy(notification => notification.Id)
            .ToListAsync(ct).ConfigureAwait(false);

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Notifications.AsNoTracking().FirstOrDefaultAsync(notification => notification.Id == id, ct);

    public async Task<IReadOnlyList<Notification>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return [];
        }

        return await _db.Notifications.AsNoTracking()
            .Where(notification => ids.Contains(notification.Id))
            .OrderBy(notification => notification.ReferenceDate).ThenBy(notification => notification.Id)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Result> UpdateRangeAsync(
        IReadOnlyList<Notification> notifications, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(notifications);

        if (notifications.Count == 0)
        {
            return Result.Success(null!);
        }

        // Track exactly the entries THIS call attaches, so a failed save can detach
        // only those -- never ChangeTracker.Clear(), which would poison unrelated
        // entities tracked elsewhere on the shared, process-wide AppDbContext.
        var attachedEntries = new List<Notification>();

        foreach (Notification notification in notifications)
        {
            _db.Notifications.Update(notification);
            attachedEntries.Add(notification);
        }

        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return Result.Success(null!);
        }
        catch (DbUpdateException ex)
        {
            foreach (Notification entry in attachedEntries)
            {
                _db.Entry(entry).State = EntityState.Detached;
            }

            return Result.Failure(
                "NotificationUpdateConflict", $"Failed to update the notification batch: {ex.Message}");
        }
    }

    public async Task<Result> ApplyReconciliationAsync(
        NotificationReconciliationPlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.IsEmpty)
        {
            return Result.Success(null!);
        }

        // Track exactly the entries THIS call attaches, so a failed save can detach
        // only those -- never ChangeTracker.Clear(), which would poison unrelated
        // entities tracked elsewhere on the shared, process-wide AppDbContext (D-1).
        var attachedEntries = new List<Notification>();

        foreach (Notification notification in plan.ToAdd)
        {
            _db.Notifications.Add(notification);
            attachedEntries.Add(notification);
        }

        foreach (Notification notification in plan.ToUpdate)
        {
            _db.Notifications.Update(notification);
            attachedEntries.Add(notification);
        }

        if (plan.ToRemove.Count > 0)
        {
            List<Notification> toRemoveEntities = await _db.Notifications
                .Where(notification => plan.ToRemove.Contains(notification.Id))
                .ToListAsync(ct).ConfigureAwait(false);
            _db.Notifications.RemoveRange(toRemoveEntities);
            attachedEntries.AddRange(toRemoveEntities);
        }

        try
        {
            // Atomic: one SaveChangesAsync applies ToAdd/ToUpdate/ToRemove together,
            // wrapped by SQLite in a single transaction (D-1).
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return Result.Success(null!);
        }
        catch (DbUpdateException ex)
        {
            foreach (Notification entry in attachedEntries)
            {
                _db.Entry(entry).State = EntityState.Detached;
            }

            return Result.Failure(
                "NotificationReconcileConflict", $"Failed to apply the notification reconciliation plan: {ex.Message}");
        }
    }
}
