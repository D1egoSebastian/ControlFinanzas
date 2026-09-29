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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// Suspension semantics of the REAL <see cref="ScopedNotificationCenter"/> on a temp SQLite file
/// (F-019, R-19, DD-7): no detection run may open the database while a restore swaps it.
/// </summary>
public sealed class ScopedNotificationCenterSuspendTests : IDisposable
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(10);
    private static readonly DateTime _createdAt = new(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"notif-suspend-{Guid.NewGuid():N}.db");

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
    public async Task SuspendAsync_WaitsForTheInFlightRun_BeforeReturning()
    {
        await using ServiceProvider provider = await BuildMigratedProviderAsync();
        var factory = new GatedScopeFactory(provider.GetRequiredService<IServiceScopeFactory>()) { BlockFirstCreate = true };
        ScopedNotificationCenter center = await CreateCenterAsync(factory);

        Task running = center.RefreshAsync();
        Assert.True(await Task.Run(() => factory.Entered.Wait(_wait)));

        Task<IDisposable> suspending = center.SuspendAsync();
        await Task.Delay(150);
        Assert.False(suspending.IsCompleted);

        factory.Release.Set();
        await running;
        using IDisposable handle = await suspending.WaitAsync(_wait);

        Assert.Equal(1, factory.Created);
    }

    [Fact]
    public async Task WhileSuspended_EveryDatabaseTouchingMember_IsANoOp_AndRaisesNothing()
    {
        await using ServiceProvider provider = await BuildMigratedProviderAsync();
        Guid id = await SeedAsync(provider);
        var factory = new GatedScopeFactory(provider.GetRequiredService<IServiceScopeFactory>());
        ScopedNotificationCenter center = await CreateCenterAsync(factory);
        int changed = 0;
        center.Changed += (_, _) => changed++;

        IDisposable handle = await center.SuspendAsync();

        bool tried = await center.TryRefreshAsync();
        NotificationListDto? list = await center.GetAsync();
        await center.RefreshAsync().WaitAsync(_wait);
        await center.MarkReadAsync([id]).WaitAsync(_wait);
        await center.DismissAsync(id).WaitAsync(_wait);

        Assert.False(tried);
        Assert.Null(list);
        Assert.Equal(0, factory.Created);
        Assert.Equal(0, changed);
        handle.Dispose();

        // Nothing was written: the seeded row is still unread and not dismissed.
        NotificationListDto? after = await center.GetAsync();
        Assert.Equal(1, after!.UnreadCount);
        Assert.Single(after.Items);
    }

    [Fact]
    public async Task DisposingTheHandle_ResumesEveryMember()
    {
        await using ServiceProvider provider = await BuildMigratedProviderAsync();
        Guid id = await SeedAsync(provider);
        ScopedNotificationCenter center = await CreateCenterAsync(provider.GetRequiredService<IServiceScopeFactory>());

        IDisposable handle = await center.SuspendAsync();
        Assert.False(await center.TryRefreshAsync());
        handle.Dispose();

        Assert.True(await center.TryRefreshAsync());
        Assert.NotNull(await center.GetAsync());
        await center.MarkReadAsync([id]);
        Assert.Equal(0, (await center.GetAsync())!.UnreadCount);
    }

    [Fact]
    public async Task DisposingTheHandleTwice_ReleasesTheGateOnlyOnce()
    {
        await using ServiceProvider provider = await BuildMigratedProviderAsync();
        ScopedNotificationCenter center = await CreateCenterAsync(provider.GetRequiredService<IServiceScopeFactory>());

        IDisposable first = await center.SuspendAsync();
        first.Dispose();
        first.Dispose();

        // A later suspension must still exclude: a double release would let two owners in.
        IDisposable second = await center.SuspendAsync();
        Task<IDisposable> third = center.SuspendAsync();
        await Task.Delay(150);
        Assert.False(third.IsCompleted);

        first.Dispose();
        await Task.Delay(50);
        Assert.False(third.IsCompleted);
        Assert.False(await center.TryRefreshAsync());

        second.Dispose();
        using IDisposable thirdHandle = await third.WaitAsync(_wait);
    }

    [Fact]
    public async Task CancelledSuspendAsync_LeavesTheCenterUsable()
    {
        await using ServiceProvider provider = await BuildMigratedProviderAsync();
        var factory = new GatedScopeFactory(provider.GetRequiredService<IServiceScopeFactory>()) { BlockFirstCreate = true };
        ScopedNotificationCenter center = await CreateCenterAsync(factory);

        Task running = center.RefreshAsync();
        Assert.True(await Task.Run(() => factory.Entered.Wait(_wait)));
        using var cts = new CancellationTokenSource();
        Task<IDisposable> suspending = center.SuspendAsync(cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => suspending);
        factory.Release.Set();
        await running;

        Assert.True(await center.TryRefreshAsync());
        Assert.NotNull(await center.GetAsync());
    }

    private static Task<ScopedNotificationCenter> CreateCenterAsync(IServiceScopeFactory factory) =>
        Task.Run(() => new ScopedNotificationCenter(factory, NullLogger<ScopedNotificationCenter>.Instance));

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

    private async Task<ServiceProvider> BuildMigratedProviderAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new StubClock());
        services.AddSingleton<ICurrencyConverter, IdentityCurrencyConverter>();
        services.AddPersistence(new StubPathResolver(_databasePath));
        services.AddTransient<GetBudgetVsActualQuery>();
        services.AddTransient<DetectNotificationsUseCase>();
        services.AddTransient<GetNotificationsQuery>();
        services.AddTransient<MarkNotificationsReadUseCase>();
        services.AddTransient<DismissNotificationUseCase>();
        ServiceProvider provider = services.BuildServiceProvider();

        using IServiceScope scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        return provider;
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

    private sealed class GatedScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private int _created;
        private int _blocked;

        public int Created => Volatile.Read(ref _created);

        public bool BlockFirstCreate { get; init; }

        public ManualResetEventSlim Entered { get; } = new(false);

        public ManualResetEventSlim Release { get; } = new(false);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _created);

            if (BlockFirstCreate && Interlocked.Exchange(ref _blocked, 1) == 0)
            {
                Entered.Set();
                Release.Wait(_wait);
            }

            return inner.CreateScope();
        }
    }
}
