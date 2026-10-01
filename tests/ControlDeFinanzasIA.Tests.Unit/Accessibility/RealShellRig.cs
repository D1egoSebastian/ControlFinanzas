using System.Globalization;
using Avalonia.Controls;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Tests.Unit.UI;
using ControlDeFinanzasIA.UI.Composition;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlDeFinanzasIA.Tests.Unit.Accessibility;

/// <summary>
/// F-021 S8: the REAL composition root (<see cref="Bootstrap"/>) over a migrated temp-file SQLite database, the REAL
/// <see cref="MainWindow"/> and the REAL <see cref="ShellViewModel"/>. Used by the runtime accessibility tests so the
/// controls they inspect are the ones the user gets.
/// </summary>
internal sealed class RealShellRig : IAsyncDisposable
{
    private readonly string _directory;
    private readonly ServiceProvider _provider;
    private readonly CultureInfo _previousCulture;

    private RealShellRig(string directory, ServiceProvider provider, ShellViewModel shell, MainWindow window, CultureInfo previous)
    {
        _directory = directory;
        _provider = provider;
        Shell = shell;
        Window = window;
        _previousCulture = previous;
    }

    public ShellViewModel Shell { get; }

    public MainWindow Window { get; }

    /// <summary>Gets the real service provider (to seed and inspect the temp database).</summary>
    public IServiceProvider Services => _provider;

    public static async Task<RealShellRig> CreateAsync(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

        string directory = Path.Combine(Path.GetTempPath(), $"a11y-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var services = new ServiceCollection();
        services.AddLogging();
        Bootstrap.ConfigureServices(services, new ConfigurationBuilder().Build());

        var resolver = new TempPathResolver(directory);
        services.RemoveAll<IPathResolver>();
        services.AddSingleton<IPathResolver>(resolver);
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<AppDbContext>();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={resolver.DatabaseFilePath}"));

        ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = false, ValidateOnBuild = false });
        using (var migrator = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={resolver.DatabaseFilePath};Pooling=False").Options))
        {
            await migrator.Database.MigrateAsync();
        }

        ShellViewModel shell = provider.GetRequiredService<ShellViewModel>();
        var window = new MainWindow { DataContext = shell, Width = 1100, Height = 900 };
        window.Show();
        HeadlessUi.Flush();
        return new RealShellRig(directory, provider, shell, window, previous);
    }

    /// <summary>Opens the section at a sidebar position (0-based) and waits for its view-model to finish loading.</summary>
    /// <param name="position">0-based position in the sidebar.</param>
    public async Task OpenSectionAsync(int position)
    {
        Shell.GoToSectionCommand.Execute(position == 9 ? 0 : position + 1);
        for (int i = 0; i < 60; i++)
        {
            await Task.Delay(10);
            HeadlessUi.Flush();
        }
    }

    /// <summary>Pumps the dispatcher until <paramref name="condition"/> holds (or fails the test after about 4 seconds).</summary>
    /// <param name="condition">The condition to wait for.</param>
    public async Task SettleAsync(Func<bool> condition)
    {
        for (int i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(10);
            HeadlessUi.Flush();
        }

        Assert.True(condition(), "Timed out waiting for the view-model to settle.");
        HeadlessUi.Flush();
    }

    public ValueTask DisposeAsync()
    {
        Window.Close();
        HeadlessUi.Flush();
        _provider.Dispose();
        SqliteConnection.ClearAllPools();
        CultureInfo.CurrentUICulture = _previousCulture;
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }

        return ValueTask.CompletedTask;
    }

    private sealed class TempPathResolver(string directory) : IPathResolver
    {
        public string DataDirectory => directory;

        public string DatabaseFilePath => Path.Combine(directory, "data.db");
    }
}
