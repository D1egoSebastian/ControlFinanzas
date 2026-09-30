using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Tests.Unit.UI;
using ControlDeFinanzasIA.UI.Composition;
using ControlDeFinanzasIA.UI.Diagnostics;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Unit.Diagnostics;

/// <summary>
/// F-021 S4 (R-7, R-10, S-13, S-19): the REAL <c>Bootstrap</c> provider against a temporary data directory, a real
/// <see cref="MainWindow"/> over the resolved <see cref="ShellViewModel"/>, the real global hooks and a real command
/// that throws unexpectedly. The short id on screen must equal the first 8 hex of the Guid written to the real log
/// file at <c>ERR</c>.
/// </summary>
/// <remarks>
/// Lives in the Unit project (not Integration as the task text says) because only this project references
/// <c>Avalonia.Headless</c>; adding the package to the Integration project would be a package change (R-25).
/// </remarks>
[Collection(HeadlessUi.CollectionName)]
public sealed class ErrorOverlayEndToEndTests
{
    private const string _envVar = "CONTROLDEFINANZASIA_DATA_DIR";

    [Fact]
    public async Task AnUnexpectedCommandFailure_ShowsTheOverlayWithTheShortIdOfTheGuidInTheRealLog()
    {
        string root = Path.Combine(Path.GetTempPath(), "cdfia-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string? previous = Environment.GetEnvironmentVariable(_envVar);
        Environment.SetEnvironmentVariable(_envVar, root);
        try
        {
            (string ShortId, bool Visible, bool Open, string Log) result = await HeadlessUi.OnUiThread(() =>
            {
                var services = new ServiceCollection();
                services.AddLogging(b => b.ClearProviders());
                Bootstrap.ConfigureServices(services, new ConfigurationBuilder().Build());

                string log;
                string shortId;
                bool visible;
                bool open;
                using (ServiceProvider sp = services.BuildServiceProvider())
                {
                    var window = new MainWindow { DataContext = sp.GetRequiredService<ShellViewModel>(), Width = 1100, Height = 900 };
                    window.Show();
                    HeadlessUi.Flush();
                    ErrorOverlayView view = window.GetVisualDescendants().OfType<ErrorOverlayView>().Single();
                    Assert.False(view.IsEffectivelyVisible);

                    using IDisposable hooks = GlobalExceptionHooks.Install(sp.GetRequiredService<IUnexpectedErrorReporter>());
                    var exploding = new RelayCommand(() => throw new InvalidOperationException("e2e unexpected failure"));
                    Dispatcher.UIThread.Post(() => exploding.Execute(null));
                    Dispatcher.UIThread.RunJobs();
                    HeadlessUi.Flush();

                    ErrorOverlayViewModel overlay = sp.GetRequiredService<ErrorOverlayViewModel>();
                    shortId = overlay.ShortId;
                    open = overlay.IsOpen;
                    visible = view.IsEffectivelyVisible;
                    window.Close();
                    HeadlessUi.Flush();
                }

                log = ReadLogs(Path.Combine(root, "logs"));
                return Task.FromResult((shortId, visible, open, log));
            });

            Assert.True(result.Open);
            Assert.True(result.Visible);
            Assert.Matches("^[0-9a-f]{8}$", result.ShortId);

            Match error = Regex.Match(
                result.Log,
                @" ERR .*Unexpected error (?<id>[0-9a-f]{8}-[0-9a-f-]{27})",
                RegexOptions.Multiline);
            Assert.True(error.Success, "No ERR entry with a correlation id in the real log:\n" + result.Log);
            Assert.Equal(
                Guid.Parse(error.Groups["id"].Value).ToString("N")[..8],
                result.ShortId);
            Assert.Contains("e2e unexpected failure", result.Log, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(_envVar, previous);
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Best effort cleanup of a temp folder.
            }
        }
    }

    private static string ReadLogs(string directory) =>
        string.Concat(Directory.GetFiles(directory, "app-*.log").Order().Select(ReadShared));

    private static string ReadShared(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
