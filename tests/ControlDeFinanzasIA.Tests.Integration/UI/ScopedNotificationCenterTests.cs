using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Notifications;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Notifications.DetectNotifications;
using ControlDeFinanzasIA.Application.UseCases.Notifications.DismissNotification;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;
using ControlDeFinanzasIA.Application.UseCases.Notifications.MarkNotificationsRead;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.UI.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.UI;

/// <summary>
/// Integration tests for <see cref="ScopedNotificationCenter"/> against a real SQLite file and a
/// real dependency-injection container (F-017, R-15, D-9, slice S5a).
/// </summary>
public sealed class ScopedNotificationCenterTests : IDisposable
{
    private static readonly DateTime _createdAt = new(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"notif-center-{Guid.NewGuid():N}.db");

    private readonly ListLoggerProvider _logs = new();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (string suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
        {
            try
            {
                File.Delete(_databasePath + suffix);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup.
            }
        }
    }

    [Fact]
    public async Task EachOperation_RunsInItsOwnScope_ThatIsDisposed()
    {
        await using ServiceProvider provider = BuildProvider();
        await MigrateAsync(provider);
        var factory = new CountingScopeFactory(provider.GetRequiredService<IServiceScopeFactory>());
        ScopedNotificationCenter center = await CreateCenterAsync(factory);
        Guid id = await SeedAsync(provider);

        await center.RefreshAsync();
        await center.GetAsync();
        await center.MarkReadAsync([id]);
        await center.DismissAsync(id);

        Assert.Equal(4, factory.Created);
        Assert.Equal(4, factory.Disposed);
    }

    [Fact]
    public async Task TryRefreshAsync_ReturnsFalse_WhileARunIsInFlight()
    {
        await using ServiceProvider provider = BuildProvider();
        await MigrateAsync(provider);
        var factory = new CountingScopeFactory(provider.GetRequiredService<IServiceScopeFactory>())
        {
            BlockFirstCreate = true,
        };
        ScopedNotificationCenter center = await CreateCenterAsync(factory);

        Task running = center.RefreshAsync();
        Assert.True(await Task.Run(() => factory.Entered.Wait(TimeSpan.FromSeconds(10))));

        bool duringRun = await center.TryRefreshAsync();
        factory.Release.Set();
        await running;
        bool afterRun = await center.TryRefreshAsync();

        Assert.False(duringRun);
        Assert.True(afterRun);
    }

    [Fact]
    public async Task Changed_FiresAfterSuccessfulRefresh_Dismiss_AndMarkRead()
    {
        await using ServiceProvider provider = BuildProvider();
        await MigrateAsync(provider);
        ScopedNotificationCenter center = await CreateCenterAsync(provider.GetRequiredService<IServiceScopeFactory>());
        Guid id = await SeedAsync(provider);
        int changed = 0;
        center.Changed += (_, _) => changed++;

        await center.RefreshAsync();
        Assert.Equal(1, changed);

        await center.MarkReadAsync([id]);
        Assert.Equal(2, changed);

        await center.DismissAsync(id);
        Assert.Equal(3, changed);
    }

