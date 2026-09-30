using System.Collections.ObjectModel;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Debts;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IDebtRepository"/> used in unit
/// tests. Stores debts in memory and exposes configurable results to
/// simulate validation/repository failures.
/// </summary>
public sealed class FakeDebtRepository : IDebtRepository
{
    private readonly List<Debt> _debts = new();
    private Exception? _getAllException;

    /// <summary>Gets the debts currently stored.</summary>
    public IReadOnlyList<Debt> Debts => _debts;

    /// <summary>
    /// Gets or sets the result returned by <see cref="AddWithScheduleAsync"/>.
    /// Defaults to success; tests can set this to simulate failures.
    /// </summary>
    public Result AddWithScheduleResult { get; set; } = Result.Success(null!);

    /// <summary>True once <see cref="AddWithScheduleAsync"/> has been called.</summary>
    public bool AddWithScheduleCalled { get; private set; }

    public void Seed(Debt debt) => _debts.Add(debt);

    /// <summary>
    /// Configures <see cref="GetAllAsync"/> to throw the given exception
    /// (F-015, Slice S1 — isolates <c>BuildChatContextQuery</c>'s debts
    /// section).
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    public void SetThrowOnGetAll(Exception exception) => _getAllException = exception;

    /// <inheritdoc />
    public Task<Result> AddAsync(Debt debt, CancellationToken ct = default)
    {
        _debts.Add(debt);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Result> AddWithScheduleAsync(Debt debt, IReadOnlyList<DebtPayment> payments, CancellationToken ct = default)
    {
        AddWithScheduleCalled = true;
        if (AddWithScheduleResult.IsSuccess)
        {
            _debts.Add(debt);
        }

        return Task.FromResult(AddWithScheduleResult);
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(Debt debt, CancellationToken ct = default)
    {
        int index = _debts.FindIndex(d => d.Id == debt.Id);
        if (index < 0)
        {
            return Task.FromResult(Result.Failure("NotFound", $"Debt {debt.Id} was not found."));
        }

        _debts[index] = debt;
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _debts.RemoveAll(d => d.Id == id);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Debt?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_debts.FirstOrDefault(d => d.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<Debt>> GetAllAsync(CancellationToken ct = default)
        => GetAllGate is not null
            ? GetAllGatedAsync()
            : GetAllCore();

    /// <summary>
    /// Gets or sets a gate that holds <see cref="GetAllAsync"/> until completed (simulates a slow first load,
    /// F-021 S6). <c>null</c> by default.
    /// </summary>
    public TaskCompletionSource? GetAllGate { get; set; }

    private async Task<IReadOnlyList<Debt>> GetAllGatedAsync()
    {
        await GetAllGate!.Task;
        return await GetAllCore();
    }

    private Task<IReadOnlyList<Debt>> GetAllCore()
        => _getAllException is not null
            ? Task.FromException<IReadOnlyList<Debt>>(_getAllException)
            : Task.FromResult<IReadOnlyList<Debt>>(new ReadOnlyCollection<Debt>(_debts.ToList()));
}
