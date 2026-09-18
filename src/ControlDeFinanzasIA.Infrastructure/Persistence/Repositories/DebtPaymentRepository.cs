using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

public sealed class DebtPaymentRepository : IDebtPaymentRepository
{
    private readonly AppDbContext _db;

    public DebtPaymentRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> AddAsync(DebtPayment payment, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        _db.DebtPayments.Add(payment);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> UpdateAsync(DebtPayment payment, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        DebtPayment? existing = await _db.DebtPayments.FindAsync([payment.Id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Debt payment {payment.Id} was not found.");
        }

        _db.Entry(existing).CurrentValues.SetValues(payment);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        DebtPayment? existing = await _db.DebtPayments.FindAsync([id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Debt payment {id} was not found.");
        }

        _db.DebtPayments.Remove(existing);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public Task<DebtPayment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.DebtPayments.AsNoTracking().FirstOrDefaultAsync(payment => payment.Id == id, ct);

    public async Task<IReadOnlyList<DebtPayment>> GetAllAsync(CancellationToken ct = default) =>
        await _db.DebtPayments.AsNoTracking().OrderBy(payment => payment.ScheduledDate).ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<DebtPayment>> GetByDebtIdAsync(Guid debtId, CancellationToken ct = default) =>
        await _db.DebtPayments
            .AsNoTracking()
            .Where(payment => payment.DebtId == debtId)
            .OrderBy(payment => payment.InstallmentNumber)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    /// <summary>
    /// Filtro SQL real (<c>WHERE</c>) por <see cref="DebtPaymentStatus.Pending"/>
    /// y <see cref="DebtPayment.ScheduledDate"/> dentro de <paramref name="range"/>
    /// — no trae el calendario completo a memoria para filtrar (F-008, slice 3, R-6).
    /// </summary>
    public async Task<IReadOnlyList<DebtPayment>> GetPendingByDateRangeAsync(DateRange range, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(range);

        return await _db.DebtPayments
            .AsNoTracking()
            .Where(payment => payment.Status == DebtPaymentStatus.Pending
                && payment.ScheduledDate >= range.Start
                && payment.ScheduledDate <= range.End)
            .OrderBy(payment => payment.ScheduledDate)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
