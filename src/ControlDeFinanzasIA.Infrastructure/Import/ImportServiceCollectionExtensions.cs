using ControlDeFinanzasIA.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlDeFinanzasIA.Infrastructure.Import;

/// <summary>DI registration of the import readers (F-020, DD-9, DD-10).</summary>
public static class ImportServiceCollectionExtensions
{
    /// <summary>
    /// Registers the CSV and XLSX readers as singletons and binds <see cref="ITabularReader"/> to the
    /// content-sniffing reader that composes them. Safe to call more than once.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddImportReaders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<CsvTableReader>();
        services.TryAddSingleton<ClosedXmlTableReader>();
        services.TryAddSingleton<ITabularReader, SniffingTabularReader>();

        return services;
    }

    /// <summary>
    /// Registers everything the import feature needs from Infrastructure (F-020, DD-13): the readers, the lookup
    /// and the atomic writer, all singletons that hold only the <see cref="IServiceScopeFactory"/> (never a scoped
    /// context). Safe to call more than once.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddImport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddImportReaders();
        services.TryAddSingleton<IImportLookup, EfImportLookup>();
        services.TryAddSingleton<IImportWriter, EfImportWriter>();

        return services;
    }
}
