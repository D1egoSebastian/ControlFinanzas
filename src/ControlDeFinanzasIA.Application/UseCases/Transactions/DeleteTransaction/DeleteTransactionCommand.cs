using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Transactions.DeleteTransaction;

/// <summary>
/// Immutable command to delete a transaction by identity and type.
/// <see cref="Type"/> dispatches the hard delete to the matching
/// repository (<c>IExpenseRepository</c> or <c>IIncomeRepository</c>).
/// </summary>
public sealed record DeleteTransactionCommand(
    Guid Id,
    TransactionType Type);
