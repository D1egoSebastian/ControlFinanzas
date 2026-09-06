using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.SavedViews.DeleteSavedView;

/// <summary>
/// Use case that deletes a saved view by delegating to the repository.
/// </summary>
/// <remarks>
/// This use case does not use FluentValidation; the only input guard is an
/// inline check for <see cref="Guid.Empty"/>. The repository result is
/// propagated verbatim.
/// </remarks>
public sealed class DeleteSavedViewUseCase
{
    private readonly ISavedViewRepository _savedViewRepository;
    private readonly ILogger<DeleteSavedViewUseCase> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteSavedViewUseCase"/> class.
    /// </summary>
    /// <param name="savedViewRepository">Repository used to delete saved views.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public DeleteSavedViewUseCase(
        ISavedViewRepository savedViewRepository,
        ILogger<DeleteSavedViewUseCase> logger)
    {
        _savedViewRepository = savedViewRepository;
        _logger = logger;
    }

    /// <summary>
    /// Executes the delete-saved-view workflow.
    /// </summary>
    /// <param name="command">The command describing the saved view to delete.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>
    /// A <see cref="Result"/> that is successful when the repository deletion succeeds,
    /// or failed with a business error (validation or repository error).
    /// </returns>
    public async Task<Result> ExecuteAsync(DeleteSavedViewCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.SavedViewId == Guid.Empty)
        {
            return Result.Failure("Validation", "Id cannot be empty.");
        }

        return await _savedViewRepository.RemoveAsync(command.SavedViewId, ct);
    }
}
