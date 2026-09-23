using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Aggregates;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IExpenseRepository"/> used in unit tests.
/// Captures added expenses and allows configuring the result returned by
/// <see cref="AddAsync"/>.
/// </summary>
public sealed class FakeExpenseRepository : IExpenseRepository
{
    private readonly List<Expense> _addedExpenses = new();
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
    /// Gets or sets the expense returned by <see cref="GetByIdAsync"/>.
    /// When <c>null</c> the fake searches <see cref="AddedExpenses"/>.
    /// </summary>
    public Expense? GetByIdResultOverride { get; set; }

    /// <summary>
    /// Gets the expenses passed to <see cref="AddAsync"/>.
    /// </summary>
    public IReadOnlyList<Expense> AddedExpenses => _addedExpenses;

    /// <summary>
    /// Gets the result returned by <see cref="GetByFilterAsync"/>.
    /// </summary>
    public PagedResult<Expense> FilterResult { get; private set; } = new(new List<Expense>(), 0, 1, 50);

    /// <summary>
    /// Gets the number of times <see cref="GetByFilterAsync"/> has been called.
    /// </summary>
    public int GetByFilterCallCount { get; private set; }

    /// <summary>
    /// Gets the last filter passed to <see cref="GetByFilterAsync"/>.
    /// </summary>
    public ExpenseFilter? LastExpenseFilter { get; private set; }

    /// <summary>
    /// Configures the <see cref="Result"/> returned by the next
    /// <see cref="AddAsync"/> call.
    /// </summary>
    /// <param name="result">The result to return.</param>
    public void SetAddResult(Result result) => _addResult = result;

    /// <summary>
    /// Configures the <see cref="PagedResult{Expense}"/> returned by
    /// subsequent <see cref="GetByFilterAsync"/> calls.
    /// </summary>
    /// <param name="result">The paged result to return.</param>
    public void SetFilterResult(PagedResult<Expense> result) => FilterResult = result;

    /// <summary>
    /// Configures the <see cref="PagedResult{Expense}"/> returned by
    /// subsequent <see cref="GetByFilterAsync"/> calls.
    /// </summary>
    /// <param name="items">Items of the page.</param>
    /// <param name="total">Total number of items across all pages.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page.</param>
    public void SetFilterResult(IEnumerable<Expense> items, int total, int page = 1, int pageSize = 50)
        => FilterResult = new PagedResult<Expense>(items.ToList(), total, page, pageSize);

    /// <summary>
    /// Configures <see cref="GetByFilterAsync"/> to throw the given exception.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    public void SetThrowOnGetFilter(Exception exception) => _getByFilterException = exception;

    /// <inheritdoc />
    public Task<Result> AddAsync(Expense expense, CancellationToken ct = default)
    {
        _addedExpenses.Add(expense);
        return Task.FromResult(_addResult);
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(Expense expense, CancellationToken ct = default)
    {
        if (UpdateResultOverride is not null)
        {
            return Task.FromResult(UpdateResultOverride);
        }

        _addedExpenses.RemoveAll(e => e.Id == expense.Id);
        _addedExpenses.Add(expense);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        if (RemoveResultOverride is not null)
        {
            return Task.FromResult(RemoveResultOverride);
        }

        _addedExpenses.RemoveAll(e => e.Id == id);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (GetByIdResultOverride is not null)
        {
            return Task.FromResult<Expense?>(GetByIdResultOverride);
        }

        return Task.FromResult<Expense?>(_addedExpenses.FirstOrDefault(e => e.Id == id));
    }

    public Task<Expense?> GetByRecurringRuleAndPeriodAsync(Guid recurringRuleId, DateOnly period, CancellationToken ct = default)
        => Task.FromResult<Expense?>(_addedExpenses.FirstOrDefault(e => e.RecurringRuleId == recurringRuleId && e.RecurringPeriod == period));

    /// <inheritdoc />
    public Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Expense>>(Array.Empty<Expense>());

    /// <inheritdoc />
    public Task<PagedResult<Expense>> GetByFilterAsync(ExpenseFilter filter, CancellationToken ct = default)
    {
        GetByFilterCallCount++;
        LastExpenseFilter = filter;

        if (_getByFilterException is not null)
        {
            return Task.FromException<PagedResult<Expense>>(_getByFilterException);
        }

        return Task.FromResult(FilterResult);
    }

    /// <summary>
    /// Nombres de categoría usados por <see cref="GetCategoryTotalsAsync"/>
    /// para emular el join SQL a <c>Categories</c> que hace el repositorio
    /// real. Los tests lo pueblan para controlar la etiqueta sin una BD real.
    /// </summary>
    public Dictionary<Guid, string> CategoryNames { get; } = new();

    /// <summary>
    /// Number of times <see cref="GetCategoryTotalsAsync"/> was invoked.
    /// Used by <c>GetBudgetVsActualQuery</c> tests to assert the
    /// aggregation query is skipped when a month has no budgets (S-4).
    /// </summary>
    public int GetCategoryTotalsCallCount { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<CurrencyTotal>> GetPeriodTotalsAsync(DateRange period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        IReadOnlyList<CurrencyTotal> totals = _addedExpenses
            .Where(e => e.Date >= period.Start && e.Date <= period.End)
            .GroupBy(e => e.Amount.Currency)
            .Select(g => new CurrencyTotal(g.Key, g.Sum(x => x.Amount.Amount)))
            .ToList();

        return Task.FromResult(totals);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CategoryCurrencyTotal>> GetCategoryTotalsAsync(DateRange period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);
        GetCategoryTotalsCallCount++;

        IReadOnlyList<CategoryCurrencyTotal> totals = _addedExpenses
            .Where(e => e.Date >= period.Start && e.Date <= period.End)
            .GroupBy(e => new { e.CategoryId, e.Amount.Currency })
            .Select(g => new CategoryCurrencyTotal(
                g.Key.CategoryId,
                CategoryNames.TryGetValue(g.Key.CategoryId, out string? name) ? name : string.Empty,
                g.Key.Currency,
                g.Sum(x => x.Amount.Amount)))
            .ToList();

        return Task.FromResult(totals);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MonthCurrencyTotal>> GetMonthlyTotalsAsync(DateRange period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        IReadOnlyList<MonthCurrencyTotal> totals = _addedExpenses
            .Where(e => e.Date >= period.Start && e.Date <= period.End)
            .GroupBy(e => new { e.Date.Year, e.Date.Month, e.Amount.Currency })
            .Select(g => new MonthCurrencyTotal(g.Key.Year, g.Key.Month, g.Key.Currency, g.Sum(x => x.Amount.Amount)))
            .ToList();

        return Task.FromResult(totals);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DayCurrencyTotal>> GetDailyTotalsAsync(DateRange period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        IReadOnlyList<DayCurrencyTotal> totals = _addedExpenses
            .Where(e => e.Date >= period.Start && e.Date <= period.End)
            .GroupBy(e => new { e.Date, e.Amount.Currency })
            .Select(g => new DayCurrencyTotal(g.Key.Date, g.Key.Currency, g.Sum(x => x.Amount.Amount)))
            .ToList();

        return Task.FromResult(totals);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ExpenseAmountRow>> GetAmountRowsAsync(DateRange period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        IReadOnlyList<ExpenseAmountRow> rows = _addedExpenses
            .Where(e => e.Date >= period.Start && e.Date <= period.End)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Id)
            .Select(e => new ExpenseAmountRow(e.Id, e.CategoryId, e.Date, e.Amount.Amount, e.Amount.Currency, e.Description))
            .ToList();

        return Task.FromResult(rows);
    }
}
