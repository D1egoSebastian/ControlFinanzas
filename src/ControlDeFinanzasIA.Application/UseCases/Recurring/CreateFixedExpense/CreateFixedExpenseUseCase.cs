using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.CreateFixedExpense;

public sealed class CreateFixedExpenseUseCase
{
    private readonly IRecurringRuleRepository _repository;
    private readonly IValidator<CreateFixedExpenseCommand> _validator;
    private readonly ILogger<CreateFixedExpenseUseCase> _logger;

    public CreateFixedExpenseUseCase(IRecurringRuleRepository repository, IValidator<CreateFixedExpenseCommand> validator, ILogger<CreateFixedExpenseUseCase> logger)
    {
        (_repository, _validator, _logger) = (repository, validator, logger);
    }

    public async Task<Result> ExecuteAsync(CreateFixedExpenseCommand command, CancellationToken ct = default)
    {
        FluentValidation.Results.ValidationResult validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            return Result.Failure("Validation", "The fixed expense command is invalid.", validation.Errors
                .GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        try
        {
            FixedExpense rule = FixedExpenseFactory.Create(command);
            Result result = await _repository.AddAsync(rule, ct).ConfigureAwait(false);
            return result.IsSuccess ? Result.Success(rule) : result;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while creating fixed expense.");
            return Result.Failure("DomainInvariant", ex.Message);
        }
    }

    private static class FixedExpenseFactory
    {
        public static FixedExpense Create(CreateFixedExpenseCommand c)
        {
            Subscription subscription = RecurringRule.Create(c.Name, Money.Create(c.Amount, c.Currency), c.Currency,
                c.Frequency, c.StartDate, c.EndDate, c.CategoryId, c.PaymentMethodId, c.TagIds, c.NextChargeDate);
            return new FixedExpense(subscription.Id, subscription.Name, subscription.Amount, subscription.Currency,
                subscription.Frequency, subscription.StartDate, subscription.EndDate, subscription.CategoryId,
                subscription.PaymentMethodId, subscription.TagIds, subscription.Active, subscription.NextGenerationDate);
        }
    }
}
