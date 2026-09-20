using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Budgets;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

public sealed class BudgetRepository : IBudgetRepository
{
    private readonly AppDbContext _db;

    public BudgetRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> AddAsync(Budget budget, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(budget);
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> UpdateAsync(Budget budget, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(budget);
        Budget? existing = await _db.Budgets.FindAsync([budget.Id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Budget {budget.Id} was not found.");
        }

        _db.Entry(existing).CurrentValues.SetValues(budget);

        // OwnsOne navigations (Amount) are NOT covered by CurrentValues.SetValues
        // above: it only copies scalar properties of the entity itself. Amount is
        // a separate tracked entry, so it must be copied explicitly or a budget
        // amount edit would silently discard the new value. (D-7)
        _db.Entry(existing).Reference(b => b.Amount).TargetEntry!
            .CurrentValues.SetValues(budget.Amount);

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        Budget? existing = await _db.Budgets.FindAsync([id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Budget {id} was not found.");
        }

        _db.Budgets.Remove(existing);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public Task<Budget?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Budgets.AsNoTracking().FirstOrDefaultAsync(budget => budget.Id == id, ct);

    public async Task<IReadOnlyList<Budget>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Budgets.AsNoTracking()
            .OrderBy(budget => budget.Year).ThenBy(budget => budget.Month).ThenBy(budget => budget.CategoryId)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<Budget>> GetByMonthAsync(int year, int month, CancellationToken ct = default) =>
        await _db.Budgets.AsNoTracking()
            .Where(budget => budget.Year == year && budget.Month == month)
            .OrderBy(budget => budget.CategoryId)
            .ToListAsync(ct).ConfigureAwait(false);

    public Task<Budget?> GetByCategoryAndMonthAsync(Guid categoryId, int year, int month, CancellationToken ct = default) =>
        _db.Budgets.AsNoTracking()
            .FirstOrDefaultAsync(
                budget => budget.CategoryId == categoryId && budget.Year == year && budget.Month == month, ct);
}
