namespace ControlDeFinanzasIA.Application.UseCases.Exports.Common;

/// <summary>Stable failure codes returned by the export use cases (F-018).</summary>
public static class ReportErrorCodes
{
    /// <summary>No registered writer supports the requested format.</summary>
    public const string WriterUnavailable = "ReportWriterUnavailable";

    /// <summary>Reading the data the report needs failed.</summary>
    public const string DataFailed = "ReportDataFailed";

    /// <summary>The writer failed or threw while rendering.</summary>
    public const string RenderFailed = "ReportRenderFailed";

    /// <summary>Opening or writing the destination failed.</summary>
    public const string SaveFailed = "ReportSaveFailed";
}
