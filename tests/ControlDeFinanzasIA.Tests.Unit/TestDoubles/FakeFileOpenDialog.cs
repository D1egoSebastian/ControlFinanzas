using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>Configurable <see cref="IFileOpenDialog"/> double (F-019).</summary>
public sealed class FakeFileOpenDialog : IFileOpenDialog
{
    /// <summary>Target returned by the dialog; <c>null</c> simulates a cancelled dialog.</summary>
    public OpenTarget? Result { get; set; }

    /// <summary>When set, every call awaits it before returning.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Number of <see cref="PickAsync(FileTypeFilter, string?, CancellationToken)"/> and list-overload calls.</summary>
    public int Calls { get; private set; }

    /// <summary>Filter received by the last call (the first one when several were offered).</summary>
    public FileTypeFilter? LastFilter { get; private set; }

    /// <summary>Every filter received by the last call, in order (F-020).</summary>
    public IReadOnlyList<FileTypeFilter> LastFilters { get; private set; } = [];

    /// <summary>Title received by the last call.</summary>
    public string? LastTitle { get; private set; }

    /// <inheritdoc />
    public Task<OpenTarget?> PickAsync(FileTypeFilter filter, string? title = null, CancellationToken ct = default)
        => PickAsync([filter], title, ct);

    /// <inheritdoc />
    public async Task<OpenTarget?> PickAsync(IReadOnlyList<FileTypeFilter> filters, string? title = null, CancellationToken ct = default)
    {
        Calls++;
        LastFilters = filters.ToArray();
        LastFilter = filters[0];
        LastTitle = title;
        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(ct);
        }

        return Result;
    }
}
