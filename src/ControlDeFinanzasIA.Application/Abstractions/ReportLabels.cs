using System.Globalization;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Localized strings and culture for a report, built by the UI layer so that report writers
/// never read resources or the ambient culture (F-018, R-10, D-7). Properties ending in
/// <c>Format</c> are composite format strings.
/// </summary>
public sealed record ReportLabels
{
    /// <summary>Culture used for every number, date and month name in the report.</summary>
    public required CultureInfo Culture { get; init; }

    public required string ReportMonthSummaryTitle { get; init; }

    public required string ReportExpenseListTitle { get; init; }

    public required string ReportSummarySection { get; init; }

    public required string ReportExpensesLabel { get; init; }

    public required string ReportIncomesLabel { get; init; }

    public required string ReportNetBalanceLabel { get; init; }

    public required string ReportDeltaLabel { get; init; }

    public required string ReportCategoriesSection { get; init; }

    public required string ReportBudgetsSection { get; init; }

    public required string ReportCategoryColumn { get; init; }

    public required string ReportAmountDopColumn { get; init; }

    public required string ReportShareColumn { get; init; }

    public required string ReportBudgetColumn { get; init; }

    public required string ReportActualColumn { get; init; }

    public required string ReportPercentColumn { get; init; }

    public required string ReportStatusColumn { get; init; }

    public required string ReportDateColumn { get; init; }

    public required string ReportDescriptionColumn { get; init; }

    public required string ReportAmountColumn { get; init; }

    public required string ReportCurrencyColumn { get; init; }

    public required string ReportTotalLabel { get; init; }

    public required string ReportStatusOk { get; init; }

    public required string ReportStatusNear { get; init; }

    public required string ReportStatusExceeded { get; init; }

    public required string ReportStatusIndeterminate { get; init; }

    public required string ReportNoData { get; init; }

    public required string ReportFiltersLabel { get; init; }

    public required string ReportFilterAll { get; init; }

    public required string ReportFilterPeriodFormat { get; init; }

    public required string ReportFilterCategoryLabel { get; init; }

    public required string ReportFilterTagsFormat { get; init; }

    public required string ReportCountLabel { get; init; }

    public required string ReportGeneratedOnFormat { get; init; }

    public required string ReportMissingRatesNote { get; init; }

    public required string ReportPageLabel { get; init; }
}
