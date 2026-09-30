using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Notifications.DetectNotifications;
using ControlDeFinanzasIA.Application.UseCases.Notifications.DismissNotification;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;
using ControlDeFinanzasIA.Application.UseCases.Notifications.MarkNotificationsRead;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.Services;

/// <summary>
/// <see cref="INotificationCenter"/> implementation that runs each operation in its own
/// dependency-injection scope (F-017, D-9, R-15).
/// </summary>
/// <remarks>
/// <para>
/// Singleton ViewModels cannot hold the Scoped <c>AppDbContext</c>, so every operation resolves
/// its use case inside <c>IServiceScopeFactory.CreateScope()</c>. The work is wrapped in
/// <see cref="Task.Run(Func{Task}, CancellationToken)"/> because Microsoft.Data.Sqlite completes
/// synchronously: without it a "fire-and-forget" refresh would run the whole detection on the
/// UI thread.
/// </para>
/// <para>
/// One <see cref="SemaphoreSlim"/> serializes every mutating operation, so a dismiss can never
/// be overwritten by a concurrent detection run holding a stale snapshot. Reads are not gated.
/// </para>
/// <para>
/// <see cref="SuspendAsync"/> (F-019, DD-7) takes that same gate and raises a flag: while any
/// suspension is held every database-touching member is a no-op (no scope is created, nothing
/// is raised), so a restore can swap the database file without a detection run opening it. Callers
/// already queued on the gate when a suspension is taken return as no-ops, so none can wait on a
/// handle that a successful restore intentionally never disposes.
/// </para>
/// </remarks>
public sealed class ScopedNotificationCenter : INotificationCenter
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ScopedNotificationCenter> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SynchronizationContext? _context;
    private readonly object _sync = new();
    private CancellationTokenSource _abandonQueued = new();
    private int _suspensions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedNotificationCenter"/> class.
    /// </summary>
    /// <remarks>
    /// Captures <see cref="SynchronizationContext.Current"/> so <see cref="Changed"/> is
    /// raised on the UI thread; when there is none (unit tests) the event is raised inline.
    /// </remarks>
    /// <param name="scopes">Factory used to open one scope per operation.</param>
    /// <param name="logger">Logger for swallowed failures.</param>
    public ScopedNotificationCenter(IServiceScopeFactory scopes, ILogger<ScopedNotificationCenter> logger)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(logger);

        _scopes = scopes;
        _logger = logger;
        _context = SynchronizationContext.Current;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (IsSuspended || !await EnterGateAsync(ct))
        {
            return;
        }

        await RunGatedAsync("refresh", DetectAsync, ct);
    }

    /// <inheritdoc />
    public async Task<bool> TryRefreshAsync(CancellationToken ct = default)
    {
        if (IsSuspended || !await _gate.WaitAsync(0, ct))
        {
            return false;
        }

        if (IsSuspended)
        {
            _gate.Release();
            return false;
        }

        await RunGatedAsync("refresh", DetectAsync, ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<NotificationListDto?> GetAsync(CancellationToken ct = default)
    {
        if (IsSuspended)
        {
            return null;
        }

        try
        {
            return await Task.Run(
                async () =>
                {
                    using IServiceScope scope = _scopes.CreateScope();
                    GetNotificationsQuery query = scope.ServiceProvider.GetRequiredService<GetNotificationsQuery>();
                    Result result = await query.ExecuteAsync(ct).ConfigureAwait(false);

                    if (result.IsSuccess && result.Value is NotificationListDto list)
                    {
                        return list;
                    }

                    _logger.LogWarning(
                        "Notification list could not be read: {ErrorCode} {ErrorMessage}",
                        result.Error?.Code,
                        result.Error?.Message);
                    return null;
                },
                ct);
        }
        catch (Exception ex) when (!IsCallerCancellation(ex, ct))
        {
            _logger.LogWarning(ex, "Notification list read failed.");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task MarkReadAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0 || IsSuspended || !await EnterGateAsync(ct))
        {
            return;
        }

        await RunGatedAsync(
            "mark-read",
            async (provider, token) =>
            {
                MarkNotificationsReadUseCase useCase = provider.GetRequiredService<MarkNotificationsReadUseCase>();
                LogIfFailed("mark-read", await useCase.ExecuteAsync(ids, token).ConfigureAwait(false));
            },
            ct);
    }

    /// <inheritdoc />
    public async Task DismissAsync(Guid id, CancellationToken ct = default)
    {
        if (IsSuspended || !await EnterGateAsync(ct))
        {
            return;
        }

        await RunGatedAsync(
            "dismiss",
            async (provider, token) =>
            {
                DismissNotificationUseCase useCase = provider.GetRequiredService<DismissNotificationUseCase>();
                LogIfFailed("dismiss", await useCase.ExecuteAsync(id, token).ConfigureAwait(false));
            },
            ct);
    }

    /// <inheritdoc />
    public async Task<IDisposable> SuspendAsync(CancellationToken ct = default)
    {
        // Raise the flag first so no NEW operation starts while we wait for the in-flight one, and wake every
        // caller already queued on the gate: a successful restore never disposes this handle, so they would
        // otherwise wait forever.
        CancellationTokenSource abandon;
        lock (_sync)
        {
            _suspensions++;
            abandon = _abandonQueued;
        }

        abandon.Cancel();
        try
        {
            await _gate.WaitAsync(ct);
        }
        catch
        {
            LowerSuspension();
            throw;
        }

        return new Suspension(this);
    }

    private bool IsSuspended
    {
        get
        {
            lock (_sync)
            {
                return _suspensions > 0;
            }
        }
    }

    // Waits for the gate unless a suspension is taken first. Returns true with the gate held; false (gate not
    // held, nothing to do) when a suspension was taken before or while waiting.
    private async Task<bool> EnterGateAsync(CancellationToken ct)
    {
        CancellationTokenSource abandon;
        lock (_sync)
        {
            abandon = _abandonQueued;
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, abandon.Token);
            await _gate.WaitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }

        // The gate may have been handed over right when a suspension was raised.
        if (IsSuspended)
        {
            _gate.Release();
            return false;
        }

        return true;
    }

    private void LowerSuspension()
    {
        lock (_sync)
        {
            _suspensions--;
            if (_suspensions == 0)
            {
                // The old source stays cancelled (it is not disposed: a waiter may still hold its token);
                // later callers get a fresh one.
                _abandonQueued = new CancellationTokenSource();
            }
        }
    }

    private static bool IsCallerCancellation(Exception ex, CancellationToken ct)
        => ex is OperationCanceledException && ct.IsCancellationRequested;

    private async Task DetectAsync(IServiceProvider provider, CancellationToken ct)
    {
        DetectNotificationsUseCase useCase = provider.GetRequiredService<DetectNotificationsUseCase>();
        LogIfFailed("refresh", await useCase.ExecuteAsync(ct).ConfigureAwait(false));
    }

    // The caller has already acquired _gate. Always releases it, then raises Changed.
    private async Task RunGatedAsync(
        string operation,
        Func<IServiceProvider, CancellationToken, Task> work,
        CancellationToken ct)
    {
        try
        {
            await Task.Run(
                async () =>
                {
                    using IServiceScope scope = _scopes.CreateScope();
                    await work(scope.ServiceProvider, ct).ConfigureAwait(false);
                },
                ct);
        }
        catch (Exception ex) when (!IsCallerCancellation(ex, ct))
        {
            _logger.LogWarning(ex, "Notification {Operation} failed.", operation);
        }
        finally
        {
            _gate.Release();
            RaiseChanged();
        }
    }

    private void LogIfFailed(string operation, Result result)
    {
        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Notification {Operation} returned a failure: {ErrorCode} {ErrorMessage}",
                operation,
                result.Error?.Code,
                result.Error?.Message);
        }
    }

    private void RaiseChanged()
    {
        if (_context is null)
        {
            InvokeChanged();
            return;
        }

        _context.Post(_ => InvokeChanged(), null);
    }

    private void InvokeChanged()
    {
        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A notification Changed handler threw.");
        }
    }

    private sealed class Suspension(ScopedNotificationCenter owner) : IDisposable
    {
        private ScopedNotificationCenter? _owner = owner;

        public void Dispose()
        {
            // Idempotent: only the first call lowers the flag and releases the gate.
            ScopedNotificationCenter? center = Interlocked.Exchange(ref _owner, null);
            if (center is null)
            {
                return;
            }

            center.LowerSuspension();
            center._gate.Release();
        }
    }
}
