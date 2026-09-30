using System.Globalization;
using System.IO.Compression;
using ClosedXML.Excel;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;

namespace ControlDeFinanzasIA.Infrastructure.Import;

/// <summary>
/// Reads an XLSX workbook into a typed <see cref="RawTable"/> (F-020, DD-10). ClosedXML types never leave this
/// class. The archive is inspected (entry count, declared and actually inflated sizes) BEFORE ClosedXML sees it;
/// formulas are never evaluated (the cached value is read); numbers and dates keep their spreadsheet kind so the
/// decimal and date options chosen by the user never re-interpret them. File-level problems are
/// <see cref="Result"/> failures, never exceptions (cancellation aside).
/// </summary>
public sealed class ClosedXmlTableReader : ITabularReader
{
    private const string _dataSheetName = "Data";
    private const int _copyBufferBytes = 81_920;

    private readonly Action? _workbookOpening;

    /// <summary>Initializes a new instance of the <see cref="ClosedXmlTableReader"/> class.</summary>
    public ClosedXmlTableReader()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ClosedXmlTableReader"/> class with a test seam.</summary>
    /// <param name="workbookOpening">Invoked right before ClosedXML opens the workbook (after the ZIP guard passed).</param>
    public ClosedXmlTableReader(Action workbookOpening)
    {
        _workbookOpening = workbookOpening;
    }

