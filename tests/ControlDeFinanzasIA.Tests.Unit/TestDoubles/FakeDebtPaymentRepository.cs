using System.Collections.ObjectModel;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Debts;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IDebtPaymentRepository"/> used in
/// unit tests. Stores payments in memory and exposes configurable
/// results to simulate validation/repository failures.
/// </summary>
public sealed class FakeDebtPaymentRepository : IDebtPaymentRepository
{
    private readonly List<DebtPayment> _payments = new();

    /// <summary>Gets the payments currently stored.</summary>
    public IReadOnlyList<DebtPayment> Payments => _payments;

    public void Seed(DebtPayment payment) => _payments.Add(payment);

    public void SeedRange(IEnumerable<DebtPayment> payments) => _payments.AddRange(payments);

    /// <inheritdoc />
    public Task<Result> AddAsync(DebtPayment payment, CancellationToken ct = default)
    {
        _payments.Add(payment);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(DebtPayment payment, CancellationToken ct = default)
    {
        int index = _payments.FindIndex(p => p.Id == payment.Id);
        if (index < 0)
        {
            return Task.FromResult(Result.Failure("NotFound", $"Debt payment {payment.Id} was not found."));
        }

        _payments[index] = payment;
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _payments.RemoveAll(p => p.Id == id);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<DebtPayment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_payments.FirstOrDefault(p => p.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<DebtPayment>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DebtPayment>>(new ReadOnlyCollection<DebtPayment>(_payments.ToList()));

    /// <inheritdoc />
    public Task<IReadOnlyList<DebtPayment>> GetByDebtIdAsync(Guid debtId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DebtPayment>>(_payments
            .Where(p => p.DebtId == debtId)
            .OrderBy(p => p.InstallmentNumber)
            .ToList());
}
