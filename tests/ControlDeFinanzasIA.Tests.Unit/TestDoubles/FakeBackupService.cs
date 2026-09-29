using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Configurable <see cref="IBackupService"/> double (F-019). Records calls per member,
/// returns configured results, can throw, and can block on a gate.
/// </summary>
public sealed class FakeBackupService : IBackupService
{
    /// <summary>Result returned by <see cref="CreateAsync"/>.</summary>
    public Result CreateResult { get; set; } = Result.Success(new object());

    /// <summary>Result returned by <see cref="InspectAsync"/>.</summary>
    public Result InspectResult { get; set; } = Result.Success(new object());

    /// <summary>Result returned by <see cref="RestoreAsync"/>.</summary>
    public Result RestoreResult { get; set; } = Result.Success(new object());

    /// <summary>When set, <see cref="CreateAsync"/> throws it.</summary>
    public Exception? CreateException { get; set; }

    /// <summary>When set, <see cref="InspectAsync"/> throws it.</summary>
    public Exception? InspectException { get; set; }

    /// <summary>When set, <see cref="RestoreAsync"/> throws it.</summary>
    public Exception? RestoreException { get; set; }

    /// <summary>When set, every async member awaits it before returning.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Number of <see cref="CreateAsync"/> calls.</summary>
    public int CreateCalls { get; private set; }

    /// <summary>Number of <see cref="InspectAsync"/> calls.</summary>
    public int InspectCalls { get; private set; }

    /// <summary>Number of <see cref="RestoreAsync"/> calls.</summary>
    public int RestoreCalls { get; private set; }

    /// <summary>Number of <see cref="CompletePendingRestore"/> calls.</summary>
    public int CompletePendingRestoreCalls { get; private set; }

    /// <summary>Delegate received by the last <see cref="CreateAsync"/> call.</summary>
    public Func<CancellationToken, Task<Stream>>? LastOpenDestination { get; private set; }

    /// <summary>Delegate received by the last <see cref="InspectAsync"/> or <see cref="RestoreAsync"/> call.</summary>
    public Func<CancellationToken, Task<Stream>>? LastOpenRead { get; private set; }

    /// <summary>Cancellation token received by the last <see cref="CreateAsync"/> call.</summary>
    public CancellationToken LastCreateToken { get; private set; }

    /// <inheritdoc />
    public async Task<Result> CreateAsync(Func<CancellationToken, Task<Stream>> openDestination, CancellationToken ct = default)
    {
        CreateCalls++;
        LastOpenDestination = openDestination;
        LastCreateToken = ct;
        await WaitAsync(ct);
        return CreateException is not null ? throw CreateException : CreateResult;
    }

    /// <inheritdoc />
    public async Task<Result> InspectAsync(Func<CancellationToken, Task<Stream>> openRead, CancellationToken ct = default)
    {
        InspectCalls++;
        LastOpenRead = openRead;
        await WaitAsync(ct);
        return InspectException is not null ? throw InspectException : InspectResult;
    }

    /// <inheritdoc />
    public async Task<Result> RestoreAsync(Func<CancellationToken, Task<Stream>> openRead, CancellationToken ct = default)
    {
        RestoreCalls++;
        LastOpenRead = openRead;
        await WaitAsync(ct);
        return RestoreException is not null ? throw RestoreException : RestoreResult;
    }

    /// <inheritdoc />
    public void CompletePendingRestore() => CompletePendingRestoreCalls++;

    private async Task WaitAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(ct);
        }
    }
}
