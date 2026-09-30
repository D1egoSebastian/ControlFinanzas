using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// The single headless Avalonia session shared by every headless view test of the suite (Avalonia allows one
/// <c>Application</c> per process). It runs the REAL <c>App</c> resources and styles with no window system.
/// </summary>
public static class HeadlessUi
{
    public const string CollectionName = "Headless Avalonia";

    private static readonly Lazy<HeadlessUnitTestSession> _session =
        new(() => HeadlessUnitTestSession.StartNew(typeof(ImportHeadlessEntry)));

    /// <summary>Runs <paramref name="body"/> on the Avalonia UI thread of the shared session.</summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="body">The body.</param>
    public static async Task<T> OnUiThread<T>(Func<Task<T>> body)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;

        T? value = await _session.Value.Dispatch(
            async () =>
            {
                try
                {
                    T result = await body();

                    // Let fire-and-forget loads started by the view-models land, then render once more while the
                    // application services are still bound: a render left pending would run after they are released.
                    await Task.Delay(100);
                    Flush();
                    return result;
                }
                catch (Exception ex)
                {
                    // The session's own cleanup can throw while windows are still rendering and would mask the
                    // real failure of the test, so it is carried out of the dispatch and rethrown here.
                    failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex);
                    return default;
                }
            },
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(60));

        failure?.Throw();
        return value!;
    }

    /// <summary>Processes pending UI jobs (bindings, layout).</summary>
    public static void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Shows <paramref name="content"/> in a window whose data context is <paramref name="dataContext"/>.</summary>
    /// <param name="content">The view.</param>
    /// <param name="dataContext">The view-model.</param>
    public static Window Show(Control content, object dataContext)
    {
        var window = new Window { Width = 1100, Height = 900, Content = content, DataContext = dataContext };
        window.Show();
        Flush();
        return window;
    }

    /// <summary>Closes a window when disposed.</summary>
    /// <param name="window">The window.</param>
    public sealed class Scope(Window window) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            window.Close();
            Flush();
        }
    }
}

/// <summary>
/// xUnit collection that serializes every headless view test class: they share one Avalonia session and one
/// dispatcher, and a dispatch that finishes resets the application state for any other in flight.
/// </summary>
[CollectionDefinition(HeadlessUi.CollectionName, DisableParallelization = true)]
public sealed class HeadlessAvaloniaCollection
{
}
