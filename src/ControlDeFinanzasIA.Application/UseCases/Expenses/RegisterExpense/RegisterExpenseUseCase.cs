using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense;

/// <summary>
/// Use case that registers a new expense. It orchestrates input validation,
/// warning detection, domain entity creation, persistence and DTO mapping.
/// </summary>
/// <remarks>
/// The method returns a non-generic <see cref="Result"/>. On success, the
/// caller must cast <see cref="Result.Value"/> to
/// <see cref="RegisterExpenseResult"/>.
/// </remarks>
public sealed class RegisterExpenseUseCase
{
    private readonly IExpenseRepository _repository;
    private readonly IClock _clock;
    private readonly IValidator<RegisterExpenseCommand> _validator;
    private readonly ILogger<RegisterExpenseUseCase> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterExpenseUseCase"/> class.
    /// </summary>
    /// <param name="repository">Repository used to persist the new expense.</param>
    /// <param name="clock">Clock abstraction used to source audit timestamps.</param>
    /// <param name="validator">Validator for the incoming command.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public RegisterExpenseUseCase(
        IExpenseRepository repository,
        IClock clock,
        IValidator<RegisterExpenseCommand> validator,
        ILogger<RegisterExpenseUseCase> logger)
    {
        _repository = repository;
        _clock = clock;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Executes the register-expense workflow.
    /// </summary>
    /// <param name="command">The command describing the expense to register.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>
    /// A <see cref="Result"/> that is successful when the expense is persisted,
    /// or failed with a business error (validation, domain invariant or repository error).
    /// </returns>
    public async Task<Result> ExecuteAsync(RegisterExpenseCommand command, CancellationToken ct = default)
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
                "El comando para registrar el gasto no es válido.",
                details);
        }

        DateTime audit = _clock.UtcNow;
        var warnings = new List<string>();
        if (command.Date > DateOnly.FromDateTime(audit))
        {
            warnings.Add("FutureDateWarning");
        }

        Expense expense;
        try
        {
            var money = Money.Create(command.Amount, command.Currency);
            expense = Expense.Create(
                command.CategoryId,
                command.Date,
                money,
                command.Description,
                command.Note,
                audit,
                command.PaymentMethodId);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while registering expense.");
            return Result.Failure("DomainInvariant", ex.Message);
        }

        Result addResult = await _repository.AddAsync(expense, ct);
        if (!addResult.IsSuccess)
        {
            return addResult;
        }

        ExpenseDto dto = expense.ToDto();
        return Result.Success(new RegisterExpenseResult(dto, warnings));
    }
}
