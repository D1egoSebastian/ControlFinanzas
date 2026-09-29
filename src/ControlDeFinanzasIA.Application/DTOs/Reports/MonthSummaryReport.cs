using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;

namespace ControlDeFinanzasIA.Application.DTOs.Reports;

/// <summary>Fully computed data of a month-summary report (F-018, D-2).</summary>
/// <param name="MonthAnchor">Any date inside the reported month.</param>
/// <param name="GeneratedOn">Generation date (<c>IClock.Today</c>).</param>
/// <param name="Summary">The Dashboard month totals, reused as-is.</param>
/// <param name="Categories">Unfolded category slices (no aggregated bucket).</param>
/// <param name="CategoriesTotalDop">Sum of <paramref name="Categories"/> in DOP.</param>
/// <param name="Budgets">The Dashboard budget-vs-actual result, reused as-is.</param>
/// <param name="Transactions">Every income and expense of the month.</param>
/// <param name="HasMissingRates">True when any source or line lacked an exchange rate.</param>
public sealed record MonthSummaryReport(
    DateOnly MonthAnchor,
    DateOnly GeneratedOn,
    MonthSummaryResult Summary,
    IReadOnlyList<CategorySlice> Categories,
    decimal CategoriesTotalDop,
    BudgetVsActualResult Budgets,
    IReadOnlyList<ReportTransactionLine> Transactions,
    bool HasMissingRates);
