using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.SavedViews.GetSavedViews;

/// <summary>
/// Request to list saved views. When <see cref="EntityType"/> is null,
/// all views are returned; otherwise only views of that entity type.
/// </summary>
public sealed record GetSavedViewsQuery(
    /// <summary>
    /// Optional entity type filter.
    /// </summary>
    TransactionType? EntityType = null);
