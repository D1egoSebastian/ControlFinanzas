using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementacion EF Core de <see cref="IAnomalyAlertRepository"/> (F-014, D-4).
/// </summary>
public sealed class AnomalyAlertRepository : IAnomalyAlertRepository
{
    private readonly AppDbContext _db;

    public AnomalyAlertRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> AddAsync(AnomalyAlert alert, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _db.AnomalyAlerts.Add(alert);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> UpdateAsync(AnomalyAlert alert, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        AnomalyAlert? existing = await _db.AnomalyAlerts.FindAsync([alert.Id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"AnomalyAlert {alert.Id} was not found.");
        }

        // No owned value object on AnomalyAlert (D-4), so the plain
        // CurrentValues.SetValues shape is sufficient -- the BudgetRepository
        // OwnsOne fix is not needed here.
        _db.Entry(existing).CurrentValues.SetValues(alert);

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        AnomalyAlert? existing = await _db.AnomalyAlerts.FindAsync([id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"AnomalyAlert {id} was not found.");
        }

        _db.AnomalyAlerts.Remove(existing);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public Task<AnomalyAlert?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.AnomalyAlerts.AsNoTracking().FirstOrDefaultAsync(alert => alert.Id == id, ct);

    public async Task<IReadOnlyList<AnomalyAlert>> GetAllAsync(CancellationToken ct = default) =>
        await _db.AnomalyAlerts.AsNoTracking()
            .OrderBy(alert => alert.Date).ThenBy(alert => alert.Id)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<AnomalyAlert>> GetByMonthAsync(DateRange month, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(month);
        return await _db.AnomalyAlerts.AsNoTracking()
            .Where(alert => alert.Date >= month.Start && alert.Date <= month.End)
            .OrderBy(alert => alert.Date).ThenBy(alert => alert.Id)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AnomalyAlert>> GetByTransactionIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return [];
        }

        return await _db.AnomalyAlerts.AsNoTracking()
            .Where(alert => ids.Contains(alert.TransactionId))
            .OrderBy(alert => alert.Date).ThenBy(alert => alert.Id)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Result> ApplyReconciliationAsync(AnomalyReconciliationPlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.IsEmpty)
        {
            return Result.Success(null!);
        }

        // Track exactly the entries THIS call attaches, so a failed save can detach
        // only those -- never ChangeTracker.Clear(), which would poison unrelated
        // entities tracked elsewhere on the shared, process-wide AppDbContext (D-4).
        var attachedEntries = new List<AnomalyAlert>();

        foreach (AnomalyAlert alert in plan.ToAdd)
        {
            _db.AnomalyAlerts.Add(alert);
            attachedEntries.Add(alert);
        }

        foreach (AnomalyAlert alert in plan.ToUpdate)
        {
            _db.AnomalyAlerts.Update(alert);
            attachedEntries.Add(alert);
        }

        if (plan.ToRemove.Count > 0)
        {
            List<AnomalyAlert> toRemoveEntities = await _db.AnomalyAlerts
                .Where(alert => plan.ToRemove.Contains(alert.Id))
                .ToListAsync(ct).ConfigureAwait(false);
            _db.AnomalyAlerts.RemoveRange(toRemoveEntities);
            attachedEntries.AddRange(toRemoveEntities);
        }

        try
        {
            // Atomic: one SaveChangesAsync applies ToAdd/ToUpdate/ToRemove together,
            // wrapped by SQLite in a single transaction (D-4).
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return Result.Success(null!);
        }
        catch (DbUpdateException ex)
        {
            foreach (AnomalyAlert entry in attachedEntries)
            {
                _db.Entry(entry).State = EntityState.Detached;
            }

            return Result.Failure(
                "AnomalyReconcileConflict", $"Failed to apply the anomaly reconciliation plan: {ex.Message}");
        }
    }
}
