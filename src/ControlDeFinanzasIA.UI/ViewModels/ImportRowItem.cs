using System.Globalization;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>One row of the import preview, formatted for display in the current UI culture (F-020, DD-14).</summary>
public sealed class ImportRowItem
{
    private const string _dateFormat = "dd MMM yyyy";
    private const string _amountFormat = "#,##0.00##";

    /// <summary>Initializes a new instance of the <see cref="ImportRowItem"/> class.</summary>
    /// <param name="row">The planned row.</param>
    public ImportRowItem(ImportRowResult row)
    {
        ArgumentNullException.ThrowIfNull(row);

        CultureInfo culture = CultureInfo.CurrentUICulture;
        RowNumber = row.RowNumber;
        Status = row.Status;
        StatusText = row.Status switch
        {
            ImportRowStatus.Valid => Strings.ImportStatusValid,
            ImportRowStatus.Duplicate => Strings.ImportStatusDuplicate,
            _ => Strings.ImportStatusRejected,
        };
        DateText = row.Date?.ToString(_dateFormat, culture) ?? string.Empty;
        TypeText = row.Type switch
        {
            TransactionType.Expense => Strings.ImportTypeExpense,
            TransactionType.Income => Strings.ImportTypeIncome,
            _ => string.Empty,
        };
        AmountText = row.Amount is { } amount
            ? amount.ToString(_amountFormat, culture) + (row.Currency is { } currency ? " " + currency : string.Empty)
            : string.Empty;
        Description = row.Description ?? string.Empty;
        CategoryText = row.CategoryName ?? string.Empty;
        IsNewCategory = row.IsNewCategory;
        ReasonsText = string.Join(" ", row.Reasons.Select(ReasonText));
        HasReasons = row.Reasons.Count > 0;
        IsProblem = row.Status != ImportRowStatus.Valid || HasReasons;
        IsRejected = row.Status == ImportRowStatus.Rejected;
    }

    /// <summary>Gets the row number in the source file.</summary>
    public int RowNumber { get; }

    /// <summary>Gets the row outcome.</summary>
    public ImportRowStatus Status { get; }

    /// <summary>Gets the localized outcome text.</summary>
    public string StatusText { get; }

    /// <summary>Gets the date as <c>dd MMM yyyy</c> in the UI culture; empty when the date was not read.</summary>
    public string DateText { get; }

    /// <summary>Gets the localized transaction type; empty when it was not resolved.</summary>
    public string TypeText { get; }

    /// <summary>Gets the amount with its currency code; empty when the amount was not read.</summary>
    public string AmountText { get; }

    /// <summary>Gets the description.</summary>
    public string Description { get; }

    /// <summary>Gets the resolved category name.</summary>
    public string CategoryText { get; }

    /// <summary>Gets a value indicating whether the import creates the category.</summary>
    public bool IsNewCategory { get; }

    /// <summary>Gets the localized reasons (rejections and warnings), separated by a space.</summary>
    public string ReasonsText { get; }

    /// <summary>Gets a value indicating whether the row carries at least one reason.</summary>
    public bool HasReasons { get; }

    /// <summary>Gets a value indicating whether the row is a duplicate, is rejected or carries a warning.</summary>
    public bool IsProblem { get; }

    /// <summary>Gets a value indicating whether the row cannot be imported.</summary>
    public bool IsRejected { get; }

    /// <summary>Gets the localized text of a row reason; the enum name when a translation is missing (guarded by a test).</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>The localized message.</returns>
    internal static string ReasonText(ImportReason reason)
    {
        string? text = Strings.ResourceManager.GetString("ImportReason" + reason, Strings.Culture);
        return string.IsNullOrWhiteSpace(text) ? reason.ToString() : text;
    }
}

/// <summary>How many preview rows carry a reason, with the reason's localized text.</summary>
/// <param name="Text">Localized reason text.</param>
/// <param name="Count">Rows carrying the reason.</param>
/// <param name="IsRejection">The reason rejects the row (otherwise it is a warning).</param>
public sealed record ImportReasonSummaryItem(string Text, int Count, bool IsRejection);
