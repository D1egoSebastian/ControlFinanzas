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
    private Exception? _getAllException;

    /// <summary>Gets the rules passed to <see cref="AddAsync"/>.</summary>
    public IReadOnlyList<RecurringRule> Added => _rules;

    /// <summary>
    /// Gets the result returned by <see cref="AddAsync"/>. Defaults to
    /// success; tests can set this to simulate failures.
    /// </summary>
    public Result AddResult { get; set; } = Result.Success(null!);

    public void Seed(RecurringRule rule) => _rules.Add(rule);

    /// <summary>
    /// Configures <see cref="GetAllAsync"/> to throw the given exception
    /// (F-015, Slice S1 — isolates <c>BuildChatContextQuery</c>'s
    /// upcoming-payments section).
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    public void SetThrowOnGetAll(Exception exception) => _getAllException = exception;

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
    {
        GetAllCallCount++;
        return GetAllGate is not null ? GetAllGatedAsync() : GetAllCore();
    }

    /// <summary>Gets the number of times <see cref="GetAllAsync"/> was called.</summary>
    public int GetAllCallCount { get; private set; }

    /// <summary>
    /// Gets or sets a gate that holds <see cref="GetAllAsync"/> until completed (simulates a slow first load,
    /// F-021 S6). <c>null</c> by default.
    /// </summary>
    public TaskCompletionSource? GetAllGate { get; set; }

    private async Task<IReadOnlyList<RecurringRule>> GetAllGatedAsync()
    {
        await GetAllGate!.Task;
        return await GetAllCore();
    }

    private Task<IReadOnlyList<RecurringRule>> GetAllCore()
        => _getAllException is not null
            ? Task.FromException<IReadOnlyList<RecurringRule>>(_getAllException)
            : Task.FromResult<IReadOnlyList<RecurringRule>>(new ReadOnlyCollection<RecurringRule>(_rules.ToList()));
}
