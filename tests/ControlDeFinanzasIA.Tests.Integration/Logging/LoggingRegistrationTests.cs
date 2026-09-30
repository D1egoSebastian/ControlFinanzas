using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Logging;

[Collection(StderrCollection.Name)]
public sealed class LoggingRegistrationTests
{
    private static readonly DateTime _now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AddRollingFileLogging_RegistersASingletonProviderConsumedByTheFactory()
    {
        using var temp = new TempDir();
        using ServiceProvider sp = Build(temp.Path, clearProvidersFirst: false);

        RollingFileLoggerProvider[] providers = sp.GetServices<ILoggerProvider>().OfType<RollingFileLoggerProvider>().ToArray();

        RollingFileLoggerProvider single = Assert.Single(providers);
        Assert.Same(single, sp.GetServices<ILoggerProvider>().OfType<RollingFileLoggerProvider>().Single());
    }

    [Fact]
    public void Filters_DebugAndEfInformationNeverReachTheFile_WarningAndAppInformationDo()
    {
        using var temp = new TempDir();
        using (ServiceProvider sp = Build(temp.Path, clearProvidersFirst: false))
        {
            ILoggerFactory factory = sp.GetRequiredService<ILoggerFactory>();
            ILogger app = factory.CreateLogger("ControlDeFinanzasIA.Something");
            ILogger ef = factory.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command");

            app.LogDebug("debug-line");
            app.LogInformation("app-info-line");
            ef.LogInformation("ef-info-line");
            ef.LogWarning("ef-warning-line");
        }

        string text = ReadAllLogs(Path.Combine(temp.Path, "logs"));
        Assert.DoesNotContain("debug-line", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ef-info-line", text, StringComparison.Ordinal);
        Assert.Contains("app-info-line", text, StringComparison.Ordinal);
        Assert.Contains("ef-warning-line", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LogFile_LandsInDataDirLogsWithTheUtcDate()
    {
        using var temp = new TempDir();
        using (ServiceProvider sp = Build(temp.Path, clearProvidersFirst: false))
        {
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("X").LogInformation("hello");
        }

        Assert.True(File.Exists(Path.Combine(temp.Path, "logs", "app-20260930.log")));
    }

    [Fact]
    public void ClearProvidersBeforeRegistration_KeepsTheFileProvider()
    {
        using var temp = new TempDir();
        using (ServiceProvider sp = Build(temp.Path, clearProvidersFirst: true))
        {
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("X").LogInformation("after-clear");
        }

        Assert.Contains("after-clear", ReadAllLogs(Path.Combine(temp.Path, "logs")), StringComparison.Ordinal);
    }

    [Fact]
    public void LogsFolderDerivesFromTheResolvedDataDirectory()
    {
        // The logs folder derives from IPathResolver.DataDirectory only; the env-var override is resolved by
        // SystemPathResolver (UI layer) and is covered end to end in BootstrapLoggingTests.
        using var temp = new TempDir();
        string custom = Path.Combine(temp.Path, "custom-data");
        using (ServiceProvider sp = Build(custom, clearProvidersFirst: false))
        {
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("X").LogInformation("custom");
        }

        Assert.True(Directory.Exists(Path.Combine(custom, "logs")));
    }

    internal static string ReadAllLogs(string logsDir) =>
        string.Concat(Directory.GetFiles(logsDir).Select(File.ReadAllText));

    private static ServiceProvider Build(string dataDir, bool clearProvidersFirst)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new SettableClock(_now));
        services.AddSingleton<IPathResolver>(new FixedPaths(dataDir));
        services.AddLogging(b =>
        {
            if (clearProvidersFirst)
            {
                b.ClearProviders();
            }
        });
        services.AddRollingFileLogging();
        return services.BuildServiceProvider();
    }

    private sealed class FixedPaths(string dataDir) : IPathResolver
    {
        public string DataDirectory => dataDir;

        public string DatabaseFilePath => Path.Combine(dataDir, "data.db");
    }
}
