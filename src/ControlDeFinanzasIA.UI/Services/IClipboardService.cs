namespace ControlDeFinanzasIA.UI.Services;

/// <summary>
/// UI-layer port over the OS clipboard.
/// </summary>
/// <remarks>
/// F-015 (D-8/D-15, R-13): kept in <c>UI/Services</c> rather than an Application abstraction
/// because it wraps Avalonia's <c>TopLevel.Clipboard</c> API directly — an Application-layer
/// interface must stay free of any UI framework type (R-4).
/// </remarks>
public interface IClipboardService
{
    /// <summary>
    /// Copies <paramref name="text"/> to the OS clipboard.
    /// </summary>
    /// <param name="text">The exact text to copy.</param>
    Task SetTextAsync(string text);
}
