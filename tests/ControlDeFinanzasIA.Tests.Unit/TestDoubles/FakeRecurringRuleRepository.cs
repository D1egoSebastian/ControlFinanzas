using System.Collections.ObjectModel;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Recurring;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IRecurringRuleRepository"/> used in
/// unit tests. Stores rules in memory and exposes a configurable
/// Add result to simulate validation/repository failures.
/// </summary>
public sealed class FakeRecurringRuleRepository : IRecurringRuleRepository
{
    private readonly List<RecurringRule> _rules = new();

    /// <summary>Gets the rules passed to <see cref="AddAsync"/>.</summary>
    public IReadOnlyList<RecurringRule> Added => _rules;

    /// <summary>
    /// Gets the result returned by <see cref="AddAsync"/>. Defaults to
    /// success; tests can set this to simulate failures.
    /// </summary>
    public Result AddResult { get; set; } = Result.Success(null!);

    /// <inheritdoc />
    public Task<Result> AddAsync(RecurringRule rule, CancellationToken ct = default)
    {
        _rules.Add(rule);
        return Task.FromResult(AddResult);
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(RecurringRule rule, CancellationToken ct = default)
        => Task.FromResult(Result.Success(null!));

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _rules.RemoveAll(r => r.Id == id);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<RecurringRule?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_rules.FirstOrDefault(r => r.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<RecurringRule>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RecurringRule>>(new ReadOnlyCollection<RecurringRule>(_rules.ToList()));
}
