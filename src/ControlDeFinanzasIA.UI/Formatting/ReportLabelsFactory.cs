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
    /// <summary>Creates labels for <see cref="CultureInfo.CurrentUICulture"/> with <see cref="CultureInfo.CurrentCulture"/> number formatting and <see cref="CultureInfo.CurrentUICulture"/> month and day names.</summary>
    public static ReportLabels Create() => new()
    {
        Culture = ReportCulture(),
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
        ReportFilterTextFormat = Strings.ReportFilterTextFormat,
        ReportCountLabel = Strings.ReportCountLabel,
        ReportGeneratedOnFormat = Strings.ReportGeneratedOnFormat,
        ReportMissingRatesNote = Strings.ReportMissingRatesNote,
        ReportPageLabel = Strings.ReportPageLabel,
    };

    /// <summary>
    /// The regional format for numbers (separators, grouping) with the app language's calendar names, so a Spanish
    /// report never prints "October" because the operating system's region is English.
    /// </summary>
    private static CultureInfo ReportCulture()
    {
        CultureInfo regional = CultureInfo.CurrentCulture;
        CultureInfo ui = CultureInfo.CurrentUICulture;
        if (regional.DateTimeFormat.Equals(ui.DateTimeFormat) || string.Equals(regional.Name, ui.Name, StringComparison.Ordinal))
        {
            return regional;
        }

        var merged = (CultureInfo)regional.Clone();
        merged.DateTimeFormat = (DateTimeFormatInfo)ui.DateTimeFormat.Clone();
        return merged;
    }
}
