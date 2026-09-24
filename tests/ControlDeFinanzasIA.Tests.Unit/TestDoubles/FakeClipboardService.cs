using ControlDeFinanzasIA.UI.Services;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="IClipboardService"/> used in unit tests (F-015, slice S4).
/// Records every call so tests can assert the exact text copied, without a real OS clipboard.
/// </summary>
public sealed class FakeClipboardService : IClipboardService
{
    /// <summary>Every text value passed to <see cref="SetTextAsync"/>, in call order.</summary>
    public List<string> Calls { get; } = new();

    /// <summary>Gets the last text passed to <see cref="SetTextAsync"/>, or <see langword="null"/> if never called.</summary>
    public string? LastText => Calls.Count > 0 ? Calls[^1] : null;

    /// <inheritdoc />
    public Task SetTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Calls.Add(text);
        return Task.CompletedTask;
    }
}
