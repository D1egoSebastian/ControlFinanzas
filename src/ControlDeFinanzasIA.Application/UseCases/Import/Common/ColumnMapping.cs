namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>Zero-based header index chosen for each importable field (F-020, DD-1).</summary>
/// <param name="Date">Date column.</param>
/// <param name="Type">Type column.</param>
/// <param name="Description">Description column.</param>
/// <param name="Category">Category column.</param>
/// <param name="Amount">Amount column.</param>
/// <param name="Currency">Currency column.</param>
/// <param name="PaymentMethod">Payment method column.</param>
public sealed record ColumnMapping(
    int? Date,
    int? Type,
    int? Description,
    int? Category,
    int? Amount,
    int? Currency,
    int? PaymentMethod)
{
    /// <summary>Gets a value indicating whether Date, Description and Amount are all mapped.</summary>
    public bool IsComplete => Date is not null && Description is not null && Amount is not null;
}
