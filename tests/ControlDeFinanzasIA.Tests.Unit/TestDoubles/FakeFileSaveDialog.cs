using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Configurable <see cref="IFileSaveDialog"/> double (F-018). Returns a configured target (or
/// <see langword="null"/> to simulate a cancel), counts calls and can block on a gate.
/// </summary>
public sealed class FakeFileSaveDialog : IFileSaveDialog
{
    /// <summary>Gets or sets the target handed back by <see cref="PickAsync"/>; null simulates a cancel.</summary>
    public SaveTargets.RecordingSaveTarget? Result { get; set; }

    /// <summary>Gets or sets a task that <see cref="PickAsync"/> waits for before returning.</summary>
    public Task? Gate { get; set; }

    /// <summary>Gets or sets an exception that <see cref="PickAsync"/> throws.</summary>
    public Exception? ThrowWith { get; set; }

    public int CallCount { get; private set; }

    public string? LastSuggestedFileName { get; private set; }

    public ReportFormat? LastFormat { get; private set; }

    public async Task<SaveTarget?> PickAsync(string suggestedFileName, ReportFormat format, CancellationToken ct = default)
    {
        CallCount++;
        LastSuggestedFileName = suggestedFileName;
        LastFormat = format;

        if (Gate is not null)
        {
            await Gate;
        }

        if (ThrowWith is not null)
        {
            throw ThrowWith;
        }

        return Result?.Target;
    }
}
