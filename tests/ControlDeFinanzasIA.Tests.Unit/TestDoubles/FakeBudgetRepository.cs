using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Budgets;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IBudgetRepository"/> used in unit
/// tests (F-010, slice 2). Stores budgets in memory and exposes
/// configurable results to simulate repository failures, plus call
/// counters so tests can assert a query was (or was not) invoked.
/// </summary>
public sealed class FakeBudgetRepository : IBudgetRepository
{
    private readonly List<Budget> _budgets = new();
    private Exception? _getByMonthException;

    /// <summary>Gets the budgets currently stored.</summary>
    public IReadOnlyList<Budget> Budgets => _budgets;

    /// <summary>Gets or sets the result returned by <see cref="AddAsync"/>.</summary>
    public Result AddResult { get; set; } = Result.Success(null!);

    /// <summary>Gets or sets the result returned by <see cref="UpdateAsync"/>.</summary>
    public Result UpdateResult { get; set; } = Result.Success(null!);

    /// <summary>Gets or sets the result returned by <see cref="RemoveAsync"/>.</summary>
    public Result RemoveResult { get; set; } = Result.Success(null!);

    /// <summary>True once <see cref="AddAsync"/> has been called.</summary>
    public bool AddAsyncCalled { get; private set; }

    /// <summary>True once <see cref="UpdateAsync"/> has been called.</summary>
    public bool UpdateAsyncCalled { get; private set; }

    /// <summary>Number of times <see cref="GetByMonthAsync"/> was invoked.</summary>
    public int GetByMonthCallCount { get; private set; }

    public void Seed(Budget budget) => _budgets.Add(budget);

    /// <summary>
    /// Configures <see cref="GetByMonthAsync"/> to throw the given
    /// exception (F-015, Slice S1 — isolates
    /// <c>BuildChatContextQuery</c>'s budgets section).
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    public void SetThrowOnGetByMonth(Exception exception) => _getByMonthException = exception;

    /// <inheritdoc />
    public Task<Result> AddAsync(Budget budget, CancellationToken ct = default)
    {
        AddAsyncCalled = true;
        if (AddResult.IsSuccess)
        {
            _budgets.Add(budget);
        }

        return Task.FromResult(AddResult);
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(Budget budget, CancellationToken ct = default)
    {
        UpdateAsyncCalled = true;
        if (UpdateResult.IsSuccess)
        {
            int index = _budgets.FindIndex(b => b.Id == budget.Id);
            if (index >= 0)
            {
                _budgets[index] = budget;
            }
        }

        return Task.FromResult(UpdateResult);
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        if (RemoveResult.IsSuccess)
        {
            _budgets.RemoveAll(b => b.Id == id);
        }

        return Task.FromResult(RemoveResult);
    }

    /// <inheritdoc />
    public Task<Budget?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_budgets.FirstOrDefault(b => b.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<Budget>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Budget>>(_budgets.ToList());

    /// <inheritdoc />
    public Task<IReadOnlyList<Budget>> GetByMonthAsync(int year, int month, CancellationToken ct = default)
    {
        GetByMonthCallCount++;

        if (_getByMonthException is not null)
        {
            return Task.FromException<IReadOnlyList<Budget>>(_getByMonthException);
        }

        IReadOnlyList<Budget> matches = _budgets
            .Where(b => b.Year == year && b.Month == month)
            .OrderBy(b => b.CategoryId)
            .ToList();
        return Task.FromResult(matches);
    }

    /// <inheritdoc />
    public Task<Budget?> GetByCategoryAndMonthAsync(Guid categoryId, int year, int month, CancellationToken ct = default)
        => Task.FromResult(_budgets.FirstOrDefault(
            b => b.CategoryId == categoryId && b.Year == year && b.Month == month));
}
