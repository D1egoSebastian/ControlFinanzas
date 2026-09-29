using ControlDeFinanzasIA.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlDeFinanzasIA.Infrastructure.Reports;

/// <summary>DI registration of the report writers (F-018, D-10).</summary>
public static class ReportServiceCollectionExtensions
{
    /// <summary>
    /// Registers the report writers as <see cref="IReportWriter"/> singletons and applies the
    /// QuestPDF configuration (Community license, glyph check off). Safe to call more than once.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddReportExporters(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        QuestPdfConfiguration.Apply();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReportWriter, QuestPdfReportWriter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReportWriter, ClosedXmlReportWriter>());

        return services;
    }
}
