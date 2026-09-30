using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Parsing;

/// <summary>A row after validation, before category and duplicate planning (F-020, DD-6).</summary>
/// <param name="RowNumber">Source row number.</param>
/// <param name="Date">The date, when it was read.</param>
/// <param name="Type">The resolved type.</param>
/// <param name="Amount">The absolute amount.</param>
/// <param name="Currency">The resolved currency.</param>
/// <param name="Description">The trimmed description.</param>
/// <param name="CategoryText">The trimmed category cell; <c>null</c> when unmapped or blank.</param>
/// <param name="PaymentMethodText">The trimmed payment method cell; <c>null</c> when unmapped or blank.</param>
/// <param name="Reasons">Every rejection and warning found, without repeats.</param>
/// <param name="AmountWasAmbiguous">The amount could be read two ways and the style was not chosen by the user.</param>
public sealed record ParsedRow(
    int RowNumber,
    DateOnly? Date,
    TransactionType? Type,
    decimal? Amount,
    Currency? Currency,
    string? Description,
    string? CategoryText,
    string? PaymentMethodText,
    IReadOnlyList<ImportReason> Reasons,
    bool AmountWasAmbiguous)
{
    /// <summary>Gets a value indicating whether any reason rejects the row.</summary>
    public bool IsRejected => Reasons.Any(ImportReasons.IsRejection);
}

/// <summary>
/// Pure row validator (F-020, DD-6, R-10, R-11). Collects every reason instead of stopping at the first, never
/// interprets a row whose field count does not match the header, and mirrors the domain invariants so an
/// accepted row always builds with <c>Expense.Create</c> / <c>Income.Create</c>.
/// </summary>
public static class ImportRowValidator
{
    private const int _maxDescriptionChars = 200;
    private const char _replacementChar = (char)0xFFFD;
    private static readonly DateOnly _minDate = new(1900, 1, 1);

    /// <summary>Returns whether every cell of the row is empty text, so the row can be skipped and not counted.</summary>
    /// <param name="row">The row.</param>
    public static bool IsBlank(RawRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Cells.All(IsEmptyText);
    }

    /// <summary>Validates one row.</summary>
    /// <param name="row">The raw row.</param>
    /// <param name="map">A complete column mapping whose indexes are inside the header.</param>
    /// <param name="headerCount">Number of header columns.</param>
    /// <param name="options">The import options.</param>
    /// <param name="resolvedStyle">The decimal style, <c>Point</c> or <c>Comma</c>.</param>
    /// <param name="today">Today's date, taken from the clock by the caller.</param>
    public static ParsedRow Parse(
        RawRow row,
        ColumnMapping map,
        int headerCount,
        ImportOptions options,
        DecimalStyle resolvedStyle,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(options);
        GuardMapping(map, headerCount);

        if (!HasExpectedShape(row.Cells, headerCount))
        {
            return new ParsedRow(row.SourceRowNumber, null, null, null, null, null, null, null, new[] { ImportReason.ColumnCountMismatch }, false);
        }

        List<ImportReason> reasons = new();
        IReadOnlyList<RawCell> cells = row.Cells;

        bool hasCellError = new[] { map.Date, map.Type, map.Description, map.Category, map.Amount, map.Currency, map.PaymentMethod }
            .Any(index => index is int i && cells[i].Kind == RawCellKind.Error);

        if (hasCellError)
        {
            reasons.Add(ImportReason.CellError);
        }

        DateOnly? date = ReadDate(cells[map.Date!.Value], options, today, reasons);
        (decimal? amount, TransactionType? type, bool ambiguous) = ReadAmountAndType(cells, map, options, resolvedStyle, reasons);
        string? description = ReadDescription(cells[map.Description!.Value], reasons);
        Currency? currency = ReadCurrency(cells, map, options, reasons);

        // U+FFFD (or a C1 control) in free text means the decoder could not read some bytes (F-020 post-verify,
        // W4): the original characters are gone, so importing the row would store corrupted text.
        if (HasReplacementCharacter(cells[map.Description.Value])
            || (map.Category is int categoryIndex && HasReplacementCharacter(cells[categoryIndex])))
        {
            reasons.Add(ImportReason.UndecodableText);
        }

        return new ParsedRow(
            row.SourceRowNumber,
            date,
            type,
            amount,
            currency,
            description,
            OptionalText(cells, map.Category),
            OptionalText(cells, map.PaymentMethod),
            reasons.Distinct().ToList(),
            ambiguous);
    }

    private static void GuardMapping(ColumnMapping map, int headerCount)
    {
        if (!map.IsComplete)
        {
            throw new ArgumentException("The column mapping must map Date, Description and Amount.", nameof(map));
        }

        foreach (int? index in new[] { map.Date, map.Type, map.Description, map.Category, map.Amount, map.Currency, map.PaymentMethod })
        {
            if (index is int i && (i < 0 || i >= headerCount))
            {
                throw new ArgumentException("A mapped column index is outside the header.", nameof(map));
            }
        }
    }

    /// <summary>Trailing empty fields beyond the header are dropped; any other length difference is a mismatch.</summary>
    internal static bool HasExpectedShape(IReadOnlyList<RawCell> cells, int headerCount)
    {
        if (cells.Count == headerCount)
        {
            return true;
        }

        return cells.Count > headerCount && cells.Skip(headerCount).All(IsEmptyText);
    }

