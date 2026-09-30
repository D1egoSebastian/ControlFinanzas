using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.UI.Resources;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.Services;

/// <summary>
/// <see cref="IFileOpenDialog"/> implementation backed by Avalonia's
/// <see cref="IStorageProvider.OpenFilePickerAsync"/> (F-019, DD-8).
/// </summary>
/// <remarks>
/// Resolved as a Singleton; like <see cref="AvaloniaFileSaveDialog"/> it holds no window reference
/// and reads the live main window on every call, so it must run on the UI thread (ViewModel
/// commands already do). There is no automated test (no <see cref="TopLevel"/> in unit tests);
/// it is covered by the manual check recorded in the F-019 tasks. When the caller passes no
/// title the dialog uses the localized <see cref="Strings.BackupRestoreDialogTitle"/>.
/// </remarks>
public sealed class AvaloniaFileOpenDialog : IFileOpenDialog
{
    private readonly ILogger<AvaloniaFileOpenDialog> _logger;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaFileOpenDialog"/> class.</summary>
    /// <param name="logger">Logger used to trace when no window is available.</param>
    public AvaloniaFileOpenDialog(ILogger<AvaloniaFileOpenDialog> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <inheritdoc />
    public Task<OpenTarget?> PickAsync(FileTypeFilter filter, string? title = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return PickAsync([filter], title, ct);
    }

    /// <inheritdoc />
    public async Task<OpenTarget?> PickAsync(IReadOnlyList<FileTypeFilter> filters, string? title = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filters);

        IReadOnlyList<FilePickerFileType> fileTypes = BuildFileTypes(filters, Strings.FileDialogAllSupportedTypes);

        IStorageProvider? storage = ResolveStorageProvider();
        if (storage is null)
        {
            _logger.LogWarning("No window is available to show the open dialog; the operation is skipped.");
            return null;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title ?? Strings.BackupRestoreDialogTitle,
            AllowMultiple = false,
            FileTypeFilter = fileTypes,
        });

        // No file means the user cancelled: not an error.
        if (files.Count == 0)
        {
            return null;
        }

        IStorageFile file = files[0];
        return new OpenTarget(file.Name, _ => file.OpenReadAsync(), owner: file);
    }

    /// <summary>
    /// Builds the entries of the native picker's type list (F-020, R-23). One filter yields exactly that
    /// entry (the Phase 19 behavior); several filters yield an "All supported files" entry first, holding
    /// every extension pattern and MIME type once, followed by the individual entries in order.
    /// </summary>
    /// <param name="filters">The offered file types; must not be empty.</param>
    /// <param name="allSupportedLabel">Localized name of the combined entry.</param>
    /// <returns>The picker entries.</returns>
    /// <exception cref="ArgumentException"><paramref name="filters"/> is empty.</exception>
    internal static IReadOnlyList<FilePickerFileType> BuildFileTypes(IReadOnlyList<FileTypeFilter> filters, string allSupportedLabel)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.Count == 0)
        {
            throw new ArgumentException("At least one file type filter is required.", nameof(filters));
        }

        var types = new List<FilePickerFileType>(filters.Count + 1);
        if (filters.Count > 1)
        {
            types.Add(new FilePickerFileType(allSupportedLabel)
            {
                Patterns = filters.Select(f => $"*.{f.Extension.ToLowerInvariant()}").Distinct(StringComparer.Ordinal).ToArray(),
                MimeTypes = filters.Select(f => f.MimeType).Distinct(StringComparer.Ordinal).ToArray(),
            });
        }

        types.AddRange(filters.Select(f => new FilePickerFileType(f.DisplayName)
        {
            Patterns = [$"*.{f.Extension}"],
            MimeTypes = [f.MimeType],
        }));

        return types;
    }

    private static IStorageProvider? ResolveStorageProvider()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
        {
            return null;
        }

        Window? mainWindow = lifetime.MainWindow;
        return mainWindow is null ? null : TopLevel.GetTopLevel(mainWindow)?.StorageProvider;
    }
}
