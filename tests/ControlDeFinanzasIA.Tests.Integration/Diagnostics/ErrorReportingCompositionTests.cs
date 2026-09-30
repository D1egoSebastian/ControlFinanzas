using ControlDeFinanzasIA.Tests.Integration.Logging;
using ControlDeFinanzasIA.UI.Composition;
using ControlDeFinanzasIA.UI.Diagnostics;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Diagnostics;

/// <summary>
/// F-021 S3 (DD-13): the REAL <c>Bootstrap</c> composition wires the reporter, the presenter and the overlay state,
/// and a report through the resolved reporter reaches the real log file under the overridden data directory.
/// </summary>
[Collection(StderrCollection.Name)]
public sealed class ErrorReportingCompositionTests
{
    private const string _envVar = "CONTROLDEFINANZASIA_DATA_DIR";

    [Fact]
    public void RealBootstrap_ResolvesTheReporterAndPresenter_AndAReportReachesTheLogAndTheSharedOverlay()
    {
        using var temp = new TempDir();
        using var stderr = new StderrCapture();
        string? previous = Environment.GetEnvironmentVariable(_envVar);
        Environment.SetEnvironmentVariable(_envVar, temp.Path);
        try
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.ClearProviders());
            Bootstrap.ConfigureServices(services, new ConfigurationBuilder().Build());

            Guid id;
            using (ServiceProvider sp = services.BuildServiceProvider())
            {
                ErrorOverlayViewModel overlay = sp.GetRequiredService<ErrorOverlayViewModel>();
                Assert.Same(overlay, sp.GetRequiredService<ErrorOverlayViewModel>());
                Assert.Same(overlay, sp.GetRequiredService<IErrorPresenter>());
                IUnexpectedErrorReporter reporter = sp.GetRequiredService<IUnexpectedErrorReporter>();
                Assert.Same(reporter, sp.GetRequiredService<IUnexpectedErrorReporter>());

                id = reporter.Report(new InvalidOperationException("composed failure"), "ui");

                Assert.True(overlay.IsOpen);
                Assert.Equal(id, overlay.CorrelationId);
            }

            string log = LoggingRegistrationTests.ReadAllLogs(Path.Combine(temp.Path, "logs"));
            Assert.Contains(id.ToString("D"), log, StringComparison.Ordinal);
            Assert.Contains("composed failure", log, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(_envVar, previous);
        }
    }
}
