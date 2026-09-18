using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateFixedExpense;

public sealed class UpdateFixedExpenseUseCase
{
    private readonly IRecurringRuleRepository _repository;
    private readonly IValidator<UpdateFixedExpenseCommand> _validator;
    private readonly ILogger<UpdateFixedExpenseUseCase> _logger;

    public UpdateFixedExpenseUseCase(IRecurringRuleRepository repository, IValidator<UpdateFixedExpenseCommand> validator, ILogger<UpdateFixedExpenseUseCase> logger)
    {
        (_repository, _validator, _logger) = (repository, validator, logger);
    }

    public async Task<Result> ExecuteAsync(UpdateFixedExpenseCommand command, CancellationToken ct = default)
    {
        FluentValidation.Results.ValidationResult validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            return Result.Failure("Validation", "The fixed expense command is invalid.", validation.Errors
            .GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        RecurringRule? rule = await _repository.GetByIdAsync(command.Id, ct).ConfigureAwait(false);
        if (rule is not FixedExpense)
        {
            return Result.Failure("NotFound", $"Fixed expense {command.Id} was not found.");
        }

        try
        {
            rule.Update(command.Name, Money.Create(command.Amount, command.Currency), command.Currency, command.Frequency,
                command.StartDate, command.EndDate, command.CategoryId, command.PaymentMethodId, command.TagIds);
            return await _repository.UpdateAsync(rule, ct).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while updating fixed expense.");
            return Result.Failure("DomainInvariant", ex.Message);
        }
    }
}
