using ControlDeFinanzasIA.UI.Services;

namespace ControlDeFinanzasIA.Tests.Integration.TestDoubles;

/// <summary>Clipboard double that records every text it is given (no OS clipboard in tests).</summary>
internal sealed class RecordingClipboardService : IClipboardService
{
    /// <summary>Gets the texts copied so far, in order.</summary>
    public List<string> Calls { get; } = [];

    /// <inheritdoc />
    public Task SetTextAsync(string text)
    {
        Calls.Add(text);
        return Task.CompletedTask;
    }
}
