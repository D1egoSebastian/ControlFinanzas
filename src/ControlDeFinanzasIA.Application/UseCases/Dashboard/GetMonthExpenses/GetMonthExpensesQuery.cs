using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthExpenses;

/// <summary>
/// Lists the most recent expenses of one month for the Dashboard. It reuses the paged expense filter
/// (date range, most recent first) and only adds the category names and the DOP equivalent per row.
/// </summary>
public sealed class GetMonthExpensesQuery
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICurrencyConverter _currencyConverter;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetMonthExpensesQuery"/> class.
    /// </summary>
    /// <param name="expenseRepository">The expense repository.</param>
    /// <param name="categoryRepository">The category repository (names).</param>
    /// <param name="currencyConverter">The converter used for the DOP equivalent.</param>
    public GetMonthExpensesQuery(
        IExpenseRepository expenseRepository,
        ICategoryRepository categoryRepository,
        ICurrencyConverter currencyConverter)
    {
        ArgumentNullException.ThrowIfNull(expenseRepository);
        ArgumentNullException.ThrowIfNull(categoryRepository);
        ArgumentNullException.ThrowIfNull(currencyConverter);

        _expenseRepository = expenseRepository;
        _categoryRepository = categoryRepository;
        _currencyConverter = currencyConverter;
    }

    /// <summary>
    /// Runs the query for the month that contains <paramref name="monthAnchor"/>.
    /// </summary>
    /// <param name="monthAnchor">Any date inside the selected month.</param>
    /// <param name="limit">The maximum number of rows to return.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A <see cref="Result"/> holding a <see cref="MonthExpensesResult"/>, or a <c>RepositoryError</c> failure.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is not positive.</exception>
    public async Task<Result> ExecuteAsync(
        DateOnly monthAnchor,
        int limit = DashboardDefaults.MonthExpensesLimit,
        CancellationToken ct = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be greater than zero.");
        }

        DateRange range = MonthPeriod.Range(monthAnchor);

        try
        {
            ExpenseFilter filter = new()
            {
                Page = 1,
                PageSize = limit,
                DateFrom = range.Start,
                DateTo = range.End,
            };

            PagedResult<Expense> page = await _expenseRepository.GetByFilterAsync(filter, ct).ConfigureAwait(false);
            IReadOnlyList<Category> categories = await _categoryRepository.GetAllAsync(ct).ConfigureAwait(false);
            var names = categories.ToDictionary(c => c.Id, c => c.Name);

            List<MonthExpenseItem> items = [];
            bool hasMissingRates = false;

            foreach (Expense expense in page.Items)
            {
                Result converted = await _currencyConverter
                    .ConvertAsync(expense.Amount, Currency.DOP, ct)
                    .ConfigureAwait(false);

                decimal? amountDop = null;
                if (converted.IsSuccess)
                {
                    amountDop = ((Money)converted.Value!).Amount;
                }
                else
                {
                    hasMissingRates = true;
                }

                items.Add(new MonthExpenseItem(
                    expense.Id,
                    expense.Date,
                    expense.Description,
                    names.GetValueOrDefault(expense.CategoryId, string.Empty),
                    expense.Amount,
                    amountDop));
            }

            return Result.Success(new MonthExpensesResult(items, page.Total, hasMissingRates));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Failure("RepositoryError", $"Unable to load the month's expenses: {ex.Message}");
        }
    }
}
