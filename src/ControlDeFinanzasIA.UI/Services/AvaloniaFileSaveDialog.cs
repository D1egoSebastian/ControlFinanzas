using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.UI.Resources;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.Services;

/// <summary>
/// <see cref="IFileSaveDialog"/> implementation backed by Avalonia's
/// <see cref="IStorageProvider.SaveFilePickerAsync"/> (F-018, D-8).
/// </summary>
/// <remarks>
/// Resolved as a Singleton; like <see cref="AvaloniaClipboardService"/> it holds no window
/// reference and reads the live main window on every call. It must be called on the UI thread,
/// which ViewModel commands already are. There is no automated test (no <see cref="TopLevel"/>
/// in unit tests); it is covered by the manual check recorded in the F-018 tasks.
/// </remarks>
public sealed class AvaloniaFileSaveDialog : IFileSaveDialog
{
    private const string _pdfMimeType = "application/pdf";
    private const string _xlsxMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly ILogger<AvaloniaFileSaveDialog> _logger;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaFileSaveDialog"/> class.</summary>
    /// <param name="logger">Logger used to trace when no window is available.</param>
    public AvaloniaFileSaveDialog(ILogger<AvaloniaFileSaveDialog> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SaveTarget?> PickAsync(string suggestedFileName, ReportFormat format, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);

        IStorageProvider? storage = ResolveStorageProvider();
        if (storage is null)
        {
            _logger.LogWarning("No window is available to show the save dialog; the export is skipped.");
            return null;
        }

        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.ExportDialogTitle,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = format == ReportFormat.Pdf ? "pdf" : "xlsx",
            ShowOverwritePrompt = true,
            FileTypeChoices = [CreateFileType(format)],
        });

        // A null file means the user cancelled: not an error.
        return file is null
            ? null
            : new SaveTarget(file.Name, _ => file.OpenWriteAsync(), owner: file);
    }

    private static FilePickerFileType CreateFileType(ReportFormat format) => format == ReportFormat.Pdf
        ? new FilePickerFileType(Strings.ExportPdfFileTypeName)
        {
            Patterns = ["*.pdf"],
            MimeTypes = [_pdfMimeType],
        }
        : new FilePickerFileType(Strings.ExportExcelFileTypeName)
        {
            Patterns = ["*.xlsx"],
            MimeTypes = [_xlsxMimeType],
        };

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