    [Fact]
    public async Task Changed_FiresAfterFailedRefresh_AndTheFailureIsSwallowedAndLogged()
    {
        // No migration: the Notifications table does not exist, so the detection use case throws.
        await using ServiceProvider provider = BuildProvider();
        ScopedNotificationCenter center = await CreateCenterAsync(provider.GetRequiredService<IServiceScopeFactory>());
        int changed = 0;
        center.Changed += (_, _) => changed++;

        await center.RefreshAsync();

        Assert.Equal(1, changed);
        Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task TryRefreshAsync_FailedRun_ReturnsTrueAndDoesNotThrow()
    {
        await using ServiceProvider provider = BuildProvider();
        ScopedNotificationCenter center = await CreateCenterAsync(provider.GetRequiredService<IServiceScopeFactory>());

        bool ran = await center.TryRefreshAsync();

        Assert.True(ran);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenTheReadFails()
    {
        await using ServiceProvider provider = BuildProvider();
        ScopedNotificationCenter center = await CreateCenterAsync(provider.GetRequiredService<IServiceScopeFactory>());

        NotificationListDto? list = await center.GetAsync();

        Assert.Null(list);
        Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task MarkReadAndDismiss_PersistAndAreReflectedByGet()
    {
        await using ServiceProvider provider = BuildProvider();
        await MigrateAsync(provider);
        ScopedNotificationCenter center = await CreateCenterAsync(provider.GetRequiredService<IServiceScopeFactory>());
        Guid id = await SeedAsync(provider);

        NotificationListDto? before = await center.GetAsync();
        await center.MarkReadAsync([id]);
        NotificationListDto? afterRead = await center.GetAsync();
        await center.DismissAsync(id);
        NotificationListDto? afterDismiss = await center.GetAsync();

        Assert.Equal(1, before!.UnreadCount);
        Assert.Equal(0, afterRead!.UnreadCount);
        Assert.Single(afterRead.Items);
        Assert.Empty(afterDismiss!.Items);
    }

    [Fact]
    public async Task MarkReadAsync_EmptyIds_IsANoOpAndRaisesNothing()
    {
        await using ServiceProvider provider = BuildProvider();
        await MigrateAsync(provider);
        var factory = new CountingScopeFactory(provider.GetRequiredService<IServiceScopeFactory>());
        ScopedNotificationCenter center = await CreateCenterAsync(factory);
        int changed = 0;
        center.Changed += (_, _) => changed++;

        await center.MarkReadAsync([]);

        Assert.Equal(0, factory.Created);
        Assert.Equal(0, changed);
    }

    [Fact]
    public async Task ChangedHandlerThatThrows_DoesNotBreakTheOperation()
    {
        await using ServiceProvider provider = BuildProvider();
        await MigrateAsync(provider);
        ScopedNotificationCenter center = await CreateCenterAsync(provider.GetRequiredService<IServiceScopeFactory>());
        center.Changed += (_, _) => throw new InvalidOperationException("listener failure");

        await center.RefreshAsync();

        Assert.True(await center.TryRefreshAsync());
    }

    // Constructed on a thread-pool thread, where SynchronizationContext.Current is null,
    // so Changed is raised inline (the documented unit-test behavior of the center).
    private Task<ScopedNotificationCenter> CreateCenterAsync(IServiceScopeFactory factory) =>
        Task.Run(() => new ScopedNotificationCenter(factory, new Logger<ScopedNotificationCenter>(new LoggerFactory([_logs]))));

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(_logs));
        services.AddSingleton<IClock>(new StubClock());
        services.AddSingleton<ICurrencyConverter, IdentityCurrencyConverter>();
        services.AddPersistence(new StubPathResolver(_databasePath));
        services.AddTransient<GetBudgetVsActualQuery>();
        services.AddTransient<DetectNotificationsUseCase>();
        services.AddTransient<GetNotificationsQuery>();
        services.AddTransient<MarkNotificationsReadUseCase>();
        services.AddTransient<DismissNotificationUseCase>();
        return services.BuildServiceProvider();
    }

    private static async Task MigrateAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    private static async Task<Guid> SeedAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        INotificationRepository repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var notification = Notification.ForDebtInstallment(
            Guid.NewGuid(),
            new DateOnly(2026, 9, 29),
            "Cuota Auto",
            Money.Create(1500m, Currency.DOP),
            _createdAt);

        Result result = await repository.ApplyReconciliationAsync(
            new NotificationReconciliationPlan([notification], [], []));
        Assert.True(result.IsSuccess);
        return notification.Id;
    }

    private sealed class StubClock : IClock
    {
        public DateTime UtcNow => _createdAt;

        public DateOnly Today => new(2026, 9, 28);
    }

    private sealed class StubPathResolver(string databasePath) : IPathResolver
    {
        public string DataDirectory => Path.GetDirectoryName(databasePath)!;

        public string DatabaseFilePath => databasePath;
    }

    private sealed class IdentityCurrencyConverter : ICurrencyConverter
    {
        public Task<Result> ConvertAsync(Money source, Currency target, CancellationToken ct = default) =>
            Task.FromResult(Result.Success(source));

        public Task<MoneyDisplay> DisplayAsync(Money source, CancellationToken ct = default) =>
            Task.FromResult(new MoneyDisplay(source, null, null));
    }

    private sealed class CountingScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private int _created;
        private int _disposed;
        private int _blocked;

        public int Created => Volatile.Read(ref _created);

        public int Disposed => Volatile.Read(ref _disposed);

        public bool BlockFirstCreate { get; init; }

        public ManualResetEventSlim Entered { get; } = new(false);

        public ManualResetEventSlim Release { get; } = new(false);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _created);

            if (BlockFirstCreate && Interlocked.Exchange(ref _blocked, 1) == 0)
            {
                Entered.Set();
                Release.Wait(TimeSpan.FromSeconds(10));
            }

            return new CountingScope(inner.CreateScope(), () => Interlocked.Increment(ref _disposed));
        }
    }

    private sealed class CountingScope(IServiceScope inner, Action onDispose) : IServiceScope
    {
        public IServiceProvider ServiceProvider => inner.ServiceProvider;

        public void Dispose()
        {
            inner.Dispose();
            onDispose();
        }
    }

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        private readonly List<(LogLevel Level, string Message)> _entries = new();

        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get
            {
                lock (_entries)
                {
                    return _entries.ToArray();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new ListLogger(this);

        public void Dispose()
        {
        }

        private void Add(LogLevel level, string message)
        {
            lock (_entries)
            {
                _entries.Add((level, message));
            }
        }

        private sealed class ListLogger(ListLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                owner.Add(logLevel, formatter(state, exception));
        }
    }
}
