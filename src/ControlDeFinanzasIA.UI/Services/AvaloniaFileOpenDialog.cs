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
    public async Task<OpenTarget?> PickAsync(FileTypeFilter filter, string? title = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        IStorageProvider? storage = ResolveStorageProvider();
        if (storage is null)
        {
            _logger.LogWarning("No window is available to show the open dialog; the restore is skipped.");
            return null;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title ?? Strings.BackupRestoreDialogTitle,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(filter.DisplayName)
                {
                    Patterns = [$"*.{filter.Extension}"],
                    MimeTypes = [filter.MimeType],
                },
            ],
        });

        // No file means the user cancelled: not an error.
        if (files.Count == 0)
        {
            return null;
        }

        IStorageFile file = files[0];
        return new OpenTarget(file.Name, _ => file.OpenReadAsync(), owner: file);
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
