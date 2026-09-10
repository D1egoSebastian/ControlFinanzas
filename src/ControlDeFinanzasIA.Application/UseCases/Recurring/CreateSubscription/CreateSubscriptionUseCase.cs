using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.CreateSubscription;

/// <summary>
/// Command para registrar una nueva suscripcion recurrente (Netflix,
/// internet, etc.).
/// </summary>
/// <remarks>
/// Usa <see cref="Money.Create(decimal, Currency)"/> para validar
/// el monto (&gt; 0). El factory <see cref="RecurringRule.Create"/>
/// valida el resto de invariantes (name, fechas, Guids).
/// </remarks>
public sealed record CreateSubscriptionCommand(
    string Name,
    decimal Amount,
    Currency Currency,
    string Frequency,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid CategoryId,
    Guid PaymentMethodId,
    IReadOnlyList<Guid>? TagIds = null);

/// <summary>
/// Use case que registra una nueva suscripcion. Orquestra la creacion
/// de la entidad (<see cref="RecurringRule.Create"/>) y la persistencia.
/// </summary>
public sealed class CreateSubscriptionUseCase
{
    private readonly IRecurringRuleRepository _repository;
    private readonly IValidator<CreateSubscriptionCommand> _validator;
    private readonly ILogger<CreateSubscriptionUseCase> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public CreateSubscriptionUseCase(
        IRecurringRuleRepository repository,
        IValidator<CreateSubscriptionCommand> validator,
        ILogger<CreateSubscriptionUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el registro de la suscripcion.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> con el <see cref="RecurringRule"/> creado
    /// (un <see cref="Subscription"/>) en <see cref="Result.Value"/>
    /// si la operacion tiene exito; <see cref="Error"/> con codigo
    /// <c>"Validation"</c> o <c>"RepositoryError"</c> en caso contrario.
    /// </returns>
    public async Task<Result> ExecuteAsync(CreateSubscriptionCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        FluentValidation.Results.ValidationResult validation = _validator.Validate(command);
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
                "El comando para registrar la suscripcion no es valido.",
                details);
        }

        RecurringRule rule;
        try
        {
            var money = Money.Create(command.Amount, command.Currency);
            rule = RecurringRule.Create(
                name: command.Name,
                amount: money,
                currency: command.Currency,
                frequency: command.Frequency,
                startDate: command.StartDate,
                endDate: command.EndDate,
                categoryId: command.CategoryId,
                paymentMethodId: command.PaymentMethodId,
                tagIds: command.TagIds);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while creating subscription.");
            return Result.Failure("DomainInvariant", ex.Message);
        }

        Result addResult = await _repository.AddAsync(rule, ct).ConfigureAwait(false);
        if (!addResult.IsSuccess)
        {
            return addResult;
        }

        return Result.Success(rule);
    }
}
