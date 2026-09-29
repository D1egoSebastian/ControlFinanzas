using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlDeFinanzasIA.Infrastructure.Backup;

/// <summary>DI registration of the backup services (F-019, DD-3).</summary>
public static class BackupServiceCollectionExtensions
{
    /// <summary>
    /// Registers the backup file system, options and <see cref="SqliteBackupService"/> as singletons.
    /// Safe to call more than once. <see cref="Application.Abstractions.IBackupService"/> is registered
    /// by the slice that completes the service.
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
        services.TryAddSingleton<SqliteBackupService>();

        return services;
    }
}
