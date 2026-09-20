using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Budgets.CreateOrUpdateBudget;

/// <summary>
/// Use case que crea o actualiza (upsert) el presupuesto de una
/// categoría en un mes calendario (R-1, R-2). El upsert se resuelve
/// aquí (query-then-update-or-insert sobre Year+Month+CategoryId) y
/// también está respaldado por un índice único en la base de datos
/// (D-4), para que la invariante se mantenga aunque un futuro caller
/// bypasse este use case.
/// </summary>
public sealed class CreateOrUpdateBudgetUseCase
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IValidator<CreateOrUpdateBudgetCommand> _validator;
    private readonly ILogger<CreateOrUpdateBudgetUseCase> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public CreateOrUpdateBudgetUseCase(
        IBudgetRepository budgetRepository,
        ICategoryRepository categoryRepository,
        IValidator<CreateOrUpdateBudgetCommand> validator,
        ILogger<CreateOrUpdateBudgetUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(budgetRepository);
        ArgumentNullException.ThrowIfNull(categoryRepository);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(logger);

        _budgetRepository = budgetRepository;
        _categoryRepository = categoryRepository;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el upsert del presupuesto.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> con el <see cref="Budget"/> creado o
    /// actualizado en <see cref="Result.Value"/> si la operación tiene
    /// éxito; <see cref="Error"/> con código <c>"Validation"</c>,
    /// <c>"CategoryNotFound"</c> o <c>"DomainInvariant"</c> en caso
    /// contrario.
    /// </returns>
    public async Task<Result> ExecuteAsync(CreateOrUpdateBudgetCommand command, CancellationToken ct = default)
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
                "El comando para guardar el presupuesto no es válido.",
                details);
        }

        Guid categoryId = command.CategoryId!.Value;
        Category? category = await _categoryRepository.GetByIdAsync(categoryId, ct).ConfigureAwait(false);
        if (category is null)
        {
            return Result.Failure("CategoryNotFound", $"La categoría {categoryId} no existe.");
        }

        Budget? existing = await _budgetRepository
            .GetByCategoryAndMonthAsync(categoryId, command.Year, command.Month, ct)
            .ConfigureAwait(false);

        Budget budget;
        try
        {
            var amount = Money.Create(command.Amount, command.Currency);
            if (existing is null)
            {
                budget = Budget.Create(command.Year, command.Month, categoryId, amount);
                Result addResult = await _budgetRepository.AddAsync(budget, ct).ConfigureAwait(false);
                if (!addResult.IsSuccess)
                {
                    return addResult;
                }
            }
            else
            {
                existing.ChangeAmount(amount);
                budget = existing;
                Result updateResult = await _budgetRepository.UpdateAsync(budget, ct).ConfigureAwait(false);
                if (!updateResult.IsSuccess)
                {
                    return updateResult;
                }
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while saving budget.");
            return Result.Failure("DomainInvariant", ex.Message);
        }

        return Result.Success(budget);
    }
}
