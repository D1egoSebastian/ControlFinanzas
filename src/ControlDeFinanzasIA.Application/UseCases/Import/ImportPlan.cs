using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;

namespace ControlDeFinanzasIA.Application.UseCases.Import;

/// <summary>One row after validation, category resolution and duplicate classification (F-020, DD-11).</summary>
/// <param name="Parsed">The validated row, with its own reasons.</param>
/// <param name="Status">Valid, Duplicate or Rejected.</param>
/// <param name="CategoryId">The existing category id; <c>null</c> for a category to be created or for none.</param>
/// <param name="CategoryKey">The category component of the duplicate fingerprint.</param>
/// <param name="CategoryName">The category name shown in the preview.</param>
/// <param name="IsNewCategory">The category will be created by the import.</param>
/// <param name="PaymentMethodId">The matched payment method id, or <c>null</c>.</param>
/// <param name="Reasons">Every rejection and warning of the row, in a stable order.</param>
public sealed record PlannedRow(
    ParsedRow Parsed,
    ImportRowStatus Status,
    Guid? CategoryId,
    string CategoryKey,
    string? CategoryName,
    bool IsNewCategory,
    Guid? PaymentMethodId,
    IReadOnlyList<ImportReason> Reasons);

/// <summary>Output of <see cref="ImportPlanner"/>: the classified rows, the categories to create and the plan hash.</summary>
/// <param name="Rows">One entry per evaluated row, in file order.</param>
/// <param name="NewCategories">Categories the import will create, in first-seen order.</param>
/// <param name="PlanHash">Lowercase hex SHA-256 identifying this exact plan.</param>
public sealed record PlannedRows(
    IReadOnlyList<PlannedRow> Rows,
    IReadOnlyList<NewCategoryPlan> NewCategories,
    string PlanHash);

/// <summary>
/// Everything Preview shows and Commit acts on, derived from the retained bytes, the options and the database
/// snapshot (F-020, DD-11). Preview and Commit both obtain it from <see cref="ImportPipeline"/>.
/// </summary>
/// <param name="Table">The table read from the file.</param>
/// <param name="Mapping">The effective column mapping (the user's, or the automatic one).</param>
/// <param name="AutoMapping">The automatic mapping of the headers.</param>
/// <param name="MissingFields">Required fields the effective mapping leaves unmapped.</param>
/// <param name="ConflictingFields">Fields with several candidate headers (only when no user mapping is supplied).</param>
/// <param name="Decimal">Decimal-style evidence; <c>Resolved</c> is the style actually used.</param>
/// <param name="Dates">Date-format evidence versus the selected format.</param>
/// <param name="TotalRows">Number of non-blank data rows.</param>
/// <param name="Rows">The evaluated rows; empty while the mapping is incomplete.</param>
/// <param name="NewCategories">Categories the import will create.</param>
/// <param name="PlanHash">Lowercase hex SHA-256 identifying this exact plan.</param>
public sealed record ImportPlan(
    RawTable Table,
    ColumnMapping Mapping,
    HeaderMapping AutoMapping,
    IReadOnlyList<ImportField> MissingFields,
    IReadOnlyList<ImportField> ConflictingFields,
    DecimalStyleDetection Decimal,
    DateEvidence Dates,
    int TotalRows,
    IReadOnlyList<PlannedRow> Rows,
    IReadOnlyList<NewCategoryPlan> NewCategories,
    string PlanHash);
