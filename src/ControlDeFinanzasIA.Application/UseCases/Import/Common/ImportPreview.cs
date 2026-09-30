using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>How many rows carry a given reason.</summary>
/// <param name="Reason">The reason.</param>
/// <param name="Count">Rows carrying it.</param>
public sealed record ImportReasonCount(ImportReason Reason, int Count);

/// <summary>What the user sees before confirming an import (F-020, DD-1).</summary>
/// <param name="Source">How the file was read.</param>
/// <param name="Headers">Header texts.</param>
/// <param name="Mapping">Mapping in effect.</param>
/// <param name="MissingFields">Required fields without a column.</param>
/// <param name="ConflictingFields">Fields with several candidate columns.</param>
/// <param name="Decimal">Decimal-style detection result.</param>
/// <param name="Dates">Date-format evidence.</param>
/// <param name="TotalRows">Data rows read.</param>
/// <param name="ToImportCount">Rows that will be imported.</param>
/// <param name="DuplicateCount">Duplicate rows.</param>
/// <param name="RejectedCount">Rejected rows.</param>
/// <param name="WarningCount">Rows with at least one warning.</param>
/// <param name="SampleRows">First rows.</param>
/// <param name="ProblemRows">First rejected, duplicate or warning rows.</param>
/// <param name="ReasonCounts">Rows per reason.</param>
/// <param name="CategoriesToCreate">Names of the categories the import would create.</param>
/// <param name="PlanHash">Fingerprint of the plan the user is looking at.</param>
public sealed record ImportPreview(
    TableSourceInfo Source,
    IReadOnlyList<string> Headers,
    ColumnMapping Mapping,
    IReadOnlyList<ImportField> MissingFields,
    IReadOnlyList<ImportField> ConflictingFields,
    DecimalStyleDetection Decimal,
    DateEvidence Dates,
    int TotalRows,
    int ToImportCount,
    int DuplicateCount,
    int RejectedCount,
    int WarningCount,
    IReadOnlyList<ImportRowResult> SampleRows,
    IReadOnlyList<ImportRowResult> ProblemRows,
    IReadOnlyList<ImportReasonCount> ReasonCounts,
    IReadOnlyList<string> CategoriesToCreate,
    string PlanHash);
