using ControlDeFinanzasIA.UI.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Logging;

[Collection(StderrCollection.Name)]
public sealed class BootstrapLoggingTests
{
    private const string _envVar = "CONTROLDEFINANZASIA_DATA_DIR";

    [Fact]
    public void RealBootstrap_WritesALogFileUnderTheOverriddenDataDir()
    {
        using var temp = new TempDir();
        string? previous = Environment.GetEnvironmentVariable(_envVar);
        Environment.SetEnvironmentVariable(_envVar, temp.Path);
        try
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.ClearProviders());
            Bootstrap.ConfigureServices(services, new ConfigurationBuilder().Build());

            using (ServiceProvider sp = services.BuildServiceProvider())
            {
                sp.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap.Test").LogInformation("bootstrap-sentinel-line");
            }

            string logs = Path.Combine(temp.Path, "logs");
            Assert.True(Directory.Exists(logs));
            Assert.Contains("bootstrap-sentinel-line", LoggingRegistrationTests.ReadAllLogs(logs), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(_envVar, previous);
        }
    }
}
