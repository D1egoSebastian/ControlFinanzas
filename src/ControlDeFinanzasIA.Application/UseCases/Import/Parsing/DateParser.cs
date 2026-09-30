using System.Globalization;
using System.Text.RegularExpressions;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Parsing;

/// <summary>How well the text dates of a column agree with the selected date format (F-020, DD-5).</summary>
public enum DateEvidence
{
    /// <summary>No non-ISO text dates to judge.</summary>
    NotApplicable,

    /// <summary>At least one date settles the order and none contradicts the selection.</summary>
    Consistent,

    /// <summary>Every judged date has both parts at most 12, so the order is a guess.</summary>
    AmbiguousOnly,

    /// <summary>At least one date can only be read with the other order.</summary>
    Contradicts,
}

/// <summary>Outcome of parsing one date cell.</summary>
/// <param name="Value">The date, when it was read.</param>
/// <param name="Error">The rejection reason, when it was not.</param>
public readonly record struct DateParseResult(DateOnly? Value, ImportReason? Error);

/// <summary>
/// Strict, culture-independent date parser (F-020, DD-5, R-13, C-6, C-7). ISO dates are always tried first
/// and never fall through to a user format; other text dates use literal separators and a four-digit year.
/// </summary>
public static partial class DateParser
{
    private const string _isoFormat = "yyyy-MM-dd";

    /// <summary>Parses one date cell.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="format">Order of non-ISO text dates.</param>
    public static DateParseResult Parse(RawCell cell, ImportDateFormat format)
    {
        switch (cell.Kind)
        {
            case RawCellKind.Error:
                return Fail(ImportReason.CellError);
            case RawCellKind.Number:
                return Fail(ImportReason.InvalidDate);
            case RawCellKind.Date:
                return DateOnly.TryParseExact(cell.Text, _isoFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly exact)
                    ? new DateParseResult(exact, null)
                    : Fail(ImportReason.InvalidDate);
            default:
                return ParseText(cell.Text.Trim(), format);
        }
    }

    /// <summary>Judges the non-ISO text dates against the selected format.</summary>
    /// <param name="cells">The date cells; only text cells contribute.</param>
    /// <param name="format">The selected format.</param>
    public static DateEvidence Detect(IEnumerable<RawCell> cells, ImportDateFormat format)
    {
        ArgumentNullException.ThrowIfNull(cells);

        bool selectedDayFirst = format != ImportDateFormat.MonthDayYear;
        int consistent = 0;
        int contradicting = 0;
        int ambiguous = 0;

        foreach (RawCell cell in cells)
        {
            if (cell.Kind != RawCellKind.Text)
            {
                continue;
            }

            Match match = TextDatePattern().Match(cell.Text.Trim());

            if (!match.Success)
            {
                continue;
            }

            int first = int.Parse(match.Groups["a"].Value, CultureInfo.InvariantCulture);
            int second = int.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture);

            if (first > 12 && second <= 12)
            {
                Count(dayFirst: true);
            }
            else if (second > 12 && first <= 12)
            {
                Count(dayFirst: false);
            }
            else if (first <= 12 && second <= 12)
            {
                ambiguous++;
            }
        }

        if (contradicting > 0)
        {
            return DateEvidence.Contradicts;
        }

        if (consistent > 0)
        {
            return DateEvidence.Consistent;
        }

        return ambiguous > 0 ? DateEvidence.AmbiguousOnly : DateEvidence.NotApplicable;

        void Count(bool dayFirst)
        {
            if (dayFirst == selectedDayFirst)
            {
                consistent++;
            }
            else
            {
                contradicting++;
            }
        }
    }

    [GeneratedRegex(@"^(?<a>\d{1,2})[/-](?<b>\d{1,2})[/-]\d{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex TextDatePattern();

    [GeneratedRegex(@"^[T ]([01]\d|2[0-3]):[0-5]\d(:[0-5]\d(\.\d+)?)?(Z|[+-]\d{2}:?\d{2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoTimeSuffixPattern();

    private static DateParseResult ParseText(string text, ImportDateFormat format)
    {
        if (text.Length == 0)
        {
            return Fail(ImportReason.MissingDate);
        }

        if (text.Length >= 10 && text[4] == '-' && text[7] == '-')
        {
            return ParseIso(text);
        }

        if (!TextDatePattern().IsMatch(text))
        {
            return Fail(ImportReason.InvalidDate);
        }

        string pattern = format switch
        {
            ImportDateFormat.DayMonthYear => "d'/'M'/'yyyy",
            ImportDateFormat.MonthDayYear => "M'/'d'/'yyyy",
            _ => "d'-'M'-'yyyy",
        };

        return DateOnly.TryParseExact(text, pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? new DateParseResult(date, null)
            : Fail(ImportReason.InvalidDate);
    }

    private static DateParseResult ParseIso(string text)
    {
        if (!DateOnly.TryParseExact(text[..10], _isoFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            return Fail(ImportReason.InvalidDate);
        }

        string rest = text[10..];
        bool restIsValid = rest.Length == 0 || IsoTimeSuffixPattern().IsMatch(rest);

        return restIsValid ? new DateParseResult(date, null) : Fail(ImportReason.InvalidDate);
    }

    private static DateParseResult Fail(ImportReason reason) => new(null, reason);
}
