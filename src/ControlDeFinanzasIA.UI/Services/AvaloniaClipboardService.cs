using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.Services;

/// <summary>
/// <see cref="IClipboardService"/> implementation backed by Avalonia's
/// <see cref="TopLevel.Clipboard"/>.
/// </summary>
/// <remarks>
/// F-015 (D-8, R-13). Resolved as a Singleton (see <c>Bootstrap.cs</c>); it holds no state of
/// its own and reads the live main window on every call, so a Singleton lifetime does not risk
/// pinning a stale window reference.
/// </remarks>
public sealed class AvaloniaClipboardService : IClipboardService
{
    private readonly ILogger<AvaloniaClipboardService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaClipboardService"/> class.
    /// </summary>
    /// <param name="logger">Logger used to trace when no clipboard is available.</param>
    public AvaloniaClipboardService(ILogger<AvaloniaClipboardService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SetTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        IClipboard? clipboard = ResolveClipboard();
        if (clipboard is null)
        {
            _logger.LogDebug("No clipboard is available; SetTextAsync is a no-op.");
            return;
        }

        await clipboard.SetTextAsync(text).ConfigureAwait(false);
    }

    private static IClipboard? ResolveClipboard()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
        {
            return null;
        }

        Window? mainWindow = lifetime.MainWindow;
        if (mainWindow is null)
        {
            return null;
        }

        return TopLevel.GetTopLevel(mainWindow)?.Clipboard;
    }
}
