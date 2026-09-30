using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.UI.Composition;
using ControlDeFinanzasIA.UI.Diagnostics;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Services;
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

        ILogger<App> logger = Services.GetRequiredService<ILogger<App>>();
        IClock clock = Services.GetRequiredService<IClock>();
        IPathResolver pathResolver = Services.GetRequiredService<IPathResolver>();

        // F-021 (DD-2): global exception hooks live only for the desktop lifetime and are removed on exit.
        IDisposable exceptionHooks = GlobalExceptionHooks.Install(
            Services.GetRequiredService<IUnexpectedErrorReporter>(),
            clock);
        desktop.Exit += (_, _) => exceptionHooks.Dispose();

        DateTime startTime = clock.UtcNow;
        logger.LogInformation(
            "App starting at {Time}, data directory: {Directory}",
            startTime,
            pathResolver.DataDirectory);

        // F-019 (R-14): the pending-restore cleanup runs only after the migrations succeeded and
        // never crashes startup; a restore itself never goes through ApplyMigrations.
        Bootstrap.ApplyMigrationsAndCompletePendingRestore(Services, logger);

        ApplyPersistedUiCulture(Services, logger);

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

        // F-017 D-10: one detection run after the window shows. Opened fires once, after
        // ApplyMigrations and the culture read above, so the table exists and the formatter
        // sees the persisted culture. The center runs detection off the UI thread and never
        // throws for a data failure (R-15); this trigger adds a last-resort guard so that
        // nothing here can ever take startup down.
        INotificationCenter notificationCenter = Services.GetRequiredService<INotificationCenter>();
        mainWindow.Opened += (_, _) => StartNotificationDetection(notificationCenter, logger);

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Fires the startup notification detection run without awaiting it (fire-and-forget).
    /// </summary>
    /// <remarks>
    /// The center already swallows and logs every non-cancellation failure (R-15); the
    /// try/catch and the faulted-task continuation are defense in depth so an unexpected
    /// synchronous throw or a faulted task can never surface as an unhandled exception.
    /// </remarks>
    private static void StartNotificationDetection(INotificationCenter center, ILogger logger)
    {
        try
        {
            Task run = center.RefreshAsync();
            _ = run.ContinueWith(
                t => logger.LogWarning(t.Exception, "Startup notification detection failed."),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Startup notification detection could not start.");
        }
    }

    /// <summary>
    /// Reads the persisted locale from <see cref="ISettingsStore"/> and sets the
    /// process' UI culture accordingly.
    /// </summary>
    /// <remarks>
    /// F-012 D-7: this MUST run AFTER <see cref="Bootstrap.ApplyMigrations(IServiceProvider)"/>
    /// (the <c>Settings</c> table may not exist yet on a first launch) and BEFORE
    /// <see cref="ShellViewModel"/> is resolved or <see cref="MainWindow"/> is constructed
    /// (both materialize localized strings once, at that point, and cannot live-rebind).
    /// <para>
    /// <see cref="OnFrameworkInitializationCompleted"/> is <see langword="void"/> and runs
    /// before Avalonia's dispatcher starts pumping, so a naked
    /// <c>store.GetAppSettingsAsync().GetAwaiter().GetResult()</c> risks a startup hang under
    /// the captured <c>AvaloniaSynchronizationContext</c>. Wrapping the call in
    /// <see cref="Task.Run(Func{Task})"/> schedules the async chain on the thread pool,
    /// escaping that synchronization context before the blocking wait.
    /// </para>
    /// </remarks>
    private static void ApplyPersistedUiCulture(IServiceProvider services, ILogger logger)
    {
        AppSettings settings = AppSettings.Defaults;

        try
        {
            // ISettingsStore and its dependencies are Scoped (AddPersistence), the same
            // lifetime Bootstrap.ApplyMigrations already scopes five lines above. Never
            // resolve a Scoped service from the root provider.
            using IServiceScope scope = services.CreateScope();
            ISettingsStore store = scope.ServiceProvider.GetRequiredService<ISettingsStore>();

            settings = Task
                .Run(() => store.GetAppSettingsAsync(CancellationToken.None))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            // GetAppSettingsAsync already degrades internally to AppSettings.Defaults
            // (SettingsStore's own broad catch); this covers a DI resolution or
            // scope-creation failure instead. Startup must never block on a preference read.
            logger.LogWarning(ex, "Persisted settings could not be read; using defaults.");
        }

        CultureInfo culture = ResolveUiCulture(settings, logger);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// Resolves the <see cref="CultureInfo"/> to apply from a settings snapshot, falling
    /// back to <see cref="SupportedLocales.Default"/> when <see cref="AppSettings.Locale"/>
    /// is missing/blank or not a known culture.
    /// </summary>
    /// <remarks>
    /// Extracted as a pure, side-effect-free (besides logging) helper so the fallback
    /// behaviour is unit testable without a running Avalonia application or DI container —
    /// there is no headless Avalonia test harness in this repo to exercise
    /// <see cref="OnFrameworkInitializationCompleted"/> end-to-end.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static CultureInfo ResolveUiCulture(AppSettings settings, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string requestedLocale = settings.Locale;

        // Primary guard: the allowlist (D-4), not just exception handling. .NET's
        // CultureInfo constructor does not reliably throw CultureNotFoundException for
        // malformed input — e.g. "not-a-real-culture" silently resolves to a custom
        // culture named "not" instead of throwing — so checking SupportedLocales first is
        // load-bearing, not redundant with the try/catch below.
        string locale = SupportedLocales.IsSupported(requestedLocale)
            ? requestedLocale
            : SupportedLocales.Default;

        if (locale != requestedLocale && !string.IsNullOrWhiteSpace(requestedLocale))
        {
            logger?.LogWarning(
                "Locale {Locale} is not supported; using {Default}.",
                requestedLocale,
                SupportedLocales.Default);
        }

        try
        {
            return new CultureInfo(locale);
        }
        catch (CultureNotFoundException ex)
        {
            // Defense-in-depth (D-4/D-7): reachable only if SupportedLocales.Default
            // itself were ever misconfigured to a non-existent culture.
            logger?.LogWarning(
                ex,
                "Locale {Locale} is not a known culture; using {Default}.",
                locale,
                SupportedLocales.Default);
            return new CultureInfo(SupportedLocales.Default);
        }
    }
}
