namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>A data row of an imported table (F-020, DD-1).</summary>
/// <param name="SourceRowNumber">One-based CSV line or Excel row number.</param>
/// <param name="Cells">The cells, in header order.</param>
public sealed record RawRow(int SourceRowNumber, IReadOnlyList<RawCell> Cells);
