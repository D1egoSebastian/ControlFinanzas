using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Renders an already computed report DTO into a stream (F-018, D-1). Implementations live in
/// Infrastructure, are stateless, and never query data or read resources.
/// </summary>
public interface IReportWriter
{
    /// <summary>The format this writer produces.</summary>
    ReportFormat Format { get; }

    /// <summary>Writes the month-summary report; failures are returned as <c>ReportRenderFailed</c>.</summary>
    Task<Result> WriteMonthSummaryAsync(
        MonthSummaryReport report,
        ReportLabels labels,
        Stream destination,
        CancellationToken ct = default);

    /// <summary>Writes the expense-list report; failures are returned as <c>ReportRenderFailed</c>.</summary>
    Task<Result> WriteExpenseListAsync(
        ExpenseListReport report,
        ReportLabels labels,
        Stream destination,
        CancellationToken ct = default);
}