    /// <inheritdoc />
    public Task<Result> ReadAsync(ReadOnlyMemory<byte> content, TabularReadOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => Read(content, ct), ct);
    }

    private static string ErrorText(XLError error) => error switch
    {
        XLError.DivisionByZero => "#DIV/0!",
        XLError.NameNotRecognized => "#NAME?",
        XLError.NoValueAvailable => "#N/A",
        XLError.NullValue => "#NULL!",
        XLError.NumberInvalid => "#NUM!",
        XLError.CellReference => "#REF!",
        XLError.IncompatibleValue => "#VALUE!",
        _ => "#ERROR!",
    };

    private static RawCell Convert(IXLCell cell)
    {
        XLCellValue value = cell.HasFormula ? cell.CachedValue : cell.Value;
        switch (value.Type)
        {
            case XLDataType.Blank:
                return new RawCell(string.Empty, RawCellKind.Text);
            case XLDataType.Text:
                return new RawCell(value.GetText(), RawCellKind.Text);
            case XLDataType.Number:
                try
                {
                    decimal rounded = decimal.Round((decimal)value.GetNumber(), ImportLimits.MaxFractionDigits, MidpointRounding.AwayFromZero);
                    return new RawCell(rounded.ToString("0.####", CultureInfo.InvariantCulture), RawCellKind.Number);
                }
                catch (OverflowException)
                {
                    return new RawCell(string.Empty, RawCellKind.Error);
                }

            case XLDataType.DateTime:
                try
                {
                    return new RawCell(
                        DateOnly.FromDateTime(value.GetDateTime()).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        RawCellKind.Date);
                }
                catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidCastException or OverflowException)
                {
                    return new RawCell(string.Empty, RawCellKind.Error);
                }

            case XLDataType.Boolean:
                return new RawCell(value.GetBoolean() ? "TRUE" : "FALSE", RawCellKind.Text);
            case XLDataType.TimeSpan:
                return new RawCell(value.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture), RawCellKind.Text);
            case XLDataType.Error:
                return new RawCell(ErrorText(value.GetError()), RawCellKind.Error);
            default:
                return new RawCell(string.Empty, RawCellKind.Error);
        }
    }

    private static bool IsBlank(RawCell[] cells)
    {
        foreach (RawCell c in cells)
        {
            if (c.Text.Length > 0 || c.Kind == RawCellKind.Error)
            {
                return false;
            }
        }

        return true;
    }

    private static Result Fail(string code, string message, string reason)
    {
        var details = new Dictionary<string, string[]> { [ImportErrorCodes.DetailReason] = [reason] };
        return Result.Failure(code, message, details);
    }

    private static Result NotAWorkbook() =>
        Fail(ImportErrorCodes.UnreadableFile, "The file is not a readable Excel workbook.", ImportErrorCodes.ReasonNotAWorkbook);

    private static Result ZipTooLarge() =>
        Fail(ImportErrorCodes.TooLarge, "The workbook is too large once unpacked.", ImportErrorCodes.ReasonZipTooLarge);

    /// <summary>Returns a failure when the archive is hostile or unreadable, otherwise <see langword="null"/>.</summary>
    private static Result? GuardArchive(byte[] bytes, CancellationToken ct)
    {
        try
        {
            using var stream = new MemoryStream(bytes, false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, false);
            IReadOnlyList<ZipArchiveEntry> entries = archive.Entries;
            if (entries.Count > ImportLimits.MaxXlsxEntries)
            {
                return ZipTooLarge();
            }

            long declared = 0;
            foreach (ZipArchiveEntry entry in entries)
            {
                declared += entry.Length;
                if (declared > ImportLimits.MaxXlsxUncompressedBytes)
                {
                    return ZipTooLarge();
                }
            }

            // Declared sizes can lie: measure what actually inflates.
            long total = 0;
            byte[] buffer = new byte[_copyBufferBytes];
            foreach (ZipArchiveEntry entry in entries)
            {
                using Stream input = entry.Open();
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += read;
                    if (total > ImportLimits.MaxXlsxUncompressedBytes)
                    {
                        return ZipTooLarge();
                    }

                    ct.ThrowIfCancellationRequested();
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or ArgumentException or OverflowException)
        {
            return NotAWorkbook();
        }
    }

    private static IXLWorksheet PickSheet(XLWorkbook workbook)
    {
        foreach (IXLWorksheet sheet in workbook.Worksheets)
        {
            if (string.Equals(sheet.Name, _dataSheetName, StringComparison.OrdinalIgnoreCase))
            {
                return sheet;
            }
        }

        return workbook.Worksheets.First();
    }

    private static Result ReadSheet(XLWorkbook workbook, CancellationToken ct)
    {
        if (!workbook.Worksheets.Any())
        {
            return NotAWorkbook();
        }

        IXLWorksheet sheet = PickSheet(workbook);
        IXLRange? used = sheet.RangeUsed();
        if (used is null)
        {
            return Result.Failure(ImportErrorCodes.NoRows, "The file has no data rows.");
        }

        int firstColumn = used.RangeAddress.FirstAddress.ColumnNumber;
        int columnCount = used.RangeAddress.LastAddress.ColumnNumber - firstColumn + 1;
        if (columnCount > ImportLimits.MaxColumns)
        {
            return Fail(ImportErrorCodes.TooLarge, "The sheet has too many columns.", ImportErrorCodes.ReasonTooManyColumns);
        }

        List<string>? headers = null;
        var rows = new List<RawRow>();

        // Only rows that hold content are visited, so a sparse used range (a stray cell at row 1,000,000) costs
        // nothing per empty row. The collection itself is capped so a hostile sheet cannot grow it without bound.
        var contentRows = new SortedSet<int>();
        foreach (IXLCell stored in used.CellsUsed())
        {
            _ = contentRows.Add(stored.Address.RowNumber);
            if (contentRows.Count > 2 * (ImportLimits.MaxDataRows + 1))
            {
                return Fail(ImportErrorCodes.TooLarge, "The sheet has too many rows.", ImportErrorCodes.ReasonTooManyRows);
            }

            ct.ThrowIfCancellationRequested();
        }

        foreach (int rowNumber in contentRows)
        {
            ct.ThrowIfCancellationRequested();
            var cells = new RawCell[columnCount];
            for (int c = 0; c < columnCount; c++)
            {
                cells[c] = Convert(sheet.Cell(rowNumber, firstColumn + c));
                if (cells[c].Text.Length > ImportLimits.MaxFieldChars)
                {
                    return Fail(ImportErrorCodes.TooLarge, "A cell is too long.", ImportErrorCodes.ReasonFieldTooLong);
                }
            }

            if (IsBlank(cells))
            {
                continue;
            }

            if (headers is null)
            {
                headers = cells.Select(c => c.Text).ToList();
                continue;
            }

            if (rows.Count >= ImportLimits.MaxDataRows)
            {
                return Fail(ImportErrorCodes.TooLarge, "The sheet has too many rows.", ImportErrorCodes.ReasonTooManyRows);
            }

            rows.Add(new RawRow(rowNumber, cells));
        }

        if (headers is null || rows.Count == 0)
        {
            return Result.Failure(ImportErrorCodes.NoRows, "The file has no data rows.");
        }

        string sample = string.Join(" ", headers);
        var source = new TableSourceInfo(
            ImportFileKind.Xlsx,
            null,
            false,
            null,
            sheet.Name,
            sample.Length <= ImportLimits.SampleTextChars ? sample : sample[..ImportLimits.SampleTextChars]);

        return Result.Success(new RawTable(headers, rows, source));
    }

    private Result Read(ReadOnlyMemory<byte> content, CancellationToken ct)
    {
        if (content.Length > ImportLimits.MaxFileBytes)
        {
            return Fail(ImportErrorCodes.TooLarge, "The file is larger than the allowed size.", ImportErrorCodes.ReasonFileTooLarge);
        }

        byte[] bytes = content.ToArray();
        Result? guard = GuardArchive(bytes, ct);
        if (guard is not null)
        {
            return guard;
        }

        try
        {
            _workbookOpening?.Invoke();
            using var stream = new MemoryStream(bytes, false);
            using var workbook = new XLWorkbook(stream);
            return ReadSheet(workbook, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return NotAWorkbook();
        }
    }
}
