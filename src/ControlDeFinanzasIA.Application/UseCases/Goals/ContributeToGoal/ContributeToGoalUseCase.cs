using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Goals.ContributeToGoal;

/// <summary>
/// Use case que registra un aporte manual a una meta de ahorro
/// existente (R-3, R-4, R-6, D-6). Este es el único punto donde la
/// invariante de moneda (Q1) se aplica de verdad: el constructor toma
/// ÚNICAMENTE <see cref="IGoalRepository"/> y
/// <see cref="IGoalContributionRepository"/> — sin
/// <c>IExpenseRepository</c>, <c>IIncomeRepository</c> ni
/// <c>ICurrencyConverter</c> — para que un aporte a una meta nunca
/// pueda crear un <c>Expense</c>/<c>Income</c> (R-6) ni convertir
/// silenciosamente una moneda (R-4).
/// </summary>
public sealed class ContributeToGoalUseCase
{
    private readonly IGoalRepository _goalRepository;
    private readonly IGoalContributionRepository _contributionRepository;
    private readonly IValidator<ContributeToGoalCommand> _validator;
    private readonly ILogger<ContributeToGoalUseCase> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public ContributeToGoalUseCase(
        IGoalRepository goalRepository,
        IGoalContributionRepository contributionRepository,
        IValidator<ContributeToGoalCommand> validator,
        ILogger<ContributeToGoalUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(goalRepository);
        ArgumentNullException.ThrowIfNull(contributionRepository);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(logger);

        _goalRepository = goalRepository;
        _contributionRepository = contributionRepository;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Registra un aporte a la meta indicada por
    /// <see cref="ContributeToGoalCommand.GoalId"/>, y auto-completa la
    /// meta (R-8) si el aporte la lleva al objetivo.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> con la <see cref="GoalContribution"/> creada
    /// en <see cref="Result.Value"/> si la operación tiene éxito;
    /// <see cref="Error"/> con código <c>"Validation"</c>,
    /// <c>"GoalNotFound"</c>, <c>"CurrencyMismatch"</c> o
    /// <c>"DomainInvariant"</c> en caso contrario.
    /// </returns>
    public async Task<Result> ExecuteAsync(ContributeToGoalCommand command, CancellationToken ct = default)
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
                "El comando para registrar el aporte no es válido.",
                details);
        }

        Goal? goal = await _goalRepository.GetByIdAsync(command.GoalId, ct).ConfigureAwait(false);
        if (goal is null)
        {
            return Result.Failure("GoalNotFound", $"La meta {command.GoalId} no existe.");
        }

        // Currency gate (Q1 / R-4) — BEFORE any Money/GoalContribution construction.
        // It sits after the goal load (it needs goal.Currency) and before step 5 so
        // an invalid contribution is never materialised.
        if (command.Currency != goal.Currency)
        {
            return Result.Failure(
                "CurrencyMismatch",
                $"El aporte está en {command.Currency} pero la meta '{goal.Name}' es en {goal.Currency}. " +
                "Registra el aporte en la moneda de la meta.");
        }

        GoalContribution contribution;
        try
        {
            var amount = Money.Create(command.Amount, command.Currency);
            contribution = GoalContribution.Create(goal.Id, amount, command.Date);

            Result addResult = await _contributionRepository.AddAsync(contribution, ct).ConfigureAwait(false);
            if (!addResult.IsSuccess)
            {
                return addResult;
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while contributing to goal.");
            return Result.Failure("DomainInvariant", ex.Message);
        }

        // Auto-complete, after the add so the new contribution counts.
        IReadOnlyList<GoalContribution> all = await _contributionRepository
            .GetByGoalIdAsync(goal.Id, ct)
            .ConfigureAwait(false);

        if (goal.TryAutoComplete(all))
        {
            Result updateResult = await _goalRepository.UpdateAsync(goal, ct).ConfigureAwait(false);
            if (!updateResult.IsSuccess)
            {
                return updateResult;
            }
        }

        return Result.Success(contribution);
    }
}
