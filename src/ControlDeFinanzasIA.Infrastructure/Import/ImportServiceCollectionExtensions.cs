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
}
