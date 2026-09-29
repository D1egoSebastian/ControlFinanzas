using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Infrastructure.Reports;
using Microsoft.Extensions.DependencyInjection;

namespace ControlDeFinanzasIA.Tests.Integration.Reports;

/// <summary>Tests for <see cref="ReportServiceCollectionExtensions.AddReportExporters"/> (F-018, slice S2).</summary>
public sealed class ReportServiceCollectionExtensionsTests
{
    [Fact]
    public void AddReportExporters_RegistersPdfWriterWithDistinctFormats()
    {
        var services = new ServiceCollection();
        services.AddReportExporters();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        IReportWriter[] writers = provider.GetServices<IReportWriter>().ToArray();

        Assert.Contains(writers, w => w.Format == ReportFormat.Pdf);
        Assert.Equal(writers.Length, writers.Select(w => w.Format).Distinct().Count());
    }

    [Fact]
    public void AddReportExporters_CalledTwice_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddReportExporters();
        int first = services.Count;
        services.AddReportExporters();

        Assert.Equal(first, services.Count);
    }

    [Fact]
    public void AddReportExporters_NullServices_Throws() => Assert.Throws<ArgumentNullException>(() => ReportServiceCollectionExtensions.AddReportExporters(null!));

    [Fact]
    public async Task ResolvedPdfWriter_RendersWithoutOtherGlobalInitialization()
    {
        var services = new ServiceCollection();
        services.AddReportExporters();
        using ServiceProvider provider = services.BuildServiceProvider();
        IReportWriter writer = provider.GetServices<IReportWriter>().Single(w => w.Format == ReportFormat.Pdf);
        using var ms = new MemoryStream();

        Result result = await writer.WriteMonthSummaryAsync(ReportTestData.Report(), ReportTestData.EsLabels(), ms);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(ms.Length > 1024);
    }
}
