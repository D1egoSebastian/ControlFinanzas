namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Lets the user pick an existing file to read (F-019, DD-1). Implemented in the UI layer.
/// </summary>
public interface IFileOpenDialog
{
    /// <summary>Shows the picker.</summary>
    /// <param name="filter">File type the dialog offers.</param>
    /// <param name="title">Optional dialog title.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The picked file, or <c>null</c> when the user cancelled.</returns>
    Task<OpenTarget?> PickAsync(FileTypeFilter filter, string? title = null, CancellationToken ct = default);
}
