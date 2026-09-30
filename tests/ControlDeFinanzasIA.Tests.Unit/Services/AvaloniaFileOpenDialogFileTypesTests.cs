using Avalonia.Platform.Storage;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.UI.Services;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Services;

/// <summary>
/// Proves the file-type list the Avalonia open dialog hands to the native picker (F-020, R-23, D-20).
/// The native dialog itself has no headless harness; the pure builder is what is asserted.
/// </summary>
public sealed class AvaloniaFileOpenDialogFileTypesTests
{
    private const string _allLabel = "All supported files";

    private static readonly FileTypeFilter _csv = new("CSV", "csv", "text/csv");
    private static readonly FileTypeFilter _txt = new("Text", "txt", "text/plain");
    private static readonly FileTypeFilter _xlsx = new("Excel", "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    private static readonly FileTypeFilter _zip = new("Backup", "zip", "application/zip");

    [Fact]
    public void SingleFilter_ProducesExactlyThatEntry_WithoutAnAllEntry()
    {
        IReadOnlyList<FilePickerFileType> types = AvaloniaFileOpenDialog.BuildFileTypes([_zip], _allLabel);

        FilePickerFileType only = Assert.Single(types);
        Assert.Equal("Backup", only.Name);
        Assert.Equal(["*.zip"], only.Patterns);
        Assert.Equal(["application/zip"], only.MimeTypes);
    }

    [Fact]
    public void SeveralFilters_PrependAnAllSupportedEntryHoldingEveryPattern()
    {
        IReadOnlyList<FilePickerFileType> types = AvaloniaFileOpenDialog.BuildFileTypes([_csv, _txt, _xlsx], _allLabel);

        Assert.Equal(4, types.Count);
        Assert.Equal(_allLabel, types[0].Name);
        Assert.Equal(["*.csv", "*.txt", "*.xlsx"], types[0].Patterns);
        Assert.Equal(["text/csv", "text/plain", _xlsx.MimeType], types[0].MimeTypes);
    }

    [Fact]
    public void SeveralFilters_KeepEachIndividualEntryInOrderAfterTheAllEntry()
    {
        IReadOnlyList<FilePickerFileType> types = AvaloniaFileOpenDialog.BuildFileTypes([_csv, _txt, _xlsx], _allLabel);

        Assert.Equal(["CSV", "Text", "Excel"], types.Skip(1).Select(t => t.Name));
        Assert.Equal(["*.csv"], types[1].Patterns);
        Assert.Equal(["*.txt"], types[2].Patterns);
        Assert.Equal(["*.xlsx"], types[3].Patterns);
        Assert.Equal(["text/csv"], types[1].MimeTypes);
    }

    [Fact]
    public void DuplicateExtensionsOrMimeTypes_AppearOnceInTheAllEntry()
    {
        var csvAgain = new FileTypeFilter("CSV again", "CSV", "text/csv");

        IReadOnlyList<FilePickerFileType> types = AvaloniaFileOpenDialog.BuildFileTypes([_csv, csvAgain], _allLabel);

        Assert.Equal(["*.csv"], types[0].Patterns);
        Assert.Equal(["text/csv"], types[0].MimeTypes);
        Assert.Equal(3, types.Count);
    }

    [Fact]
    public void EmptyFilterList_IsRejected() => Assert.Throws<ArgumentException>(() => AvaloniaFileOpenDialog.BuildFileTypes([], _allLabel));
}
