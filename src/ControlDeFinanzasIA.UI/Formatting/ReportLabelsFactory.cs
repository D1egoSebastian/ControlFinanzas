using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.UI.Formatting;

/// <summary>
/// Builds the <see cref="ReportLabels"/> handed to the report writers from the resx strings and
/// the current culture (F-018, R-10, D-7). Writers never read resources themselves.
/// </summary>
internal static class ReportLabelsFactory
{
    /// <summary>Creates labels for <see cref="CultureInfo.CurrentUICulture"/> with <see cref="CultureInfo.CurrentCulture"/> number/date formatting.</summary>
    public static ReportLabels Create() => new()
    {
        Culture = CultureInfo.CurrentCulture,
        ReportMonthSummaryTitle = Strings.ReportMonthSummaryTitle,
        ReportExpenseListTitle = Strings.ReportExpenseListTitle,
        ReportSummarySection = Strings.ReportSummarySection,
        ReportExpensesLabel = Strings.ReportExpensesLabel,
        ReportIncomesLabel = Strings.ReportIncomesLabel,
        ReportNetBalanceLabel = Strings.ReportNetBalanceLabel,
        ReportDeltaLabel = Strings.ReportDeltaLabel,
        ReportCategoriesSection = Strings.ReportCategoriesSection,
        ReportBudgetsSection = Strings.ReportBudgetsSection,
        ReportCategoryColumn = Strings.ReportCategoryColumn,
        ReportAmountDopColumn = Strings.ReportAmountDopColumn,
        ReportShareColumn = Strings.ReportShareColumn,
        ReportBudgetColumn = Strings.ReportBudgetColumn,
        ReportActualColumn = Strings.ReportActualColumn,
        ReportPercentColumn = Strings.ReportPercentColumn,
        ReportStatusColumn = Strings.ReportStatusColumn,
        ReportDateColumn = Strings.ReportDateColumn,
        ReportDescriptionColumn = Strings.ReportDescriptionColumn,
        ReportAmountColumn = Strings.ReportAmountColumn,
        ReportCurrencyColumn = Strings.ReportCurrencyColumn,
        ReportTotalLabel = Strings.ReportTotalLabel,
        ReportStatusOk = Strings.ReportStatusOk,
        ReportStatusNear = Strings.ReportStatusNear,
        ReportStatusExceeded = Strings.ReportStatusExceeded,
        ReportStatusIndeterminate = Strings.ReportStatusIndeterminate,
        ReportNoData = Strings.ReportNoData,
        ReportFiltersLabel = Strings.ReportFiltersLabel,
        ReportFilterAll = Strings.ReportFilterAll,
        ReportFilterPeriodFormat = Strings.ReportFilterPeriodFormat,
        ReportFilterCategoryLabel = Strings.ReportFilterCategoryLabel,
        ReportFilterTagsFormat = Strings.ReportFilterTagsFormat,
        ReportCountLabel = Strings.ReportCountLabel,
        ReportGeneratedOnFormat = Strings.ReportGeneratedOnFormat,
        ReportMissingRatesNote = Strings.ReportMissingRatesNote,
        ReportPageLabel = Strings.ReportPageLabel,
    };
}
