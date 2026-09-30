using ControlDeFinanzasIA.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Infrastructure.Logging;

/// <summary>DI registration of the rolling file logger (F-021, DD-1).</summary>
public static class LoggingServiceCollectionExtensions
{
    private const string _logsFolder = "logs";

    /// <summary>
    /// Registers <see cref="RollingFileLoggerProvider"/> as a singleton <see cref="ILoggerProvider"/> writing to
    /// <c>&lt;DataDirectory&gt;/logs</c>, with the filter rules: <c>Information</c> by default and
    /// <c>Microsoft.*</c> categories at <c>Warning</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddRollingFileLogging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLogging();
        services.AddSingleton<ILoggerProvider>(sp =>
        {
            string directory = Path.Combine(sp.GetRequiredService<IPathResolver>().DataDirectory, _logsFolder);
            return new RollingFileLoggerProvider(new RollingFileLoggerOptions(directory), sp.GetRequiredService<IClock>());
        });

        string providerName = typeof(RollingFileLoggerProvider).FullName!;
        services.Configure<LoggerFilterOptions>(options =>
        {
            options.Rules.Add(new LoggerFilterRule(providerName, null, LogLevel.Information, null));
            options.Rules.Add(new LoggerFilterRule(providerName, "Microsoft", LogLevel.Warning, null));
        });

        return services;
    }
}
