using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Goals.CreateGoal;

/// <summary>
/// Use case que crea una nueva meta de ahorro (R-1, R-2, D-5). Clona
/// la forma de <c>CreateOrUpdateBudgetUseCase</c>, sin el paso de
/// existencia de categoría: una meta no referencia ningún otro
/// agregado.
/// </summary>
public sealed class CreateGoalUseCase
{
    private readonly IGoalRepository _goalRepository;
    private readonly IValidator<CreateGoalCommand> _validator;
    private readonly ILogger<CreateGoalUseCase> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public CreateGoalUseCase(
        IGoalRepository goalRepository,
        IValidator<CreateGoalCommand> validator,
        ILogger<CreateGoalUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(goalRepository);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(logger);

        _goalRepository = goalRepository;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Crea una nueva meta de ahorro.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> con el <see cref="Goal"/> creado en
    /// <see cref="Result.Value"/> si la operación tiene éxito;
    /// <see cref="Error"/> con código <c>"Validation"</c> o
    /// <c>"DomainInvariant"</c> en caso contrario.
    /// </returns>
    public async Task<Result> ExecuteAsync(CreateGoalCommand command, CancellationToken ct = default)
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
                "El comando para crear la meta no es válido.",
                details);
        }

        Goal goal;
        try
        {
            var amount = Money.Create(command.TargetAmount, command.Currency);
            goal = Goal.Create(command.Name, amount, command.TargetDate, command.Note);

            Result addResult = await _goalRepository.AddAsync(goal, ct).ConfigureAwait(false);
            if (!addResult.IsSuccess)
            {
                return addResult;
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while creating goal.");
            return Result.Failure("DomainInvariant", ex.Message);
        }

        return Result.Success(goal);
    }
}
