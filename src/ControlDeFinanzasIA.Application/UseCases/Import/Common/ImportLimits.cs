namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>Named limits that bound an import (F-020, DD-2).</summary>
public static class ImportLimits
{
    /// <summary>Largest accepted file: 10 MiB.</summary>
    public const int MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>Most data rows in one import.</summary>
    public const int MaxDataRows = 50_000;

    /// <summary>Most columns in one file.</summary>
    public const int MaxColumns = 50;

    /// <summary>Longest single field, in characters.</summary>
    public const int MaxFieldChars = 32_768;

    /// <summary>Most uncompressed bytes a workbook may inflate to.</summary>
    public const long MaxXlsxUncompressedBytes = 100L * 1024 * 1024;

    /// <summary>Most entries in a workbook archive.</summary>
    public const int MaxXlsxEntries = 1_000;

    /// <summary>Most categories one import may create.</summary>
    public const int MaxNewCategories = 50;

    /// <summary>Longest category name, matching the column.</summary>
    public const int MaxCategoryNameChars = 100;

    /// <summary>Rows shown in each preview list.</summary>
    public const int PreviewRows = 50;

    /// <summary>Characters kept as the decoded sample.</summary>
    public const int SampleTextChars = 200;

    /// <summary>Largest amount the database column can hold (decimal(18,4)).</summary>
    public const decimal MaxAmount = 99_999_999_999_999.9999m;

    /// <summary>Most fraction digits kept.</summary>
    public const int MaxFractionDigits = 4;

    /// <summary>Colour given to categories created by an import.</summary>
    public const string NewCategoryColor = "#95A5A6";

    /// <summary>Id of the seeded "Otros" category. Duplicated from the seed because Application cannot see it.</summary>
    public static readonly Guid FallbackCategoryId = new("11111111-1111-1111-1111-000000000009");
}
