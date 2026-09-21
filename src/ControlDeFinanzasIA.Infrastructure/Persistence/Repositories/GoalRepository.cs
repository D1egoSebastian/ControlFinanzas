using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Goals;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

public sealed class GoalRepository : IGoalRepository
{
    private readonly AppDbContext _db;

    public GoalRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> AddAsync(Goal goal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goal);
        _db.Goals.Add(goal);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> UpdateAsync(Goal goal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goal);
        Goal? existing = await _db.Goals.FindAsync([goal.Id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Goal {goal.Id} was not found.");
        }

        _db.Entry(existing).CurrentValues.SetValues(goal);

        // OwnsOne navigations (TargetAmount) are NOT covered by CurrentValues.SetValues
        // above: it only copies scalar properties of the entity itself. TargetAmount is
        // a separate tracked entry, so it must be copied explicitly or a target-amount
        // edit would silently discard the new value. Copied from BudgetRepository (D-9)
        // — DebtRepository.UpdateAsync still has the unfixed version.
        _db.Entry(existing).Reference(g => g.TargetAmount).TargetEntry!
            .CurrentValues.SetValues(goal.TargetAmount);

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        Goal? existing = await _db.Goals.FindAsync([id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Goal {id} was not found.");
        }

        _db.Goals.Remove(existing);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public Task<Goal?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Goals.AsNoTracking().FirstOrDefaultAsync(goal => goal.Id == id, ct);

    public async Task<IReadOnlyList<Goal>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Goals.AsNoTracking()
            .OrderBy(goal => goal.TargetDate).ThenBy(goal => goal.Name)
            .ToListAsync(ct).ConfigureAwait(false);
}
