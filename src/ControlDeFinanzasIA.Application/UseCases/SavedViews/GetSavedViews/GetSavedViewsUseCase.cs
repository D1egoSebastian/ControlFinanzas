using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;

namespace ControlDeFinanzasIA.Application.UseCases.SavedViews.GetSavedViews;

/// <summary>
/// Returns all saved views, optionally filtered by entity type.
/// </summary>
/// <remarks>
/// The caller receives a <see cref="Result"/> whose <see cref="Result.Value"/>
/// can be cast to <see cref="IReadOnlyList{T}"/> of <see cref="SavedView"/>.
/// </remarks>
public sealed class GetSavedViewsUseCase
{
    private readonly ISavedViewRepository _savedViewRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetSavedViewsUseCase"/> class.
    /// </summary>
    public GetSavedViewsUseCase(ISavedViewRepository savedViewRepository)
    {
        _savedViewRepository = savedViewRepository;
    }

    /// <summary>
    /// Executes the query.
    /// </summary>
    /// <param name="query">Query containing the optional entity type filter.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A <see cref="Result"/> with the list of saved views.</returns>
    public async Task<Result> ExecuteAsync(GetSavedViewsQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<SavedView> views = query.EntityType is { } et
            ? await _savedViewRepository.GetByEntityTypeAsync(et, ct)
            : await _savedViewRepository.GetAllAsync(ct);

        return Result.Success(views);
    }
}
