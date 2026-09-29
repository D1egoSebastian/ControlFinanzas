namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Asks the user where to save a report (F-018, D-1). Implemented in the UI layer.
/// </summary>
public interface IFileSaveDialog
{
    /// <summary>Shows the save dialog.</summary>
    /// <returns>The chosen target, or <c>null</c> when the user cancelled. Cancelling is not an error.</returns>
    Task<SaveTarget?> PickAsync(
        string suggestedFileName,
        ReportFormat format,
        CancellationToken ct = default);
}
