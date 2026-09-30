using System.Globalization;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Parsing;

/// <summary>Evidence gathered about the decimal style of a column of text amounts (F-020, DD-4).</summary>
/// <param name="Resolved">The style to use: <c>Point</c> or <c>Comma</c>, never <c>Auto</c>.</param>
/// <param name="PointEvidence">Tokens that can only be read with a point decimal.</param>
/// <param name="CommaEvidence">Tokens that can only be read with a comma decimal.</param>
/// <param name="Ambiguous">Tokens readable both ways (a single separator followed by three digits).</param>
public sealed record DecimalStyleDetection(DecimalStyle Resolved, int PointEvidence, int CommaEvidence, int Ambiguous)
{
    /// <summary>Gets a value indicating whether both styles have evidence.</summary>
    public bool IsConflict => PointEvidence > 0 && CommaEvidence > 0;

    /// <summary>Gets a value indicating whether any token depends on the decimal style.</summary>
    public bool IsApplicable => PointEvidence + CommaEvidence + Ambiguous > 0;
}

/// <summary>Outcome of parsing one amount cell.</summary>
/// <param name="Value">The signed amount, when the cell was read.</param>
/// <param name="Error">The rejection reason, when it was not.</param>
/// <param name="WasAmbiguous">The token had the ambiguous shape and the style was not chosen by the user.</param>
public readonly record struct AmountParseResult(decimal? Value, ImportReason? Error, bool WasAmbiguous);

/// <summary>
/// Strict, culture-independent amount parser (F-020, DD-4, R-11, R-12, C-1..C-4). Text amounts follow a
/// fixed grammar under an explicit decimal style; spreadsheet numbers bypass the style entirely. Only
/// <see cref="decimal"/> is used and the current culture is never consulted.
/// </summary>
public static class AmountParser
{
    private const char _space = ' ';

    /// <summary>Counts, per token shape, which decimal style the text cells imply and resolves one.</summary>
    /// <param name="cells">The amount cells; only text cells contribute.</param>
    public static DecimalStyleDetection Detect(IEnumerable<RawCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        int point = 0;
        int comma = 0;
        int ambiguous = 0;

        foreach (RawCell cell in cells)
        {
            if (cell.Kind != RawCellKind.Text || !TryTokenize(cell.Text, out Token token))
            {
                continue;
            }

            switch (Classify(token.Body))
            {
                case Evidence.Point:
                    point++;
                    break;
                case Evidence.Comma:
                    comma++;
                    break;
                case Evidence.Ambiguous:
                    ambiguous++;
                    break;
                default:
                    break;
            }
        }

        DecimalStyle resolved = comma > point ? DecimalStyle.Comma : DecimalStyle.Point;
        return new DecimalStyleDetection(resolved, point, comma, ambiguous);
    }

    /// <summary>Parses one amount cell.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="resolved">The decimal style, <c>Point</c> or <c>Comma</c>.</param>
    /// <param name="styleWasAutoResolved">Whether the style came from detection rather than from the user.</param>
    /// <returns>The signed value, or the rejection reason.</returns>
    public static AmountParseResult Parse(RawCell cell, DecimalStyle resolved, bool styleWasAutoResolved = true)
    {
        if (resolved is not (DecimalStyle.Point or DecimalStyle.Comma))
        {
            throw new ArgumentException("The decimal style must be resolved to Point or Comma.", nameof(resolved));
        }

        switch (cell.Kind)
        {
            case RawCellKind.Error:
                return Fail(ImportReason.CellError);
            case RawCellKind.Date:
                return Fail(ImportReason.InvalidAmount);
            case RawCellKind.Number:
                return ParseNumberCell(cell.Text);
            default:
                return ParseText(cell.Text, resolved, styleWasAutoResolved);
        }
    }

    private static AmountParseResult ParseNumberCell(string text)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
        {
            bool looksNumeric = text.Length > 0 && text.All(c => char.IsAsciiDigit(c) || c is '-' or '.');
            return Fail(looksNumeric ? ImportReason.AmountTooLarge : ImportReason.InvalidAmount);
        }

        // The spreadsheet reader only hands over more than four decimals when they are real (not double noise).
        int point = text.IndexOf('.', StringComparison.Ordinal);
        if (point >= 0 && text.Length - point - 1 > ImportLimits.MaxFractionDigits)
        {
            return Fail(ImportReason.TooManyDecimals);
        }

