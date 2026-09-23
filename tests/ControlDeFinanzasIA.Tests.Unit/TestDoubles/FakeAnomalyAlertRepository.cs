using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IAnomalyAlertRepository"/> used in
/// unit tests (F-014, slice S2). Stores alerts in memory and exposes a
/// configurable result for <see cref="ApplyReconciliationAsync"/> plus a
/// call counter so tests can assert the reconcile ran.
/// </summary>
public sealed class FakeAnomalyAlertRepository : IAnomalyAlertRepository
{
    private readonly List<AnomalyAlert> _alerts = new();

    /// <summary>Gets the alerts currently stored.</summary>
    public IReadOnlyList<AnomalyAlert> Alerts => _alerts;

    /// <summary>Gets or sets the result returned by <see cref="ApplyReconciliationAsync"/>.</summary>
    public Result ApplyReconciliationResult { get; set; } = Result.Success(null!);

    /// <summary>Number of times <see cref="ApplyReconciliationAsync"/> was invoked.</summary>
    public int ApplyReconciliationCallCount { get; private set; }

    public void Seed(AnomalyAlert alert) => _alerts.Add(alert);

    /// <inheritdoc />
    public Task<Result> AddAsync(AnomalyAlert alert, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _alerts.Add(alert);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(AnomalyAlert alert, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        int index = _alerts.FindIndex(a => a.Id == alert.Id);
        if (index < 0)
        {
            return Task.FromResult(Result.Failure("NotFound", $"AnomalyAlert {alert.Id} was not found."));
        }

        _alerts[index] = alert;
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        int removed = _alerts.RemoveAll(a => a.Id == id);
        return Task.FromResult(removed > 0
            ? Result.Success(null!)
            : Result.Failure("NotFound", $"AnomalyAlert {id} was not found."));
    }

    /// <inheritdoc />
    public Task<AnomalyAlert?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_alerts.FirstOrDefault(a => a.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<AnomalyAlert>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AnomalyAlert>>(_alerts.ToList());

    /// <inheritdoc />
    public Task<IReadOnlyList<AnomalyAlert>> GetByMonthAsync(DateRange month, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(month);
        IReadOnlyList<AnomalyAlert> matches = _alerts.Where(a => month.Contains(a.Date)).ToList();
        return Task.FromResult(matches);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AnomalyAlert>> GetByTransactionIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        IReadOnlyList<AnomalyAlert> matches = _alerts.Where(a => ids.Contains(a.TransactionId)).ToList();
        return Task.FromResult(matches);
    }

    /// <inheritdoc />
    public Task<Result> ApplyReconciliationAsync(AnomalyReconciliationPlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ApplyReconciliationCallCount++;

        if (!ApplyReconciliationResult.IsSuccess)
        {
            return Task.FromResult(ApplyReconciliationResult);
        }

        foreach (AnomalyAlert alert in plan.ToAdd)
        {
            _alerts.Add(alert);
        }

        foreach (AnomalyAlert alert in plan.ToUpdate)
        {
            int index = _alerts.FindIndex(a => a.Id == alert.Id);
            if (index >= 0)
            {
                _alerts[index] = alert;
            }
        }

        foreach (Guid id in plan.ToRemove)
        {
            _alerts.RemoveAll(a => a.Id == id);
        }

        return Task.FromResult(ApplyReconciliationResult);
    }
}
