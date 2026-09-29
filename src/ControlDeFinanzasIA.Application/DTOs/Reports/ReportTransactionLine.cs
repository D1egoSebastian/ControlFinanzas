using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.DTOs.Reports;

/// <summary>One transaction row of a report.</summary>
/// <param name="Date">Transaction date.</param>
/// <param name="Kind">Expense or income.</param>
/// <param name="Description">Free-text description.</param>
/// <param name="CategoryName">Category name, empty when unknown or absent.</param>
/// <param name="Amount">Amount in its original currency.</param>
/// <param name="Currency">Original currency.</param>
/// <param name="AmountDop">DOP equivalent; <c>null</c> when no rate is available (never 0).</param>
public sealed record ReportTransactionLine(
    DateOnly Date,
    ReportTransactionKind Kind,
    string Description,
    string CategoryName,
    decimal Amount,
    Currency Currency,
    decimal? AmountDop);
