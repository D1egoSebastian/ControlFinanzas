namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>Stable file-level failure codes and detail keys of the import (F-020, DD-2).</summary>
public static class ImportErrorCodes
{
    /// <summary>The file cannot be read as CSV or Excel.</summary>
    public const string UnreadableFile = "UnreadableFile";

    /// <summary>The file exceeds a size, row, column or field limit.</summary>
    public const string TooLarge = "TooLarge";

    /// <summary>The file has no data rows.</summary>
    public const string NoRows = "NoRows";

    /// <summary>The database or the file changed between preview and commit.</summary>
    public const string PreviewStale = "PreviewStale";

    /// <summary>Reading the catalogs failed.</summary>
    public const string LookupFailed = "LookupFailed";

    /// <summary>Writing the batch failed; nothing was stored.</summary>
    public const string WriteFailed = "WriteFailed";

    /// <summary>The column mapping is incomplete or invalid.</summary>
    public const string MappingIncomplete = "MappingIncomplete";

    /// <summary>Detail key: a sub-reason refining the code.</summary>
    public const string DetailReason = "reason";

    /// <summary>Detail key: the physical line where the problem starts.</summary>
    public const string DetailLine = "line";

    /// <summary>Reason: malformed quoting.</summary>
    public const string ReasonMalformedCsv = "malformedCsv";

    /// <summary>Reason: binary content.</summary>
    public const string ReasonBinary = "binary";

    /// <summary>Reason: legacy .xls workbook.</summary>
    public const string ReasonLegacyXls = "legacyXls";

    /// <summary>Reason: not a readable workbook.</summary>
    public const string ReasonNotAWorkbook = "notAWorkbook";

    /// <summary>Reason: file above the size cap.</summary>
    public const string ReasonFileTooLarge = "fileTooLarge";

    /// <summary>Reason: too many data rows.</summary>
    public const string ReasonTooManyRows = "tooManyRows";

    /// <summary>Reason: too many columns.</summary>
    public const string ReasonTooManyColumns = "tooManyColumns";

    /// <summary>Reason: a single field is too long.</summary>
    public const string ReasonFieldTooLong = "fieldTooLong";

    /// <summary>Reason: the workbook inflates beyond the cap.</summary>
    public const string ReasonZipTooLarge = "zipTooLarge";
}
