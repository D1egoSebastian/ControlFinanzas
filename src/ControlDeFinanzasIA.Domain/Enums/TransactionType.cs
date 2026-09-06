namespace ControlDeFinanzasIA.Domain.Enums;

/// <summary>
/// Discriminates the transaction type for delete dispatch in
/// <see cref="ControlDeFinanzasIA.Application.UseCases.Transactions.DeleteTransaction.DeleteTransactionUseCase"/>.
/// </summary>
public enum TransactionType
{
    /// <summary>Expense aggregate.</summary>
    Expense,

    /// <summary>Income aggregate.</summary>
    Income
}
