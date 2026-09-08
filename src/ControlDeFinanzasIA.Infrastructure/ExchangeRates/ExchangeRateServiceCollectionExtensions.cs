using ControlDeFinanzasIA.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControlDeFinanzasIA.Infrastructure.ExchangeRates;

/// <summary>
/// Extension methods para registrar los servicios de tasas de
/// cambio en el contenedor de DI.
/// </summary>
public static class ExchangeRateServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="IExchangeRateProvider"/> como
    /// <see cref="StubExchangeRateProvider"/> (Singleton).
    /// </summary>
    /// <param name="services">Colección de servicios de DI.</param>
    /// <param name="configuration">
    /// Configuración desde la que el stub lee las tasas. Si es
    /// <see langword="null"/>, el stub usa defaults.
    /// </param>
    /// <returns>La misma <see cref="IServiceCollection"/> para encadenar.</returns>
    /// <exception cref="ArgumentNullException">
    /// Se lanza si <paramref name="services"/> es <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddExchangeRates(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configuration is not null)
        {
            services.AddSingleton<IConfiguration>(configuration);
        }

        services.AddSingleton<IExchangeRateProvider, StubExchangeRateProvider>();

        return services;
    }
}
