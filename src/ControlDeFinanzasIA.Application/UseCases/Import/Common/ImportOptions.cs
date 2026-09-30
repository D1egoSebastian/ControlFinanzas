using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>Everything the user can configure for one import (F-020, DD-1).</summary>
/// <param name="Mapping">Column mapping; <c>null</c> means automatic header mapping.</param>
/// <param name="Delimiter">CSV delimiter; <c>null</c> sniffs it.</param>
/// <param name="DecimalStyle">Decimal style for text amounts.</param>
/// <param name="DateFormat">Order of non-ISO text dates.</param>
/// <param name="DefaultType">Type used when the file does not say.</param>
/// <param name="DefaultCurrency">Currency used when the file does not say.</param>
/// <param name="SignMode">Whether the amount sign decides the type.</param>
/// <param name="SkipDuplicates">Skip rows that already exist.</param>
/// <param name="CreateMissingCategories">Create categories that do not exist yet.</param>
public sealed record ImportOptions(
    ColumnMapping? Mapping,
    char? Delimiter,
    DecimalStyle DecimalStyle,
    ImportDateFormat DateFormat,
    TransactionType DefaultType = TransactionType.Expense,
    Currency DefaultCurrency = Currency.DOP,
    SignMode SignMode = SignMode.Unsigned,
    bool SkipDuplicates = true,
    bool CreateMissingCategories = true);
