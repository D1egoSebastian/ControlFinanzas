namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>
/// Row-level rejection or warning (F-020, DD-2). Values before <see cref="FutureDate"/> reject the row;
/// values from it on are warnings and the row is still imported. Keep that order.
/// </summary>
public enum ImportReason
{
    /// <summary>The row has a different number of fields than the header.</summary>
    ColumnCountMismatch,

    /// <summary>A mapped cell holds a spreadsheet error.</summary>
    CellError,

    /// <summary>No date.</summary>
    MissingDate,

    /// <summary>The date cannot be read.</summary>
    InvalidDate,

    /// <summary>The date is before 1900-01-01.</summary>
    DateBefore1900,

    /// <summary>No amount.</summary>
    MissingAmount,

    /// <summary>The amount cannot be read.</summary>
    InvalidAmount,

    /// <summary>The amount is zero.</summary>
    ZeroAmount,

    /// <summary>The amount is negative and sign mode is off.</summary>
    NegativeAmount,

    /// <summary>More than four decimal places.</summary>
    TooManyDecimals,

    /// <summary>Above the maximum storable amount.</summary>
    AmountTooLarge,

    /// <summary>No description.</summary>
    MissingDescription,

    /// <summary>Description above 200 characters.</summary>
    DescriptionTooLong,

    /// <summary>The type text is not recognized.</summary>
    InvalidType,

    /// <summary>The currency is not DOP or USD.</summary>
    UnsupportedCurrency,

    /// <summary>The category text matches several existing categories.</summary>
    AmbiguousCategory,

    /// <summary>The new category name is above 100 characters.</summary>
    CategoryNameTooLong,

    /// <summary>The import would create too many categories.</summary>
    TooManyNewCategories,

    /// <summary>The fallback category is missing from the database.</summary>
    NoFallbackCategory,

    /// <summary>The description or category holds characters the decoder could not read (U+FFFD or a C1 control).</summary>
    UndecodableText,

    /// <summary>Warning: the date is in the future.</summary>
    FutureDate,

    /// <summary>Warning: the amount could be read two ways.</summary>
    AmbiguousAmount,

    /// <summary>Warning: an empty type cell took the default.</summary>
    TypeDefaulted,

    /// <summary>Warning: an empty currency cell took the default.</summary>
    CurrencyDefaulted,

    /// <summary>Warning: the fallback category was used.</summary>
    CategoryFallback,

    /// <summary>Warning: an income category was dropped.</summary>
    CategoryDropped,

    /// <summary>Warning: the payment method was not found.</summary>
    PaymentMethodNotFound,

    /// <summary>Warning: a duplicate row is imported because duplicates are not skipped.</summary>
    DuplicateImported,
}
