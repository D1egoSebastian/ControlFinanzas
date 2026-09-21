using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Goals;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

public sealed class GoalContributionRepository : IGoalContributionRepository
{
    private readonly AppDbContext _db;

    public GoalContributionRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> AddAsync(GoalContribution contribution, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        _db.GoalContributions.Add(contribution);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> UpdateAsync(GoalContribution contribution, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        GoalContribution? existing = await _db.GoalContributions.FindAsync([contribution.Id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Goal contribution {contribution.Id} was not found.");
        }

        _db.Entry(existing).CurrentValues.SetValues(contribution);

        // Same explicit owned-Money copy as GoalRepository.UpdateAsync (D-9). This
        // repository must not become a third occurrence of the CurrentValues.SetValues
        // owned-type gap that DebtRepository and DebtPaymentRepository still have.
        _db.Entry(existing).Reference(c => c.Amount).TargetEntry!
            .CurrentValues.SetValues(contribution.Amount);

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        GoalContribution? existing = await _db.GoalContributions.FindAsync([id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Goal contribution {id} was not found.");
        }

        _db.GoalContributions.Remove(existing);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public Task<GoalContribution?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.GoalContributions.AsNoTracking().FirstOrDefaultAsync(contribution => contribution.Id == id, ct);

    public async Task<IReadOnlyList<GoalContribution>> GetAllAsync(CancellationToken ct = default) =>
        await _db.GoalContributions.AsNoTracking()
            .OrderBy(contribution => contribution.Date)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<GoalContribution>> GetByGoalIdAsync(Guid goalId, CancellationToken ct = default) =>
        await _db.GoalContributions
            .AsNoTracking()
            .Where(contribution => contribution.GoalId == goalId)
            .OrderBy(contribution => contribution.Date)
            .ToListAsync(ct)
            .ConfigureAwait(false);
}
