using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome;

/// <summary>
/// Use case that registers a new income. It orchestrates input validation,
/// warning detection, domain entity creation and persistence.
/// </summary>
/// <remarks>
/// The method returns a non-generic <see cref="Result"/>. On success, the
/// caller must cast <see cref="Result.Value"/> to
/// <see cref="RegisterIncomeResult"/>.
/// </remarks>
public sealed class RegisterIncomeUseCase
{
    private readonly IIncomeRepository _incomeRepository;
    private readonly IClock _clock;
    private readonly IValidator<RegisterIncomeCommand> _validator;
    private readonly ILogger<RegisterIncomeUseCase> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterIncomeUseCase"/> class.
    /// </summary>
    /// <param name="incomeRepository">Repository used to persist the new income.</param>
    /// <param name="clock">Clock abstraction used to source audit timestamps.</param>
    /// <param name="validator">Validator for the incoming command.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public RegisterIncomeUseCase(
        IIncomeRepository incomeRepository,
        IClock clock,
        IValidator<RegisterIncomeCommand> validator,
        ILogger<RegisterIncomeUseCase> logger)
    {
        _incomeRepository = incomeRepository;
        _clock = clock;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Executes the register-income workflow.
    /// </summary>
    /// <param name="command">The command describing the income to register.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>
    /// A <see cref="Result"/> that is successful when the income is persisted,
    /// or failed with a business error (validation, domain invariant or repository error).
    /// </returns>
    public async Task<Result> ExecuteAsync(RegisterIncomeCommand command, CancellationToken ct = default)
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
                "El comando para registrar el ingreso no es válido.",
                details);
        }

        DateTime audit = _clock.UtcNow;
        var warnings = new List<string>();
        if (command.Date > DateOnly.FromDateTime(audit))
        {
            warnings.Add("FutureDateWarning");
        }

        Income income;
        try
        {
            var money = Money.Create(command.Amount, command.Currency);
            income = Income.Create(
                command.CategoryId,
                command.Date,
                money,
                command.Description,
                command.Note,
                null,
                audit,
                command.PaymentMethodId);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while registering income.");
            return Result.Failure("DomainInvariant", ex.Message);
        }

        Result addResult = await _incomeRepository.AddAsync(income, ct);
        if (!addResult.IsSuccess)
        {
            return addResult;
        }

        return Result.Success(new RegisterIncomeResult(income.Id, warnings));
    }
}
