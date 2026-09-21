using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Goals;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IGoalRepository"/> used in unit
/// tests (F-011, slice 2). Stores goals in memory and exposes
/// configurable results to simulate repository failures, plus call
/// counters so tests can assert an operation was (or was not) invoked.
/// </summary>
public sealed class FakeGoalRepository : IGoalRepository
{
    private readonly List<Goal> _goals = new();

    /// <summary>Gets the goals currently stored.</summary>
    public IReadOnlyList<Goal> Goals => _goals;

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

    /// <summary>True once <see cref="RemoveAsync"/> has been called.</summary>
    public bool RemoveAsyncCalled { get; private set; }

    public void Seed(Goal goal) => _goals.Add(goal);

    /// <inheritdoc />
    public Task<Result> AddAsync(Goal goal, CancellationToken ct = default)
    {
        AddAsyncCalled = true;
        if (AddResult.IsSuccess)
        {
            _goals.Add(goal);
        }

        return Task.FromResult(AddResult);
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(Goal goal, CancellationToken ct = default)
    {
        UpdateAsyncCalled = true;
        if (UpdateResult.IsSuccess)
        {
            int index = _goals.FindIndex(g => g.Id == goal.Id);
            if (index >= 0)
            {
                _goals[index] = goal;
            }
        }

        return Task.FromResult(UpdateResult);
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        RemoveAsyncCalled = true;
        if (RemoveResult.IsSuccess)
        {
            _goals.RemoveAll(g => g.Id == id);
        }

        return Task.FromResult(RemoveResult);
    }

    /// <inheritdoc />
    public Task<Goal?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_goals.FirstOrDefault(g => g.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<Goal>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Goal>>(_goals.ToList());
}
