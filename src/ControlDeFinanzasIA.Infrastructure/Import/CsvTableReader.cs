using System.Globalization;
using System.Text;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;

namespace ControlDeFinanzasIA.Infrastructure.Import;

/// <summary>
/// Reads CSV / delimited text into a <see cref="RawTable"/> (F-020, DD-9). Stateless and thread-safe. Decodes
/// strict UTF-8 first, then Windows-1252, and honours UTF-8 and UTF-16 byte order marks; sniffs the delimiter
/// unless one is given; enforces the import limits while parsing. File-level problems come back as
/// <see cref="Result.Failure(Error)"/> values, never as exceptions (cancellation aside).
/// </summary>
public sealed class CsvTableReader : ITabularReader
{
    private const int _sniffRecords = 20;
    private static readonly char[] _candidates = ['\t', ';', ','];

    /// <inheritdoc />
    public Task<Result> ReadAsync(ReadOnlyMemory<byte> content, TabularReadOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        // ReadOnlyMemory is safe to capture: it is a view over the caller's retained buffer.
        return Task.Run(() => Read(content.Span, options, ct), ct);
    }

    private static Result Read(ReadOnlySpan<byte> bytes, TabularReadOptions options, CancellationToken ct)
    {
        if (bytes.Length > ImportLimits.MaxFileBytes)
        {
            return Fail(ImportErrorCodes.TooLarge, "The file is larger than the allowed size.", ImportErrorCodes.ReasonFileTooLarge, null);
        }

        if (!TryDecode(bytes, out string text, out string encodingName, out bool fallback))
        {
            return Fail(ImportErrorCodes.UnreadableFile, "The file looks binary, not text.", ImportErrorCodes.ReasonBinary, null);
        }

        char delimiter = options.Delimiter ?? Sniff(text, ct);
        var records = new List<CsvRecord>();
        CsvParseOutcome outcome = CsvRecordParser.Parse(text, delimiter, ImportLimits.MaxDataRows + 1, true, records, ct);
        if (outcome.Failure != CsvParseFailure.None)
        {
            return FromFailure(outcome);
        }

        if (records.Count < 2)
        {
            return Result.Failure(ImportErrorCodes.NoRows, "The file has no data rows.");
        }

        var rows = new List<RawRow>(records.Count - 1);
        for (int i = 1; i < records.Count; i++)
        {
            CsvRecord record = records[i];
            var cells = new RawCell[record.Fields.Count];
            for (int c = 0; c < cells.Length; c++)
            {
                cells[c] = new RawCell(record.Fields[c], RawCellKind.Text);
            }

            rows.Add(new RawRow(record.Line, cells));
        }

        var source = new TableSourceInfo(
            ImportFileKind.Csv,
            encodingName,
            fallback,
            delimiter,
            null,
            text.Length <= ImportLimits.SampleTextChars ? text : text[..ImportLimits.SampleTextChars]);

        return Result.Success(new RawTable(records[0].Fields, rows, source));
    }

    private static bool TryDecode(ReadOnlySpan<byte> bytes, out string text, out string encodingName, out bool fallback)
    {
        text = string.Empty;
        fallback = false;

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encodingName = "UTF-16LE";
            return TryUtf16(bytes[2..], false, out text);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encodingName = "UTF-16BE";
            return TryUtf16(bytes[2..], true, out text);
        }

        bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        ReadOnlySpan<byte> body = bom ? bytes[3..] : bytes;
        encodingName = "UTF-8";

        if (!bom && body.Contains((byte)0))
        {
            return false;
        }

        try
        {
            text = new UTF8Encoding(false, true).GetString(body);
            return true;
        }
        catch (DecoderFallbackException)
        {
            Encoding latin = CodePagesEncodingProvider.Instance.GetEncoding(
                1252,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ReplacementFallback)!;
            text = latin.GetString(body);
            encodingName = "Windows-1252";
            fallback = true;
            return true;
        }
    }

    private static bool TryUtf16(ReadOnlySpan<byte> body, bool bigEndian, out string text)
    {
        try
        {
            text = new UnicodeEncoding(bigEndian, false, true).GetString(body);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    private static char Sniff(string text, CancellationToken ct)
    {
        char best = ',';
        (bool Wide, double Share, int Count) bestScore = (false, -1, 0);

        foreach (char candidate in _candidates)
        {
            var records = new List<CsvRecord>();
            _ = CsvRecordParser.Parse(text, candidate, _sniffRecords, false, records, ct);
            if (records.Count == 0)
            {
                continue;
            }

            int headerCount = records[0].Fields.Count;
            int matching = records.Count(r => r.Fields.Count == headerCount);
            (bool Wide, double Share, int Count) score = (Wide: headerCount >= 2, Share: (double)matching / records.Count, Count: headerCount);
            if (IsBetter(score, bestScore))
            {
                best = candidate;
                bestScore = score;
            }
        }

        // No candidate produced two or more columns: comma, and the mapping step shows the single column.
        return bestScore.Wide ? best : ',';
    }

    private static bool IsBetter((bool Wide, double Share, int Count) a, (bool Wide, double Share, int Count) b)
    {
        if (a.Wide != b.Wide)
        {
            return a.Wide;
        }

        if (a.Share != b.Share)
        {
            return a.Share > b.Share;
        }

        return a.Count > b.Count;
    }

    private static Result FromFailure(CsvParseOutcome outcome) => outcome.Failure switch
    {
        CsvParseFailure.UnterminatedQuote => Fail(ImportErrorCodes.UnreadableFile, "A quoted field is never closed.", ImportErrorCodes.ReasonMalformedCsv, outcome.Line),
        CsvParseFailure.StrayQuote => Fail(ImportErrorCodes.UnreadableFile, "A quote is followed by unexpected text.", ImportErrorCodes.ReasonMalformedCsv, outcome.Line),
        CsvParseFailure.TooManyColumns => Fail(ImportErrorCodes.TooLarge, "A row has too many columns.", ImportErrorCodes.ReasonTooManyColumns, outcome.Line),
        CsvParseFailure.FieldTooLong => Fail(ImportErrorCodes.TooLarge, "A field is too long.", ImportErrorCodes.ReasonFieldTooLong, outcome.Line),
        _ => Fail(ImportErrorCodes.TooLarge, "The file has too many rows.", ImportErrorCodes.ReasonTooManyRows, outcome.Line),
    };

    private static Result Fail(string code, string message, string reason, int? line)
    {
        var details = new Dictionary<string, string[]> { [ImportErrorCodes.DetailReason] = [reason] };
        if (line is not null)
        {
            details[ImportErrorCodes.DetailLine] = [line.Value.ToString(CultureInfo.InvariantCulture)];
        }

        return Result.Failure(code, message, details);
    }
}
