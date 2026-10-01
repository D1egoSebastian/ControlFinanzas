using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthExpenses;

/// <summary>
/// One row of the Dashboard's "expenses of the month" list.
/// </summary>
/// <param name="Id">The expense identifier.</param>
/// <param name="Date">The expense date.</param>
/// <param name="Description">The expense description.</param>
/// <param name="CategoryName">The category name, or an empty string when the category no longer exists.</param>
/// <param name="Amount">The amount in its original currency.</param>
/// <param name="AmountDop">The DOP equivalent, or <see langword="null"/> when no exchange rate is available (never 0).</param>
public sealed record MonthExpenseItem(
    Guid Id,
    DateOnly Date,
    string Description,
    string CategoryName,
    Money Amount,
    decimal? AmountDop);

/// <summary>
/// The latest expenses of a month plus the month's full expense count.
/// </summary>
/// <param name="Items">The listed expenses, most recent first, capped by the requested limit.</param>
/// <param name="TotalCount">How many expenses the month has in total (may exceed <see cref="Items"/>).</param>
/// <param name="HasMissingRates">Whether at least one listed expense could not be converted to DOP.</param>
public sealed record MonthExpensesResult(
    IReadOnlyList<MonthExpenseItem> Items,
    int TotalCount,
    bool HasMissingRates);
