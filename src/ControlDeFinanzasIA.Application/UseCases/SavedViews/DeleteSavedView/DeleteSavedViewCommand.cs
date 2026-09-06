namespace ControlDeFinanzasIA.Application.UseCases.SavedViews.DeleteSavedView;

/// <summary>
/// Command that identifies the saved view to delete.
/// </summary>
/// <param name="SavedViewId">Unique identifier of the saved view.</param>
public sealed record DeleteSavedViewCommand(Guid SavedViewId);
