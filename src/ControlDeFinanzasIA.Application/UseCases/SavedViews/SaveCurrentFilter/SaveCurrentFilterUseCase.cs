using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.SavedViews.SaveCurrentFilter;

/// <summary>
/// Persists the current filter as a named <see cref="SavedView"/>.
/// </summary>
/// <remarks>
/// The caller receives a <see cref="Result"/> whose <see cref="Result.Value"/>
/// can be cast to <see cref="SavedView"/>.
/// </remarks>
public sealed class SaveCurrentFilterUseCase
{
    private readonly ISavedViewRepository _savedViewRepository;
    private readonly IClock _clock;
    private readonly IValidator<SaveCurrentFilterCommand> _validator;
    private readonly ILogger<SaveCurrentFilterUseCase> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveCurrentFilterUseCase"/> class.
    /// </summary>
    public SaveCurrentFilterUseCase(
        ISavedViewRepository savedViewRepository,
        IClock clock,
        IValidator<SaveCurrentFilterCommand> validator,
        ILogger<SaveCurrentFilterUseCase> logger)
    {
        _savedViewRepository = savedViewRepository;
        _clock = clock;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Executes the save operation.
    /// </summary>
    /// <param name="command">Command containing the name, entity type and filter.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A <see cref="Result"/> with the persisted <see cref="SavedView"/> or a failure.</returns>
    public async Task<Result> ExecuteAsync(SaveCurrentFilterCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidationResult validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            IReadOnlyDictionary<string, string[]> details = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return Result.Failure(
                "Validation",
                "El comando para guardar la vista no es válido.",
                details);
        }

        string filterJson;
        switch (command.Filter)
        {
            case ExpenseFilter expenseFilter when command.EntityType == TransactionType.Expense:
                filterJson = FilterJsonSerializer.Serialize(expenseFilter);
                break;
            case IncomeFilter incomeFilter when command.EntityType == TransactionType.Income:
                filterJson = FilterJsonSerializer.Serialize(incomeFilter);
                break;
            case ExpenseFilter or IncomeFilter:
                return Result.Failure(
                    "Validation",
                    "El tipo del filtro no coincide con el tipo de entidad de la vista.");
            default:
                return Result.Failure(
                    "Validation",
                    $"Unsupported filter type: {command.Filter.GetType()}.");
        }

        SavedView savedView;
        try
        {
            savedView = SavedView.Create(
                command.Name.Trim(),
                command.EntityType,
                filterJson,
                _clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while saving saved view.");
            return Result.Failure("DomainInvariant", ex.Message);
        }

        Result addResult = await _savedViewRepository.AddAsync(savedView, ct);
        if (!addResult.IsSuccess)
        {
            return addResult;
        }

        return Result.Success(savedView);
    }
}
