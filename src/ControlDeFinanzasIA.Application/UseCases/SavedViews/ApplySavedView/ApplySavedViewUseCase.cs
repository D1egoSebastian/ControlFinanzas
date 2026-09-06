using System.Text.Json;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.SavedViews.ApplySavedView;

/// <summary>
/// Retrieves a saved view and returns its entity type plus the
/// deserialized filter ready to be applied.
/// </summary>
/// <remarks>
/// The caller receives a <see cref="Result"/> whose <see cref="Result.Value"/>
/// can be cast to <see cref="ApplySavedViewResult"/>.
/// </remarks>
public sealed class ApplySavedViewUseCase
{
    private readonly ISavedViewRepository _savedViewRepository;
    private readonly ILogger<ApplySavedViewUseCase> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplySavedViewUseCase"/> class.
    /// </summary>
    public ApplySavedViewUseCase(
        ISavedViewRepository savedViewRepository,
        ILogger<ApplySavedViewUseCase> logger)
    {
        _savedViewRepository = savedViewRepository;
        _logger = logger;
    }

    /// <summary>
    /// Executes the apply operation.
    /// </summary>
    /// <param name="query">Query containing the saved view identifier.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A <see cref="Result"/> with the <see cref="ApplySavedViewResult"/> or a failure.</returns>
    public async Task<Result> ExecuteAsync(ApplySavedViewQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        SavedView? savedView = await _savedViewRepository.GetByIdAsync(query.SavedViewId, ct);
        if (savedView is null)
        {
            return Result.Failure("NotFound", $"Vista guardada {query.SavedViewId} no encontrada.");
        }

        try
        {
            object filter = savedView.EntityType switch
            {
                TransactionType.Expense => FilterJsonSerializer.Deserialize<ExpenseFilter>(savedView.FilterJson),
                TransactionType.Income => FilterJsonSerializer.Deserialize<IncomeFilter>(savedView.FilterJson),
                _ => throw new InvalidOperationException($"Unknown transaction type: {savedView.EntityType}."),
            };

            return Result.Success(new ApplySavedViewResult(savedView.EntityType, filter));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Corrupted filter JSON in SavedView {Id}", savedView.Id);
            return Result.Failure("CorruptedFilter", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid filter envelope in SavedView {Id}", savedView.Id);
            return Result.Failure("IncompatibleVersion", ex.Message);
        }
    }
}
