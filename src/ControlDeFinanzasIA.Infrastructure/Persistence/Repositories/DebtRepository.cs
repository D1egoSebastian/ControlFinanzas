using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Debts;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

public sealed class DebtRepository : IDebtRepository
{
    private readonly AppDbContext _db;

    public DebtRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> AddAsync(Debt debt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(debt);
        _db.Debts.Add(debt);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> AddWithScheduleAsync(Debt debt, IReadOnlyList<DebtPayment> payments, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(debt);
        ArgumentNullException.ThrowIfNull(payments);

        _db.Debts.Add(debt);
        _db.DebtPayments.AddRange(payments);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> UpdateAsync(Debt debt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(debt);
        Debt? existing = await _db.Debts.FindAsync([debt.Id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Debt {debt.Id} was not found.");
        }

        _db.Entry(existing).CurrentValues.SetValues(debt);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        Debt? existing = await _db.Debts.FindAsync([id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Debt {id} was not found.");
        }

        _db.Debts.Remove(existing);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public Task<Debt?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Debts.AsNoTracking().FirstOrDefaultAsync(debt => debt.Id == id, ct);

    public async Task<IReadOnlyList<Debt>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Debts.AsNoTracking().OrderBy(debt => debt.StartDate).ToListAsync(ct).ConfigureAwait(false);
}
