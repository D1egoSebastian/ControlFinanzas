namespace ControlDeFinanzasIA.Application.UseCases.SavedViews.ApplySavedView;

/// <summary>
/// Identifies the saved view to apply.
/// </summary>
public sealed record ApplySavedViewQuery(
    /// <summary>
    /// Unique identifier of the saved view.
    /// </summary>
    Guid SavedViewId);
