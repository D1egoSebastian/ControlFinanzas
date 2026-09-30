namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>How a source cell stored its value (F-020, DD-1).</summary>
public enum RawCellKind
{
    /// <summary>Text.</summary>
    Text,

    /// <summary>A spreadsheet number.</summary>
    Number,

    /// <summary>A spreadsheet date.</summary>
    Date,

    /// <summary>A spreadsheet error value such as <c>#N/A</c>.</summary>
    Error,
}

/// <summary>Kind of imported file.</summary>
public enum ImportFileKind
{
    /// <summary>Delimited text.</summary>
    Csv,

    /// <summary>Excel workbook.</summary>
    Xlsx,
}

/// <summary>Importable transaction fields.</summary>
public enum ImportField
{
    /// <summary>Transaction date.</summary>
    Date,

    /// <summary>Expense or income.</summary>
    Type,

    /// <summary>Description.</summary>
    Description,

    /// <summary>Category name.</summary>
    Category,

    /// <summary>Amount.</summary>
    Amount,

    /// <summary>Currency code.</summary>
    Currency,

    /// <summary>Payment method name.</summary>
    PaymentMethod,
}

/// <summary>How decimal and grouping separators are read in text amounts.</summary>
public enum DecimalStyle
{
    /// <summary>Detect from the data.</summary>
    Auto,

    /// <summary>Point is the decimal separator (<c>1,234.56</c>).</summary>
    Point,

    /// <summary>Comma is the decimal separator (<c>1.234,56</c>).</summary>
    Comma,
}

/// <summary>Order of the parts of a non-ISO text date. ISO dates are always accepted.</summary>
public enum ImportDateFormat
{
    /// <summary><c>d/M/yyyy</c>.</summary>
    DayMonthYear,

    /// <summary><c>M/d/yyyy</c>.</summary>
    MonthDayYear,

    /// <summary><c>d-M-yyyy</c>.</summary>
    DayMonthYearDash,
}

/// <summary>Whether the sign of the amount decides the transaction type.</summary>
public enum SignMode
{
    /// <summary>Amounts are positive; the type comes from the Type column or the default.</summary>
    Unsigned,

    /// <summary>Negative amounts are expenses, positive amounts are incomes.</summary>
    NegativeIsExpense,
}

/// <summary>Outcome of a row after planning.</summary>
public enum ImportRowStatus
{
    /// <summary>Will be imported.</summary>
    Valid,

    /// <summary>Already stored (or repeated) and skipped.</summary>
    Duplicate,

    /// <summary>Cannot be imported.</summary>
    Rejected,
}
