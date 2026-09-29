using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.DTOs.Reports;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Tests.Integration.Reports;

/// <summary>Builders for report DTOs and labels used by the report writer tests (F-018).</summary>
internal static class ReportTestData
{
    public static ReportLabels Labels(string culture, string prefix) => new()
    {
        Culture = CultureInfo.GetCultureInfo(culture),
        ReportMonthSummaryTitle = prefix + "Month summary",
        ReportExpenseListTitle = prefix + "Expense list",
        ReportSummarySection = prefix + "Summary",
        ReportExpensesLabel = prefix + "Expenses",
        ReportIncomesLabel = prefix + "Incomes",
        ReportNetBalanceLabel = prefix + "Net balance",
        ReportDeltaLabel = prefix + "vs previous month",
        ReportCategoriesSection = prefix + "Categories",
        ReportBudgetsSection = prefix + "Budgets",
        ReportCategoryColumn = prefix + "Category",
        ReportAmountDopColumn = prefix + "Amount (DOP)",
        ReportShareColumn = prefix + "Share",
        ReportBudgetColumn = prefix + "Budget",
        ReportActualColumn = prefix + "Actual",
        ReportPercentColumn = prefix + "% used",
        ReportStatusColumn = prefix + "Status",
        ReportDateColumn = prefix + "Date",
        ReportDescriptionColumn = prefix + "Description",
        ReportAmountColumn = prefix + "Amount",
        ReportCurrencyColumn = prefix + "Currency",
        ReportTotalLabel = prefix + "Total",
        ReportStatusOk = prefix + "OK",
        ReportStatusNear = prefix + "Near limit",
        ReportStatusExceeded = prefix + "Exceeded",
        ReportStatusIndeterminate = prefix + "Not available",
        ReportNoData = prefix + "No data",
        ReportFiltersLabel = prefix + "Filters",
        ReportFilterAll = prefix + "All",
        ReportFilterPeriodFormat = prefix + "{0} to {1}",
        ReportFilterCategoryLabel = prefix + "Category",
        ReportFilterTagsFormat = prefix + "{0} tags",
        ReportCountLabel = prefix + "Count",
        ReportGeneratedOnFormat = prefix + "Generated on {0}",
        ReportMissingRatesNote = prefix + "Some amounts have no exchange rate and are excluded.",
        ReportPageLabel = prefix + "Page",
    };

    public static ReportLabels EsLabels() => Labels("es-DO", "ES ");

    public static ReportLabels EnLabels() => Labels("en", "EN ");

    public static MonthSummaryReport Report(
        bool hasMissingRates = false,
        bool empty = false,
        bool indeterminateBudget = false,
        string description = "Supermercado",
        int transactionCount = 3)
    {
        var anchor = new DateOnly(2026, 9, 1);
        if (empty)
        {
            return new MonthSummaryReport(
                anchor,
                new DateOnly(2026, 9, 29),
                new MonthSummaryResult(0m, 0m, 0m, 0m, 0m, null, null, false),
                [],
                0m,
                new BudgetVsActualResult(anchor, [], 0m, 0m, false),
                [],
                false);
        }

        var budgets = new List<BudgetVsActualLine>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Comida", 20000m, 12000m, 60m, BudgetStatus.Ok),
            new(Guid.NewGuid(), Guid.NewGuid(), "Transporte", 5000m, 5600m, 112m, BudgetStatus.Exceeded),
        };
        if (indeterminateBudget)
        {
            budgets.Add(new(Guid.NewGuid(), Guid.NewGuid(), "Viajes", null, null, null, null));
        }

        var lines = new List<ReportTransactionLine>();
        for (int i = 0; i < transactionCount; i++)
        {
            lines.Add(new ReportTransactionLine(
                anchor.AddDays(i % 28),
                i % 2 == 0 ? ReportTransactionKind.Expense : ReportTransactionKind.Income,
                description,
                "Comida",
                100m + i,
                i % 2 == 0 ? Currency.USD : Currency.DOP,
                hasMissingRates && i == 0 ? null : 6000m + i));
        }

        return new MonthSummaryReport(
            anchor,
            new DateOnly(2026, 9, 29),
            new MonthSummaryResult(17600m, 50000m, 32400m, 15000m, 48000m, 17.3m, null, hasMissingRates),
            [new CategorySlice(Guid.NewGuid(), "Comida", 12000m), new CategorySlice(Guid.NewGuid(), "Transporte", 5600m)],
            17600m,
            new BudgetVsActualResult(anchor, budgets, 25000m, 17600m, indeterminateBudget),
            lines,
            hasMissingRates);
    }
}
