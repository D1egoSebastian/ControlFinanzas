namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>Facts about how a file was decoded, shown to the user in the preview (F-020, DD-1).</summary>
/// <param name="Kind">CSV or Excel.</param>
/// <param name="EncodingName">Detected text encoding (CSV only).</param>
/// <param name="EncodingFallback">The legacy Windows-1252 fallback was used.</param>
/// <param name="Delimiter">Field delimiter (CSV only).</param>
/// <param name="SheetName">Worksheet read (Excel only).</param>
/// <param name="SampleText">First 200 decoded characters (D21).</param>
public sealed record TableSourceInfo(
    ImportFileKind Kind,
    string? EncodingName,
    bool EncodingFallback,
    char? Delimiter,
    string? SheetName,
    string SampleText);
