using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Goals;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IGoalContributionRepository"/> used
/// in unit tests (F-011, slice 2). Stores contributions in memory and
/// exposes configurable results plus call counters.
/// </summary>
public sealed class FakeGoalContributionRepository : IGoalContributionRepository
{
    private readonly List<GoalContribution> _contributions = new();

    /// <summary>Gets the contributions currently stored.</summary>
    public IReadOnlyList<GoalContribution> Contributions => _contributions;

    /// <summary>Gets or sets the result returned by <see cref="AddAsync"/>.</summary>
    public Result AddResult { get; set; } = Result.Success(null!);

    /// <summary>Gets or sets the result returned by <see cref="UpdateAsync"/>.</summary>
    public Result UpdateResult { get; set; } = Result.Success(null!);

    /// <summary>Gets or sets the result returned by <see cref="RemoveAsync"/>.</summary>
    public Result RemoveResult { get; set; } = Result.Success(null!);

    /// <summary>True once <see cref="AddAsync"/> has been called.</summary>
    public bool AddAsyncCalled { get; private set; }

    /// <summary>Number of times <see cref="GetByGoalIdAsync"/> was invoked.</summary>
    public int GetByGoalIdCallCount { get; private set; }

    public void Seed(GoalContribution contribution) => _contributions.Add(contribution);

    public void SeedRange(IEnumerable<GoalContribution> contributions) => _contributions.AddRange(contributions);

    /// <inheritdoc />
    public Task<Result> AddAsync(GoalContribution contribution, CancellationToken ct = default)
    {
        AddAsyncCalled = true;
        if (AddResult.IsSuccess)
        {
            _contributions.Add(contribution);
        }

        return Task.FromResult(AddResult);
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(GoalContribution contribution, CancellationToken ct = default)
    {
        if (UpdateResult.IsSuccess)
        {
            int index = _contributions.FindIndex(c => c.Id == contribution.Id);
            if (index >= 0)
            {
                _contributions[index] = contribution;
            }
        }

        return Task.FromResult(UpdateResult);
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        if (RemoveResult.IsSuccess)
        {
            _contributions.RemoveAll(c => c.Id == id);
        }

        return Task.FromResult(RemoveResult);
    }

    /// <inheritdoc />
    public Task<GoalContribution?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_contributions.FirstOrDefault(c => c.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<GoalContribution>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GoalContribution>>(_contributions.ToList());

    /// <inheritdoc />
    public Task<IReadOnlyList<GoalContribution>> GetByGoalIdAsync(Guid goalId, CancellationToken ct = default)
    {
        GetByGoalIdCallCount++;
        IReadOnlyList<GoalContribution> matches = _contributions
            .Where(c => c.GoalId == goalId)
            .OrderBy(c => c.Date)
            .ToList();
        return Task.FromResult(matches);
    }
}