    // U+FFFD is what a replacement decoder emits; U+0080..U+009F (C1 controls) is what .NET's Windows-1252 table
    // emits for the bytes Windows-1252 leaves undefined (0x81, 0x8D, 0x8F, 0x90, 0x9D). Neither is ever real text.
    private static bool HasReplacementCharacter(RawCell cell) =>
        cell.Kind == RawCellKind.Text && cell.Text.Any(c => c == _replacementChar || (char.IsControl(c) && c >= (char)0x0080));

    private static bool IsEmptyText(RawCell cell) =>
        cell.Kind == RawCellKind.Text && string.IsNullOrWhiteSpace(cell.Text);

    private static DateOnly? ReadDate(RawCell cell, ImportOptions options, DateOnly today, List<ImportReason> reasons)
    {
        if (cell.Kind == RawCellKind.Error)
        {
            return null;
        }

        DateParseResult parsed = DateParser.Parse(cell, options.DateFormat);

        if (parsed.Value is not DateOnly value)
        {
            reasons.Add(parsed.Error ?? ImportReason.InvalidDate);
            return null;
        }

        if (value < _minDate)
        {
            reasons.Add(ImportReason.DateBefore1900);
            return null;
        }

        if (value > today)
        {
            reasons.Add(ImportReason.FutureDate);
        }

        return value;
    }

    private static (decimal? Amount, TransactionType? Type, bool Ambiguous) ReadAmountAndType(
        IReadOnlyList<RawCell> cells,
        ColumnMapping map,
        ImportOptions options,
        DecimalStyle resolvedStyle,
        List<ImportReason> reasons)
    {
        RawCell amountCell = cells[map.Amount!.Value];
        decimal? amount = null;
        bool ambiguous = false;
        decimal? signed = null;

        if (amountCell.Kind != RawCellKind.Error)
        {
            AmountParseResult parsed = AmountParser.Parse(amountCell, resolvedStyle, options.DecimalStyle == DecimalStyle.Auto);

            if (parsed.Value is decimal value)
            {
                signed = value;
                ambiguous = parsed.WasAmbiguous;
            }
            else
            {
                reasons.Add(parsed.Error ?? ImportReason.InvalidAmount);
            }
        }

        TransactionType? type;

        if (options.SignMode == SignMode.NegativeIsExpense)
        {
            type = signed is decimal s && s != 0m ? (s < 0m ? TransactionType.Expense : TransactionType.Income) : null;
        }
        else
        {
            type = ReadType(cells, map, options, reasons);
        }

        if (signed is decimal number)
        {
            if (number == 0m)
            {
                reasons.Add(ImportReason.ZeroAmount);
            }
            else if (number < 0m && options.SignMode == SignMode.Unsigned)
            {
                reasons.Add(ImportReason.NegativeAmount);
            }
            else
            {
                amount = Math.Abs(number);
            }

            if (ambiguous && amount is not null)
            {
                reasons.Add(ImportReason.AmbiguousAmount);
            }
        }

        return (amount, type, amount is not null && ambiguous);
    }

    private static TransactionType? ReadType(IReadOnlyList<RawCell> cells, ColumnMapping map, ImportOptions options, List<ImportReason> reasons)
    {
        if (map.Type is not int index)
        {
            return options.DefaultType;
        }

        RawCell cell = cells[index];

        if (cell.Kind == RawCellKind.Error)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(cell.Text))
        {
            reasons.Add(ImportReason.TypeDefaulted);
            return options.DefaultType;
        }

        switch (ImportText.LooseKey(cell.Text))
        {
            case "EXPENSE":
            case "GASTO":
            case "EGRESO":
                return TransactionType.Expense;
            case "INCOME":
            case "INGRESO":
                return TransactionType.Income;
            default:
                reasons.Add(ImportReason.InvalidType);
                return null;
        }
    }

    private static string? ReadDescription(RawCell cell, List<ImportReason> reasons)
    {
        if (cell.Kind == RawCellKind.Error)
        {
            return null;
        }

        string trimmed = cell.Text.Trim();

        if (trimmed.Length == 0)
        {
            reasons.Add(ImportReason.MissingDescription);
            return null;
        }

        if (trimmed.Length > _maxDescriptionChars)
        {
            reasons.Add(ImportReason.DescriptionTooLong);
            return null;
        }

        return trimmed;
    }

    private static Currency? ReadCurrency(IReadOnlyList<RawCell> cells, ColumnMapping map, ImportOptions options, List<ImportReason> reasons)
    {
        if (map.Currency is not int index)
        {
            return options.DefaultCurrency;
        }

        RawCell cell = cells[index];

        if (cell.Kind == RawCellKind.Error)
        {
            return null;
        }

        string text = cell.Text.Trim();

        if (text.Length == 0)
        {
            reasons.Add(ImportReason.CurrencyDefaulted);
            return options.DefaultCurrency;
        }

        if (string.Equals(text, "DOP", StringComparison.OrdinalIgnoreCase))
        {
            return Currency.DOP;
        }

        if (string.Equals(text, "USD", StringComparison.OrdinalIgnoreCase))
        {
            return Currency.USD;
        }

        reasons.Add(ImportReason.UnsupportedCurrency);
        return null;
    }

    private static string? OptionalText(IReadOnlyList<RawCell> cells, int? index)
    {
        if (index is not int i || cells[i].Kind == RawCellKind.Error)
        {
            return null;
        }

        string trimmed = cells[i].Text.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
