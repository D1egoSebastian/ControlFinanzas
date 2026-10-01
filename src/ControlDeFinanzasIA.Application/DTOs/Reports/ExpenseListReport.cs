namespace ControlDeFinanzasIA.Application.DTOs.Reports;

/// <summary>Fully computed data of an expense-list report (F-018, D-2).</summary>
/// <param name="GeneratedOn">Generation date (<c>IClock.Today</c>).</param>
/// <param name="Filter">Human-readable summary of the applied filter.</param>
/// <param name="Lines">Every matching expense across all pages.</param>
/// <param name="TotalDop">Sum of the non-null DOP amounts.</param>
/// <param name="HasMissingRates">True when any line lacked an exchange rate.</param>
public sealed record ExpenseListReport(
    DateOnly GeneratedOn,
    ReportFilterSummary Filter,
    IReadOnlyList<ReportTransactionLine> Lines,
    decimal TotalDop,
    bool HasMissingRates);

/// <summary>Filter fields echoed in an expense-list report.</summary>
/// <param name="DateFrom">Inclusive start date, if any.</param>
/// <param name="DateTo">Inclusive end date, if any.</param>
/// <param name="CategoryName">Category filter name, if any.</param>
/// <param name="TagCount">Number of tags in the filter.</param>
/// <param name="SearchText">Text searched inside the description, if any.</param>
public sealed record ReportFilterSummary(
    DateOnly? DateFrom,
    DateOnly? DateTo,
    string? CategoryName,
    int TagCount,
    string? SearchText = null);
