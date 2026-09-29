using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.Application.UseCases.Exports.ExportMonthSummaryReport;

/// <summary>Input of <see cref="ExportMonthSummaryReportUseCase"/>.</summary>
/// <param name="MonthAnchor">Any date inside the month to export.</param>
/// <param name="Format">Output format.</param>
/// <param name="Labels">Localized strings and culture, passed to the writer unchanged.</param>
public sealed record ExportMonthSummaryReportCommand(
    DateOnly MonthAnchor,
    ReportFormat Format,
    ReportLabels Labels);
