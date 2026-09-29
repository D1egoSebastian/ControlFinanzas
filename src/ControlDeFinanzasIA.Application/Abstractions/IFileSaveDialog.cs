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

    /// <summary>Shows the save dialog for an arbitrary file type (F-019, DD-1).</summary>
    /// <param name="suggestedFileName">File name proposed to the user.</param>
    /// <param name="filter">File type the dialog offers.</param>
    /// <param name="title">Optional dialog title; <c>null</c> lets the implementation use its default.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The chosen target, or <c>null</c> when the user cancelled. Cancelling is not an error.</returns>
    Task<SaveTarget?> PickAsync(
        string suggestedFileName,
        FileTypeFilter filter,
        string? title = null,
        CancellationToken ct = default);
}
