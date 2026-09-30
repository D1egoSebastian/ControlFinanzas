using Avalonia;
using Avalonia.Controls;
using ControlDeFinanzasIA.UI.Composition;
using ControlDeFinanzasIA.UI.Diagnostics;
using ControlDeFinanzasIA.UI.System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI;

/// <summary>
/// Entry point for the Avalonia desktop application.
/// </summary>
/// <remarks>
/// Builds a .NET generic host, configures logging and configuration,
/// wires the service graph via <see cref="Bootstrap"/>, and starts
/// Avalonia with the host's <see cref="IServiceProvider"/>.
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The process exit code; 1 when start-up failed before the UI existed.</returns>
    [STAThread]
    public static int Main(string[] args) =>
        StartupGuard.Run(
            () => RunApplication(args),
            () => Environment.GetEnvironmentVariable(SystemPathResolver.DataDirectoryEnvironmentVariable),
            Console.Error,
            () => DateTime.UtcNow);

    /// <summary>
    /// Avalonia configuration used by the visual designer and build tooling.
    /// Do not remove.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static int RunApplication(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddJsonFile(
            "appsettings.json",
            optional: true,
            reloadOnChange: true);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        Bootstrap.ConfigureServices(builder.Services, builder.Configuration);

        using IHost host = builder.Build();

        AppBuilder appBuilder = AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .AfterSetup(b =>
            {
                if (b.Instance is App app)
                {
                    app.Services = host.Services;
                }
            });

        return appBuilder.StartWithClassicDesktopLifetime(args);
    }
}
