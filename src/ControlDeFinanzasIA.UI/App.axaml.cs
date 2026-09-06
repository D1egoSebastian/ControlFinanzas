using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.UI.Composition;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI;

/// <summary>
/// Avalonia application that consumes the <see cref="IServiceProvider"/>
/// created by the host and wires up the main window.
/// </summary>
public partial class App : Avalonia.Application
{
    /// <summary>
    /// Gets or sets the service provider for the application.
    /// </summary>
    /// <remarks>
    /// Set by <see cref="Program.Main(string[])"/> before the framework
    /// initialization completes.
    /// </remarks>
    public IServiceProvider? Services { get; set; }

    /// <summary>
    /// Loads the XAML resources for the application.
    /// </summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Called when the framework initialization is completed.
    /// </summary>
    /// <remarks>
    /// Resolves the required services, emits the "App starting" log,
    /// delegates pending EF Core migration application to the composition
    /// root (<see cref="Bootstrap.ApplyMigrations(IServiceProvider)"/>),
    /// creates the main window from <see cref="MainWindow.axaml"/>,
    /// assigns the <see cref="ShellViewModel"/> as its data context,
    /// navigates to the dashboard route, and wires up the "App stopping"
    /// log to the window's closing event.
    /// </remarks>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        if (Services is null)
        {
            Console.Error.WriteLine("IServiceProvider is not available; running without DI.");
            desktop.MainWindow = new MainWindow();
            base.OnFrameworkInitializationCompleted();
            return;
        }

        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("es-DO");

        ILogger<App> logger = Services.GetRequiredService<ILogger<App>>();
        IClock clock = Services.GetRequiredService<IClock>();
        IPathResolver pathResolver = Services.GetRequiredService<IPathResolver>();

        DateTime startTime = clock.UtcNow;
        logger.LogInformation(
            "App starting at {Time}, data directory: {Directory}",
            startTime,
            pathResolver.DataDirectory);

        Bootstrap.ApplyMigrations(Services);

        ShellViewModel shellViewModel = Services.GetRequiredService<ShellViewModel>();
        INavigationService navigationService = Services.GetRequiredService<INavigationService>();
        navigationService.NavigateTo(RouteNames.Dashboard);

        MainWindow mainWindow = new() { DataContext = shellViewModel };
        mainWindow.Closing += (_, _) =>
        {
            TimeSpan elapsed = clock.UtcNow - startTime;
            logger.LogInformation(
                "App stopping after {ElapsedMs} ms",
                elapsed.TotalMilliseconds);
        };

        desktop.MainWindow = mainWindow;

        base.OnFrameworkInitializationCompleted();
    }
}
