namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>A whole imported table: header row plus data rows (F-020, DD-1).</summary>
/// <param name="Headers">Header texts.</param>
/// <param name="Rows">Data rows.</param>
/// <param name="Source">Facts about how the file was read.</param>
public sealed record RawTable(IReadOnlyList<string> Headers, IReadOnlyList<RawRow> Rows, TableSourceInfo Source);
