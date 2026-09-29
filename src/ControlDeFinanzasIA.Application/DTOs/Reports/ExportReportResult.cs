using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.Application.DTOs.Reports;

/// <summary>Success payload of an export use case (carried in <c>Result.Value</c>).</summary>
/// <param name="Format">Format that was written.</param>
/// <param name="TargetName">Display name of the file the user picked.</param>
/// <param name="RowCount">Number of transaction rows exported.</param>
/// <param name="HasMissingRates">True when the report flagged missing exchange rates.</param>
public sealed record ExportReportResult(
    ReportFormat Format,
    string TargetName,
    int RowCount,
    bool HasMissingRates);
