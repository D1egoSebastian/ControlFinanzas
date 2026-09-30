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

    /// <summary>Shows the picker offering several file types (F-020, D-20).</summary>
    /// <remarks>
    /// The default implementation offers only the first filter, so an implementer that has not overridden
    /// this member still works. Implementers that can show several types (the Avalonia dialog) override it.
    /// </remarks>
    /// <param name="filters">File types the dialog offers; must not be empty.</param>
    /// <param name="title">Optional dialog title.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The picked file, or <c>null</c> when the user cancelled.</returns>
    Task<OpenTarget?> PickAsync(IReadOnlyList<FileTypeFilter> filters, string? title = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.Count == 0)
        {
            throw new ArgumentException("At least one file type filter is required.", nameof(filters));
        }

        return PickAsync(filters[0], title, ct);
    }
}
