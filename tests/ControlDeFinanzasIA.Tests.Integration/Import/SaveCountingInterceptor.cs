using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>
/// Counts <c>SaveChangesAsync</c> calls on the contexts built by <see cref="ImportTestDb"/> and records, for each
/// one, whether change detection was off and which thread ran it (F-020, S5).
/// </summary>
internal sealed class SaveCountingInterceptor : SaveChangesInterceptor
{
    private readonly object _sync = new();
    private readonly List<bool> _autoDetect = [];
    private readonly List<int> _threads = [];

    public int Saves
    {
        get
        {
            lock (_sync)
            {
                return _autoDetect.Count;
            }
        }
    }

    public IReadOnlyList<bool> AutoDetectAtSave
    {
        get
        {
            lock (_sync)
            {
                return _autoDetect.ToArray();
            }
        }
    }

    public IReadOnlyList<int> ThreadsAtSave
    {
        get
        {
            lock (_sync)
            {
                return _threads.ToArray();
            }
        }
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _autoDetect.Add(eventData.Context!.ChangeTracker.AutoDetectChangesEnabled);
            _threads.Add(Environment.CurrentManagedThreadId);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
