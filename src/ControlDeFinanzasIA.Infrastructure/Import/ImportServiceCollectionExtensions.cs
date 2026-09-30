using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlDeFinanzasIA.Infrastructure.Import;

/// <summary>DI registration of the import readers (F-020, DD-9).</summary>
public static class ImportServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="CsvTableReader"/> as a singleton. The <c>ITabularReader</c> port itself is bound later
    /// by the content-sniffing reader (slice S3). Safe to call more than once.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddImportReaders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<CsvTableReader>();

        return services;
    }
}
