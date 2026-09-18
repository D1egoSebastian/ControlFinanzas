using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Recurring;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

public sealed class RecurringRuleRepository : IRecurringRuleRepository
{
    private readonly AppDbContext _db;

    public RecurringRuleRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> AddAsync(RecurringRule rule, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _db.RecurringRules.Add(rule);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> UpdateAsync(RecurringRule rule, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        RecurringRule? existing = await _db.RecurringRules.FindAsync([rule.Id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Recurring rule {rule.Id} was not found.");
        }

        _db.Entry(existing).CurrentValues.SetValues(rule);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        RecurringRule? existing = await _db.RecurringRules.FindAsync([id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Recurring rule {id} was not found.");
        }

        _db.RecurringRules.Remove(existing);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public Task<RecurringRule?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.RecurringRules.AsNoTracking().FirstOrDefaultAsync(rule => rule.Id == id, ct);

    public async Task<IReadOnlyList<RecurringRule>> GetAllAsync(CancellationToken ct = default) =>
        await _db.RecurringRules.AsNoTracking().OrderBy(rule => rule.NextGenerationDate).ToListAsync(ct).ConfigureAwait(false);
}
