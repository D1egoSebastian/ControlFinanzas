using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.Application.UseCases.Exports.ExportExpenseListReport;

/// <summary>Input of <see cref="ExportExpenseListReportUseCase"/>.</summary>
/// <param name="Filter">
/// Filter of the visible list (category, date range, tags). <c>Page</c> and <c>PageSize</c> are
/// ignored: the use case reads every page.
/// </param>
/// <param name="Format">Output format.</param>
/// <param name="Labels">Localized strings and culture, passed to the writer unchanged.</param>
public sealed record ExportExpenseListReportCommand(
    ExpenseFilter Filter,
    ReportFormat Format,
    ReportLabels Labels);
