using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;

namespace ControlDeFinanzasIA.Infrastructure.Import;

/// <summary>
/// The registered <see cref="ITabularReader"/> (F-020, DD-10, D20): chooses the reader from the file CONTENT, never
/// from a file name or extension. A ZIP local-header magic selects the workbook reader, the OLE magic
/// (a legacy .xls) is rejected, and anything else is read as delimited text.
/// </summary>
public sealed class SniffingTabularReader : ITabularReader
{
    private static readonly byte[] _zipMagic = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] _oleMagic = [0xD0, 0xCF, 0x11, 0xE0];

    private readonly CsvTableReader _csv;
    private readonly ClosedXmlTableReader _xlsx;

    /// <summary>Initializes a new instance of the <see cref="SniffingTabularReader"/> class.</summary>
    /// <param name="csv">The text reader.</param>
    /// <param name="xlsx">The workbook reader.</param>
    public SniffingTabularReader(CsvTableReader csv, ClosedXmlTableReader xlsx)
    {
        ArgumentNullException.ThrowIfNull(csv);
        ArgumentNullException.ThrowIfNull(xlsx);
        _csv = csv;
        _xlsx = xlsx;
    }

    /// <inheritdoc />
    public Task<Result> ReadAsync(ReadOnlyMemory<byte> content, TabularReadOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        ReadOnlySpan<byte> head = content.Span;
        if (head.StartsWith(_zipMagic))
        {
            return _xlsx.ReadAsync(content, options, ct);
        }

        if (head.StartsWith(_oleMagic))
        {
            var details = new Dictionary<string, string[]> { [ImportErrorCodes.DetailReason] = [ImportErrorCodes.ReasonLegacyXls] };
            return Task.FromResult(Result.Failure(
                ImportErrorCodes.UnreadableFile,
                "The legacy .xls format is not supported. Save the file as .xlsx or .csv.",
                details));
        }

        return _csv.ReadAsync(content, options, ct);
    }
}
