using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateSubscription;

public sealed class UpdateSubscriptionUseCase
{
    private readonly IRecurringRuleRepository _repository;
    private readonly IValidator<UpdateSubscriptionCommand> _validator;
    private readonly ILogger<UpdateSubscriptionUseCase> _logger;

    public UpdateSubscriptionUseCase(
        IRecurringRuleRepository repository,
        IValidator<UpdateSubscriptionCommand> validator,
        ILogger<UpdateSubscriptionUseCase> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result> ExecuteAsync(
        UpdateSubscriptionCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        FluentValidation.Results.ValidationResult validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            IReadOnlyDictionary<string, string[]> details = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray(), StringComparer.Ordinal);
            return Result.Failure("Validation", "The subscription command is invalid.", details);
        }

        RecurringRule? rule = await _repository.GetByIdAsync(command.Id, ct).ConfigureAwait(false);
        if (rule is not Subscription)
        {
            return Result.Failure("NotFound", $"Subscription {command.Id} was not found.");
        }

        try
        {
            rule.Update(
                command.Name,
                Money.Create(command.Amount, command.Currency),
                command.Currency,
                command.Frequency,
                command.StartDate,
                command.EndDate,
                command.CategoryId,
                command.PaymentMethodId,
                command.TagIds);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while updating subscription.");
            return Result.Failure("DomainInvariant", ex.Message);
        }

        return await _repository.UpdateAsync(rule, ct).ConfigureAwait(false);
    }
}