        return Bound(value, wasAmbiguous: false);
    }

    private static AmountParseResult ParseText(string text, DecimalStyle style, bool styleWasAutoResolved)
    {
        string trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return Fail(ImportReason.MissingAmount);
        }

        if (!TryTokenize(trimmed, out Token token))
        {
            return Fail(ImportReason.InvalidAmount);
        }

        char decimalChar = style == DecimalStyle.Point ? '.' : ',';
        char groupChar = style == DecimalStyle.Point ? ',' : '.';
        string body = token.Body;

        int decimalIndex = body.IndexOf(decimalChar, StringComparison.Ordinal);

        if (decimalIndex >= 0 && body.IndexOf(decimalChar, decimalIndex + 1) >= 0)
        {
            return Fail(ImportReason.InvalidAmount);
        }

        string integerPart = decimalIndex >= 0 ? body[..decimalIndex] : body;
        string fraction = decimalIndex >= 0 ? body[(decimalIndex + 1)..] : string.Empty;

        if (decimalIndex >= 0 && (fraction.Length == 0 || !fraction.All(char.IsAsciiDigit)))
        {
            return Fail(ImportReason.InvalidAmount);
        }

        if (!TryStripGroups(integerPart, groupChar, out string digits))
        {
            return Fail(ImportReason.InvalidAmount);
        }

        if (fraction.Length > ImportLimits.MaxFractionDigits)
        {
            return Fail(ImportReason.TooManyDecimals);
        }

        string canonical = fraction.Length == 0 ? digits : digits + "." + fraction;

        if (!decimal.TryParse(canonical, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal magnitude))
        {
            return Fail(ImportReason.AmountTooLarge);
        }

        bool ambiguousShape = styleWasAutoResolved && Classify(body) == Evidence.Ambiguous;
        return Bound(token.Negative ? -magnitude : magnitude, ambiguousShape);
    }

    private static AmountParseResult Bound(decimal value, bool wasAmbiguous)
    {
        if (Math.Abs(value) > ImportLimits.MaxAmount)
        {
            return Fail(ImportReason.AmountTooLarge);
        }

        return new AmountParseResult(value == 0m ? 0m : value, null, wasAmbiguous);
    }

    private static AmountParseResult Fail(ImportReason reason) => new(null, reason, false);

    /// <summary>
    /// Validates the integer part: plain digits, or digit groups of three joined by a single kind of group
    /// character (the style's grouping character or a space). Returns the digits with the groups removed.
    /// </summary>
    private static bool TryStripGroups(string integerPart, char groupChar, out string digits)
    {
        digits = string.Empty;

        if (integerPart.Length == 0 || !char.IsAsciiDigit(integerPart[0]) || !char.IsAsciiDigit(integerPart[^1]))
        {
            return false;
        }

        char? group = null;
        foreach (char c in integerPart)
        {
            if (char.IsAsciiDigit(c))
            {
                continue;
            }

            if (c != groupChar && c != _space)
            {
                return false;
            }

            if (group is not null && group != c)
            {
                return false;
            }

            group = c;
        }

        if (group is null)
        {
            digits = integerPart;
            return true;
        }

        string[] parts = integerPart.Split(group.Value);
        bool wellFormed = parts[0].Length is >= 1 and <= 3
            && parts[0][0] != '0'
            && parts.Skip(1).All(p => p.Length == 3);

        if (!wellFormed)
        {
            return false;
        }

        digits = string.Concat(parts);
        return true;
    }

    /// <summary>
    /// Splits sign and parentheses from the body and normalizes the group whitespace. Returns false when the
    /// token contains anything outside digits, point, comma and single spaces between digits.
    /// </summary>
    private static bool TryTokenize(string text, out Token token)
    {
        token = default;
        string trimmed = text.Trim();
        bool negative = false;

        if (trimmed.Length >= 3 && trimmed[0] == '(' && trimmed[^1] == ')')
        {
            negative = true;
            trimmed = trimmed[1..^1];
        }
        else if (trimmed.Length >= 2 && trimmed[0] is '-' or '+')
        {
            negative = trimmed[0] == '-';
            trimmed = trimmed[1..];
        }

        if (trimmed.Length == 0 || !char.IsAsciiDigit(trimmed[0]) || !char.IsAsciiDigit(trimmed[^1]))
        {
            return false;
        }

        char[] body = new char[trimmed.Length];
        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];

            if (char.IsAsciiDigit(c) || c is '.' or ',')
            {
                body[i] = c;
            }
            else if (c is ' ' or ' ' or ' ')
            {
                body[i] = _space;
            }
            else
            {
                return false;
            }
        }

        token = new Token(new string(body), negative);
        return true;
    }

    private static Evidence Classify(string body)
    {
        int points = body.Count(c => c == '.');
        int commas = body.Count(c => c == ',');

        if (points == 0 && commas == 0)
        {
            return Evidence.None;
        }

        if (points > 0 && commas > 0)
        {
            return body.LastIndexOf('.') > body.LastIndexOf(',') ? Evidence.Point : Evidence.Comma;
        }

        char separator = points > 0 ? '.' : ',';
        Evidence asDecimal = points > 0 ? Evidence.Point : Evidence.Comma;

        if (points + commas >= 2)
        {
            return points > 0 ? Evidence.Comma : Evidence.Point;
        }

        int index = body.IndexOf(separator, StringComparison.Ordinal);
        int digitsAfter = body.Length - index - 1;
        string before = body[..index];

        bool couldBeFirstGroup = before.Length is >= 1 and <= 3 && before[0] != '0' && before.All(char.IsAsciiDigit);

        if (digitsAfter == 3 && couldBeFirstGroup)
        {
            return Evidence.Ambiguous;
        }

        return asDecimal;
    }

    private enum Evidence
    {
        None,
        Point,
        Comma,
        Ambiguous,
    }

    private readonly record struct Token(string Body, bool Negative);
}
