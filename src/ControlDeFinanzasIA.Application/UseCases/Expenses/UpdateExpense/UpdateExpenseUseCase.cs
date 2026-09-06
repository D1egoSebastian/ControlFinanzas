using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Expenses.UpdateExpense;

/// <summary>
/// Use case that updates an existing expense. It orchestrates input validation,
/// aggregate lookup, domain entity mutation, persistence and result propagation.
/// </summary>
/// <remarks>
/// The method returns a non-generic <see cref="Result"/>. On success the payload
/// is <c>null</c> (<see cref="Result.Success(object)"/> with <c>null!</c>);
/// warnings such as <c>FutureDateWarning</c> are intentionally not surfaced
/// until the UI edit feature (4-008) introduces an update DTO.
/// </remarks>
public sealed class UpdateExpenseUseCase
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IClock _clock;
    private readonly IValidator<UpdateExpenseCommand> _validator;
    private readonly ILogger<UpdateExpenseUseCase> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateExpenseUseCase"/> class.
    /// </summary>
    /// <param name="expenseRepository">Repository used to persist the updated expense.</param>
    /// <param name="clock">Clock abstraction used to source audit timestamps.</param>
    /// <param name="validator">Validator for the incoming command.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public UpdateExpenseUseCase(
        IExpenseRepository expenseRepository,
        IClock clock,
        IValidator<UpdateExpenseCommand> validator,
        ILogger<UpdateExpenseUseCase> logger)
    {
        _expenseRepository = expenseRepository;
        _clock = clock;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Executes the update-expense workflow.
    /// </summary>
    /// <param name="command">The command describing the expense to update.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>
    /// A <see cref="Result"/> that is successful when the expense is persisted,
    /// or failed with a business error (validation, not found, domain invariant or repository error).
    /// </returns>
    public async Task<Result> ExecuteAsync(UpdateExpenseCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidationResult validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            IReadOnlyDictionary<string, string[]> details = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray(),
                    StringComparer.Ordinal);

            return Result.Failure(
                "Validation",
                "El comando para actualizar el gasto no es válido.",
                details);
        }

        Expense? existing = await _expenseRepository.GetByIdAsync(command.Id, ct);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Expense {command.Id} does not exist.");
        }

        DateTime audit = _clock.UtcNow;
        try
        {
            var money = Money.Create(command.Amount, command.Currency);
            existing.Update(
                command.CategoryId,
                command.Date,
                money,
                command.Description,
                command.Note,
                audit);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while updating expense.");
            return Result.Failure("DomainInvariant", ex.Message);
        }

        Result updateResult = await _expenseRepository.UpdateAsync(existing, ct);
        if (!updateResult.IsSuccess)
        {
            return updateResult;
        }

        return Result.Success(null!);
    }
}
