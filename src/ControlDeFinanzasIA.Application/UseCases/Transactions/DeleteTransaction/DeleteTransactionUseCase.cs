using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Transactions.DeleteTransaction;

/// <summary>
/// Use case that deletes a transaction by dispatching to the repository
/// that matches the supplied <see cref="DeleteTransactionCommand.Type"/>.
/// </summary>
/// <remarks>
/// This use case does not use FluentValidation; the only input guard is an
/// inline check for <see cref="Guid.Empty"/>. The repository result is
/// propagated verbatim.
/// </remarks>
public sealed class DeleteTransactionUseCase
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IIncomeRepository _incomeRepository;
    private readonly ILogger<DeleteTransactionUseCase> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTransactionUseCase"/> class.
    /// </summary>
    /// <param name="expenseRepository">Repository used to delete expenses.</param>
    /// <param name="incomeRepository">Repository used to delete incomes.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public DeleteTransactionUseCase(
        IExpenseRepository expenseRepository,
        IIncomeRepository incomeRepository,
        ILogger<DeleteTransactionUseCase> logger)
    {
        _expenseRepository = expenseRepository;
        _incomeRepository = incomeRepository;
        _logger = logger;
    }

    /// <summary>
    /// Executes the delete-transaction workflow.
    /// </summary>
    /// <param name="command">The command describing the transaction to delete.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>
    /// A <see cref="Result"/> that is successful when the repository deletion succeeds,
    /// or failed with a business error (validation, unknown type or repository error).
    /// </returns>
    public async Task<Result> ExecuteAsync(DeleteTransactionCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Id == Guid.Empty)
        {
            return Result.Failure("Validation", "Id cannot be empty.");
        }

        switch (command.Type)
        {
            case TransactionType.Expense:
                Result expenseResult = await _expenseRepository.RemoveAsync(command.Id, ct);
                return expenseResult;

            case TransactionType.Income:
                Result incomeResult = await _incomeRepository.RemoveAsync(command.Id, ct);
                return incomeResult;

            default:
                return Result.Failure("Validation", $"Unknown transaction type: {command.Type}.");
        }
    }
}
