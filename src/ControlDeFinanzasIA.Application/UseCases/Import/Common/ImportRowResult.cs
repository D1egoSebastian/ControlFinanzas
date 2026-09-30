using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>Preview of one row after validation and planning (F-020, DD-1).</summary>
/// <param name="RowNumber">Source row number.</param>
/// <param name="Status">Outcome.</param>
/// <param name="Date">Parsed date.</param>
/// <param name="Type">Resolved type.</param>
/// <param name="Amount">Parsed absolute amount.</param>
/// <param name="Currency">Resolved currency.</param>
/// <param name="Description">Trimmed description.</param>
/// <param name="CategoryName">Resolved category name.</param>
/// <param name="IsNewCategory">The category will be created by the import.</param>
/// <param name="Reasons">Rejections and warnings.</param>
public sealed record ImportRowResult(
    int RowNumber,
    ImportRowStatus Status,
    DateOnly? Date,
    TransactionType? Type,
    decimal? Amount,
    Currency? Currency,
    string? Description,
    string? CategoryName,
    bool IsNewCategory,
    IReadOnlyList<ImportReason> Reasons);
