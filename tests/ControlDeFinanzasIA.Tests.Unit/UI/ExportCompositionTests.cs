using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.UseCases.Exports.ExportMonthSummaryReport;
using ControlDeFinanzasIA.UI.Composition;
using ControlDeFinanzasIA.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Verifies the Fase 18 DI block of <see cref="Bootstrap"/> (F-018, D-10, slice S4): both
/// writers, the month-summary use case and the Avalonia save dialog resolve.
/// </summary>
public sealed class ExportCompositionTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        Bootstrap.ConfigureServices(services, new ConfigurationBuilder().Build());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    [Fact]
    public void FileSaveDialog_IsTheAvaloniaImplementation_AsASingleton()
    {
        using ServiceProvider provider = BuildProvider();

        IFileSaveDialog first = provider.GetRequiredService<IFileSaveDialog>();

        Assert.IsType<AvaloniaFileSaveDialog>(first);
        Assert.Same(first, provider.GetRequiredService<IFileSaveDialog>());
    }

    [Fact]
    public void MonthSummaryUseCaseAndBothWriters_ResolveInsideAScope()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.NotNull(services.GetRequiredService<ExportMonthSummaryReportUseCase>());

        ReportFormat[] formats = services.GetServices<IReportWriter>().Select(w => w.Format).OrderBy(f => f).ToArray();
        Assert.Equal(new[] { ReportFormat.Pdf, ReportFormat.Xlsx }, formats);
    }
    }
