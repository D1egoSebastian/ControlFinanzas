using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Infrastructure.Backup;
using Microsoft.Extensions.DependencyInjection;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

public sealed class BackupServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBackup_RegistersTheBackupSingletons()
    {
        using ServiceProvider provider = Build(services => services.AddBackup("3.2.1-test"));

        Assert.IsType<PhysicalBackupFileSystem>(provider.GetRequiredService<IBackupFileSystem>());
        Assert.Equal("3.2.1-test", provider.GetRequiredService<BackupOptions>().AppVersion);
        Assert.IsType<EfMigrationCatalog>(provider.GetRequiredService<IMigrationCatalog>());
        Assert.Same(provider.GetRequiredService<IMigrationCatalog>(), provider.GetRequiredService<IMigrationCatalog>());
        SqliteBackupService service = provider.GetRequiredService<SqliteBackupService>();
        Assert.Same(service, provider.GetRequiredService<SqliteBackupService>());
        Assert.Same(provider.GetRequiredService<IBackupFileSystem>(), provider.GetRequiredService<IBackupFileSystem>());
    }

    [Fact]
    public void AddBackup_RegistersTheServiceContractAsTheSameSingleInstance()
    {
        using ServiceProvider provider = Build(services => services.AddBackup("3.2.1-test"));

        IBackupService contract = provider.GetRequiredService<IBackupService>();

        // One instance: the single-flight gate that serializes create/inspect/restore lives inside it.
        Assert.IsType<SqliteBackupService>(contract);
        Assert.Same(contract, provider.GetRequiredService<IBackupService>());
        Assert.Same(contract, provider.GetRequiredService<SqliteBackupService>());
    }

    [Fact]
    public void AddBackup_CalledTwice_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddBackup("1.0.0");
        int count = services.Count;

        services.AddBackup("2.0.0");

        Assert.Equal(count, services.Count);
        using ServiceProvider provider = Build(s =>
        {
            s.AddBackup("1.0.0");
            s.AddBackup("2.0.0");
        });
        Assert.Equal("1.0.0", provider.GetRequiredService<BackupOptions>().AppVersion);
    }

    [Fact]
    public void AddBackup_NullServices_Throws() => Assert.Throws<ArgumentNullException>(() => BackupServiceCollectionExtensions.AddBackup(null!, "1.0.0"));

    private static ServiceProvider Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new FixedClock());
        services.AddSingleton<IPathResolver>(new FixedPaths());
        configure(services);
        return services.BuildServiceProvider();
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc);

        public DateOnly Today => new(2026, 9, 29);
    }

    private sealed class FixedPaths : IPathResolver
    {
        public string DataDirectory => Path.Combine(Path.GetTempPath(), "never-created-backup-di-test");

        public string DatabaseFilePath => Path.Combine(DataDirectory, "data.db");
    }
}
