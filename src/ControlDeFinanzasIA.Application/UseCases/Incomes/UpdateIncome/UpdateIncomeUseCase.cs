using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Incomes.UpdateIncome;

/// <summary>
/// Use case that updates an existing income. It orchestrates input validation,
/// aggregate lookup, domain entity mutation, persistence and result propagation.
/// </summary>
/// <remarks>
/// The method returns a non-generic <see cref="Result"/>. On success the payload
/// is <c>null</c> (<see cref="Result.Success(object)"/> with <c>null!</c>);
/// <see cref="Income.Source"/> is reset to <c>null</c> because the command does
/// not carry a source value. Warnings such as <c>FutureDateWarning</c> are
/// intentionally not surfaced until the UI edit feature (4-008) introduces an
/// update DTO.
/// </remarks>
public sealed class UpdateIncomeUseCase
{
    private readonly IIncomeRepository _incomeRepository;
    private readonly IClock _clock;
    private readonly IValidator<UpdateIncomeCommand> _validator;
    private readonly ILogger<UpdateIncomeUseCase> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateIncomeUseCase"/> class.
    /// </summary>
    /// <param name="incomeRepository">Repository used to persist the updated income.</param>
    /// <param name="clock">Clock abstraction used to source audit timestamps.</param>
    /// <param name="validator">Validator for the incoming command.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public UpdateIncomeUseCase(
        IIncomeRepository incomeRepository,
        IClock clock,
        IValidator<UpdateIncomeCommand> validator,
        ILogger<UpdateIncomeUseCase> logger)
    {
        _incomeRepository = incomeRepository;
        _clock = clock;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Executes the update-income workflow.
    /// </summary>
    /// <param name="command">The command describing the income to update.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>
    /// A <see cref="Result"/> that is successful when the income is persisted,
    /// or failed with a business error (validation, not found, domain invariant or repository error).
    /// </returns>
    public async Task<Result> ExecuteAsync(UpdateIncomeCommand command, CancellationToken ct = default)
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
                "El comando para actualizar el ingreso no es válido.",
                details);
        }

        Income? existing = await _incomeRepository.GetByIdAsync(command.Id, ct);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Income {command.Id} does not exist.");
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
                null,
                audit);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while updating income.");
            return Result.Failure("DomainInvariant", ex.Message);
        }

        Result updateResult = await _incomeRepository.UpdateAsync(existing, ct);
        if (!updateResult.IsSuccess)
        {
            return updateResult;
        }

        return Result.Success(null!);
    }
}
