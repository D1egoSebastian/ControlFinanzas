namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>
/// One cell of an imported table (F-020, DD-1). The kind keeps spreadsheet numbers and dates out of the
/// decimal-style and date-format interpretation the user controls for text.
/// </summary>
/// <param name="Text">Number: invariant <c>0.####</c>; Date: <c>yyyy-MM-dd</c>; otherwise the cell text.</param>
/// <param name="Kind">How the source stored the value.</param>
public readonly record struct RawCell(string Text, RawCellKind Kind);
