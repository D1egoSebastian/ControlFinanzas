using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Infrastructure.Reports;
using ControlDeFinanzasIA.Tests.Integration.Reports;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>
/// XLSX bytes produced by the REAL <see cref="ClosedXmlReportWriter"/> for both report types (F-020, R-21):
/// the round-trip contract between the Phase 18 export and the Phase 20 import.
/// </summary>
internal static class XlsxFixtures
{
    public static readonly string[] DataHeaders = ["Date", "Type", "Description", "Category", "Amount", "Currency", "AmountDOP"];

    /// <summary>Transactions covering DOP and USD, an empty category, an income, a missing AmountDOP and 4 decimals.</summary>
    public static IReadOnlyList<ReportTransactionLine> Lines { get; } =
    [
        new ReportTransactionLine(new DateOnly(2026, 9, 1), ReportTransactionKind.Expense, "Supermercado", "Comida", 1234.5m, Currency.DOP, 1234.5m),
        new ReportTransactionLine(new DateOnly(2026, 9, 2), ReportTransactionKind.Expense, "Cafe, con leche", string.Empty, 4.5m, Currency.USD, 270m),
        new ReportTransactionLine(new DateOnly(2026, 9, 3), ReportTransactionKind.Income, "Salario", string.Empty, 50000m, Currency.DOP, 50000m),
        new ReportTransactionLine(new DateOnly(2026, 9, 4), ReportTransactionKind.Expense, "Precio raro", "Otros", 12.3456m, Currency.USD, null),
    ];

    public static async Task<byte[]> ExpenseListAsync()
    {
        IReadOnlyList<ReportTransactionLine> expenses = Lines.Where(l => l.Kind == ReportTransactionKind.Expense).ToList();
        var report = new ExpenseListReport(
            new DateOnly(2026, 9, 29),
            new ReportFilterSummary(null, null, null, 0),
            expenses,
            1504.5m,
            true);
        using var ms = new MemoryStream();
        Result result = await new ClosedXmlReportWriter().WriteExpenseListAsync(report, ReportTestData.EnLabels(), ms);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return ms.ToArray();
    }

    public static async Task<byte[]> MonthSummaryAsync()
    {
        var anchor = new DateOnly(2026, 9, 1);
        var report = new MonthSummaryReport(
            anchor,
            new DateOnly(2026, 9, 29),
            new MonthSummaryResult(1504.5m, 50000m, 48495.5m, 0m, 0m, null, null, true),
            [],
            0m,
            new BudgetVsActualResult(anchor, [], 0m, 0m, false),
            Lines,
            true);
        using var ms = new MemoryStream();
        Result result = await new ClosedXmlReportWriter().WriteMonthSummaryAsync(report, ReportTestData.EsLabels(), ms);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return ms.ToArray();
    }
}
