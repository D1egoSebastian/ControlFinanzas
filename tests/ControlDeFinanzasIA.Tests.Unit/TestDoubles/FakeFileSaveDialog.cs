using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Configurable <see cref="IFileSaveDialog"/> double (F-018, F-019). Returns a configured target (or
/// <see langword="null"/> to simulate a cancel), counts calls and can block on a gate. Both
/// overloads share the counter, the gate and the failure switch.
/// </summary>
public sealed class FakeFileSaveDialog : IFileSaveDialog
{
    /// <summary>Gets or sets the target handed back by <see cref="PickAsync(string, ReportFormat, CancellationToken)"/>; null simulates a cancel.</summary>
    public SaveTargets.RecordingSaveTarget? Result { get; set; }

    /// <summary>Gets or sets a task that <see cref="PickAsync(string, ReportFormat, CancellationToken)"/> waits for before returning.</summary>
    public Task? Gate { get; set; }

    /// <summary>Gets or sets an exception that <see cref="PickAsync(string, ReportFormat, CancellationToken)"/> throws.</summary>
    public Exception? ThrowWith { get; set; }

    public int CallCount { get; private set; }

    public string? LastSuggestedFileName { get; private set; }

    public ReportFormat? LastFormat { get; private set; }

    /// <summary>Gets the filter received by the last generic-overload call.</summary>
    public FileTypeFilter? LastFilter { get; private set; }

    /// <summary>Gets the title received by the last generic-overload call.</summary>
    public string? LastTitle { get; private set; }

    public Task<SaveTarget?> PickAsync(string suggestedFileName, ReportFormat format, CancellationToken ct = default)
    {
        LastFormat = format;
        return RunAsync(suggestedFileName);
    }

    public Task<SaveTarget?> PickAsync(string suggestedFileName, FileTypeFilter filter, string? title = null, CancellationToken ct = default)
    {
        LastFilter = filter;
        LastTitle = title;
        return RunAsync(suggestedFileName);
    }

    private async Task<SaveTarget?> RunAsync(string suggestedFileName)
    {
        CallCount++;
        LastSuggestedFileName = suggestedFileName;

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
