using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Aggregates;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IIncomeRepository"/> used in unit tests.
/// Captures added incomes and allows configuring the result returned by
/// <see cref="AddAsync"/>.
/// </summary>
public sealed class FakeIncomeRepository : IIncomeRepository
{
    private readonly List<Income> _addedIncomes = new();
    private readonly List<int> _requestedPages = new();
    private Result _addResult = Result.Success(null!);
    private Exception? _getByFilterException;

    /// <summary>
    /// Gets or sets the result returned by <see cref="UpdateAsync"/>.
    /// When <c>null</c> the fake performs an in-memory update.
    /// </summary>
    public Result? UpdateResultOverride { get; set; }

    /// <summary>
    /// Gets or sets the result returned by <see cref="RemoveAsync"/>.
    /// When <c>null</c> the fake performs an in-memory removal.
    /// </summary>
    public Result? RemoveResultOverride { get; set; }

    /// <summary>
    /// Gets or sets the income returned by <see cref="GetByIdAsync"/>.
    /// When <c>null</c> the fake searches <see cref="AddedIncomes"/>.
    /// </summary>
    public Income? GetByIdResultOverride { get; set; }

    /// <summary>
    /// Gets the incomes passed to <see cref="AddAsync"/>.
    /// </summary>
    public IReadOnlyList<Income> AddedIncomes => _addedIncomes;

    /// <summary>
    /// Gets the result returned by <see cref="GetByFilterAsync"/>.
    /// </summary>
    public PagedResult<Income> FilterResult { get; private set; } = new(new List<Income>(), 0, 1, 50);

    /// <summary>
    /// When <c>true</c>, <see cref="GetByFilterAsync"/> pages the in-memory added items
    /// (date range and page/page size honored) instead of returning <see cref="FilterResult"/>
    /// on every call. Additive mode for export tests (F-018, D-5); off by default.
    /// </summary>
    public bool PageAddedItems { get; set; }

    /// <summary>
    /// Gets the page numbers requested through <see cref="GetByFilterAsync"/>, in call order.
    /// </summary>
    public IReadOnlyList<int> RequestedPages => _requestedPages;

    /// <summary>
    /// Gets the number of times <see cref="GetByFilterAsync"/> has been called.
    /// </summary>
    public int GetByFilterCallCount { get; private set; }

    /// <summary>
    /// Gets the last filter passed to <see cref="GetByFilterAsync"/>.
    /// </summary>
    public IncomeFilter? LastIncomeFilter { get; private set; }

    /// <summary>
    /// Configures the <see cref="Result"/> returned by the next
    /// <see cref="AddAsync"/> call.
    /// </summary>
    /// <param name="result">The result to return.</param>
    public void SetAddResult(Result result) => _addResult = result;

    /// <summary>
    /// Configures the <see cref="PagedResult{Income}"/> returned by
    /// subsequent <see cref="GetByFilterAsync"/> calls.
    /// </summary>
    /// <param name="result">The paged result to return.</param>
    public void SetFilterResult(PagedResult<Income> result) => FilterResult = result;

    /// <summary>
    /// Configures the <see cref="PagedResult{Income}"/> returned by
    /// subsequent <see cref="GetByFilterAsync"/> calls.
    /// </summary>
    /// <param name="items">Items of the page.</param>
    /// <param name="total">Total number of items across all pages.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page.</param>
    public void SetFilterResult(IEnumerable<Income> items, int total, int page = 1, int pageSize = 50)
        => FilterResult = new PagedResult<Income>(items.ToList(), total, page, pageSize);

    /// <summary>
    /// Configures <see cref="GetByFilterAsync"/> to throw the given exception.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    public void SetThrowOnGetFilter(Exception exception) => _getByFilterException = exception;

    /// <inheritdoc />
    public Task<Result> AddAsync(Income income, CancellationToken ct = default)
    {
        _addedIncomes.Add(income);
        return Task.FromResult(_addResult);
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(Income income, CancellationToken ct = default)
    {
        if (UpdateResultOverride is not null)
        {
            return Task.FromResult(UpdateResultOverride);
        }

        _addedIncomes.RemoveAll(i => i.Id == income.Id);
        _addedIncomes.Add(income);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        if (RemoveResultOverride is not null)
        {
            return Task.FromResult(RemoveResultOverride);
        }

        _addedIncomes.RemoveAll(i => i.Id == id);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (GetByIdResultOverride is not null)
        {
            return Task.FromResult<Income?>(GetByIdResultOverride);
        }

        return Task.FromResult<Income?>(_addedIncomes.FirstOrDefault(i => i.Id == id));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Income>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Income>>(Array.Empty<Income>());

    /// <inheritdoc />
    public Task<PagedResult<Income>> GetByFilterAsync(IncomeFilter filter, CancellationToken ct = default)
    {
        if (GetByFilterGate is not null)
        {
            return GetByFilterGatedAsync(filter, ct);
        }

        return GetByFilterCore(filter);
    }

    /// <summary>
    /// Gets or sets a gate that holds every <see cref="GetByFilterAsync"/> call until it completes
    /// (simulates a slow first load). <c>null</c> by default.
    /// </summary>
    public TaskCompletionSource? GetByFilterGate { get; set; }

    private async Task<PagedResult<Income>> GetByFilterGatedAsync(IncomeFilter filter, CancellationToken ct)
    {
        await GetByFilterGate!.Task;
        return await GetByFilterCore(filter);
    }

    private Task<PagedResult<Income>> GetByFilterCore(IncomeFilter filter)
    {
        GetByFilterCallCount++;
        LastIncomeFilter = filter;

        if (_getByFilterException is not null)
        {
            return Task.FromException<PagedResult<Income>>(_getByFilterException);
        }

        _requestedPages.Add(filter.Page);

        if (PageAddedItems)
        {
            var matching = _addedIncomes
                .Where(x => (filter.DateFrom is null || x.Date >= filter.DateFrom)
                    && (filter.DateTo is null || x.Date <= filter.DateTo))
                .ToList();
            var pageItems = matching.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
            return Task.FromResult(new PagedResult<Income>(pageItems, matching.Count, filter.Page, filter.PageSize));
        }

        return Task.FromResult(FilterResult);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CurrencyTotal>> GetPeriodTotalsAsync(DateRange period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        IReadOnlyList<CurrencyTotal> totals = _addedIncomes
            .Where(i => i.Date >= period.Start && i.Date <= period.End)
            .GroupBy(i => i.Amount.Currency)
            .Select(g => new CurrencyTotal(g.Key, g.Sum(x => x.Amount.Amount)))
            .ToList();

        return Task.FromResult(totals);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MonthCurrencyTotal>> GetMonthlyTotalsAsync(DateRange period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        IReadOnlyList<MonthCurrencyTotal> totals = _addedIncomes
            .Where(i => i.Date >= period.Start && i.Date <= period.End)
            .GroupBy(i => new { i.Date.Year, i.Date.Month, i.Amount.Currency })
            .Select(g => new MonthCurrencyTotal(g.Key.Year, g.Key.Month, g.Key.Currency, g.Sum(x => x.Amount.Amount)))
            .ToList();

        return Task.FromResult(totals);
    }
}
