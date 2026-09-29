using ControlDeFinanzasIA.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlDeFinanzasIA.Infrastructure.Backup;

/// <summary>DI registration of the backup services (F-019, DD-3).</summary>
public static class BackupServiceCollectionExtensions
{
    /// <summary>
    /// Registers the backup file system, options, migration catalog and <see cref="SqliteBackupService"/> as
    /// singletons, and <see cref="IBackupService"/> as the same instance. Safe to call more than once.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="appVersion">Application version recorded in every manifest.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddBackup(this IServiceCollection services, string appVersion)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);

        services.TryAddSingleton<IBackupFileSystem, PhysicalBackupFileSystem>();
        services.TryAddSingleton(new BackupOptions(appVersion));
        services.TryAddSingleton<IMigrationCatalog, EfMigrationCatalog>();
        services.TryAddSingleton<SqliteBackupService>();

        // The contract resolves to the very same instance: its single-flight gate must be shared.
        services.TryAddSingleton<IBackupService>(sp => sp.GetRequiredService<SqliteBackupService>());

        return services;
    }
}
