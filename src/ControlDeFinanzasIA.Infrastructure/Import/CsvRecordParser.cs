using System.Text;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;

namespace ControlDeFinanzasIA.Infrastructure.Import;

/// <summary>Why a CSV pass stopped before the end of the text.</summary>
internal enum CsvParseFailure
{
    /// <summary>The pass reached the end (or the sniff cap) without a problem.</summary>
    None,

    /// <summary>A quoted field is never closed.</summary>
    UnterminatedQuote,

    /// <summary>A closing quote is followed by something other than a delimiter or a line end.</summary>
    StrayQuote,

    /// <summary>A record has more fields than <see cref="ImportLimits.MaxColumns"/>.</summary>
    TooManyColumns,

    /// <summary>A field is longer than <see cref="ImportLimits.MaxFieldChars"/>.</summary>
    FieldTooLong,

    /// <summary>More non-blank records than the caller allows.</summary>
    TooManyRecords,
}

/// <summary>One non-blank CSV record and the physical line where it started.</summary>
/// <param name="Line">One-based physical start line.</param>
/// <param name="Fields">The raw field values.</param>
internal sealed record CsvRecord(int Line, List<string> Fields);

/// <summary>Outcome of a pass: the failure (if any) and the line it is attributed to.</summary>
/// <param name="Failure">What stopped the pass.</param>
/// <param name="Line">Physical start line of the offending record.</param>
internal readonly record struct CsvParseOutcome(CsvParseFailure Failure, int Line);

/// <summary>
/// RFC-4180 four-state machine (F-020, DD-9). Stateless; every limit is checked while characters are consumed so
/// an oversized input stops at the offending record.
/// </summary>
internal static class CsvRecordParser
{
    private enum State
    {
        FieldStart,
        Unquoted,
        Quoted,
        QuoteSeen,
    }

    /// <summary>Parses <paramref name="text"/> into <paramref name="records"/>.</summary>
    /// <param name="text">Decoded text without BOM.</param>
    /// <param name="delimiter">Field delimiter.</param>
    /// <param name="recordCap">Number of non-blank records that triggers <paramref name="capIsFailure"/>.</param>
    /// <param name="capIsFailure">When <see langword="true"/> exceeding the cap fails; otherwise reaching it stops quietly.</param>
    /// <param name="records">Receives the non-blank records read so far.</param>
    /// <param name="ct">Cancellation token, checked once per record.</param>
    /// <returns>The outcome.</returns>
    public static CsvParseOutcome Parse(
        string text,
        char delimiter,
        int recordCap,
        bool capIsFailure,
        List<CsvRecord> records,
        CancellationToken ct)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        State state = State.FieldStart;
        int line = 1;
        int recordLine = 1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (state == State.Quoted)
            {
                if (c == '"')
                {
                    state = State.QuoteSeen;
                    continue;
                }

                sb.Append(c);
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    sb.Append('\n');
                    i++;
                    line++;
                }
                else if (c == '\r' || c == '\n')
                {
                    line++;
                }

                if (sb.Length > ImportLimits.MaxFieldChars)
                {
                    return new CsvParseOutcome(CsvParseFailure.FieldTooLong, recordLine);
                }

                continue;
            }

            bool isEol = c == '\r' || c == '\n';
            if (isEol || c == delimiter)
            {
                fields.Add(sb.ToString());
                sb.Clear();
                if (fields.Count > ImportLimits.MaxColumns)
                {
                    return new CsvParseOutcome(CsvParseFailure.TooManyColumns, recordLine);
                }

                state = State.FieldStart;
                if (!isEol)
                {
                    continue;
                }

                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                line++;
                CsvParseOutcome ended = EndRecord(fields, recordLine, recordCap, capIsFailure, records, ct, out bool stop);
                if (ended.Failure != CsvParseFailure.None || stop)
                {
                    return ended;
                }

                recordLine = line;
                continue;
            }

            switch (state)
            {
                case State.FieldStart:
                    if (c == '"')
                    {
                        state = State.Quoted;
                    }
                    else
                    {
                        sb.Append(c);
                        state = State.Unquoted;
                    }

                    break;
                case State.Unquoted:
                    sb.Append(c);
                    break;
                case State.QuoteSeen:
                    if (c == '"')
                    {
                        sb.Append('"');
                        state = State.Quoted;
                    }
                    else
                    {
                        return new CsvParseOutcome(CsvParseFailure.StrayQuote, recordLine);
                    }

                    break;
                default:
                    break;
            }

            if (sb.Length > ImportLimits.MaxFieldChars)
            {
                return new CsvParseOutcome(CsvParseFailure.FieldTooLong, recordLine);
            }
        }

        if (state == State.Quoted)
        {
            return new CsvParseOutcome(CsvParseFailure.UnterminatedQuote, recordLine);
        }

        if (state != State.FieldStart || fields.Count > 0 || sb.Length > 0)
        {
            fields.Add(sb.ToString());
            if (fields.Count > ImportLimits.MaxColumns)
            {
                return new CsvParseOutcome(CsvParseFailure.TooManyColumns, recordLine);
            }

            return EndRecord(fields, recordLine, recordCap, capIsFailure, records, ct, out _);
        }

        return new CsvParseOutcome(CsvParseFailure.None, recordLine);
    }

    private static CsvParseOutcome EndRecord(
        List<string> fields,
        int recordLine,
        int recordCap,
        bool capIsFailure,
        List<CsvRecord> records,
        CancellationToken ct,
        out bool stop)
    {
        stop = false;
        ct.ThrowIfCancellationRequested();

        bool blank = true;
        foreach (string f in fields)
        {
            if (!string.IsNullOrWhiteSpace(f))
            {
                blank = false;
                break;
            }
        }

        if (blank)
        {
            fields.Clear();
            return new CsvParseOutcome(CsvParseFailure.None, recordLine);
        }

        if (records.Count >= recordCap)
        {
            if (capIsFailure)
            {
                return new CsvParseOutcome(CsvParseFailure.TooManyRecords, recordLine);
            }

            stop = true;
            return new CsvParseOutcome(CsvParseFailure.None, recordLine);
        }

        records.Add(new CsvRecord(recordLine, new List<string>(fields)));
        fields.Clear();
        return new CsvParseOutcome(CsvParseFailure.None, recordLine);
    }
}
