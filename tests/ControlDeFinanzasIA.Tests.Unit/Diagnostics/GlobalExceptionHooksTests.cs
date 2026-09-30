using Avalonia.Threading;
using ControlDeFinanzasIA.Tests.Unit.UI;
using ControlDeFinanzasIA.UI.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Unit.Diagnostics;

/// <summary>
/// F-021 S3 (R-8, S-15..S-17, T4, T17): the REAL hooks over the REAL reporter and the REAL rolling file provider.
/// Every test installs the hooks itself and disposes them.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class GlobalExceptionHooksTests
{
    [Fact]
    public async Task UiThreadFailure_IsHandled_OpensTheOverlay_AndTheDispatcherKeepsRunning()
    {
        using var rig = new ReporterRig();
        bool secondJobRan = false;

        await HeadlessUi.OnUiThread(() =>
        {
            using IDisposable hooks = GlobalExceptionHooks.Install(rig.Reporter, rig.Clock);
            Dispatcher.UIThread.Post(() => throw new InvalidOperationException("command exploded"));
            Dispatcher.UIThread.Post(() => secondJobRan = true);
            Dispatcher.UIThread.RunJobs();
            return Task.FromResult(true);
        });

        Assert.True(secondJobRan);
        Assert.True(rig.Overlay.IsOpen);
        Assert.Equal(8, rig.Overlay.ShortId.Length);
        string log = rig.ReadLog();
        Assert.Contains(rig.Overlay.CorrelationId.ToString("D"), log, StringComparison.Ordinal);
        Assert.Contains("command exploded", log, StringComparison.Ordinal);
        Assert.Contains(" ERR ", log, StringComparison.Ordinal);
    }

    [Fact]
    public void Breaker_TwentyFirstReportInTenSeconds_IsNotHandled_AndLogsCriticalOnce()
    {
        using var rig = new ReporterRig();
        using var subscription = (IDisposable)GlobalExceptionHooks.Install(rig.Reporter, rig.Clock);
        var hooks = (GlobalExceptionHooks.Subscription)subscription;

        for (int i = 0; i < GlobalExceptionHooks.Subscription.BreakerLimit; i++)
        {
            Assert.True(hooks.HandleUiException(new InvalidOperationException("loop " + i)));
            rig.Clock.UtcNow = rig.Clock.UtcNow.AddMilliseconds(100);
        }

        Assert.False(hooks.HandleUiException(new InvalidOperationException("one too many")));
        Assert.False(hooks.HandleUiException(new InvalidOperationException("and another")));

        string log = rig.ReadLog();
        Assert.Equal(1, CountOccurrences(log, " CRT "));
        Assert.Equal(GlobalExceptionHooks.Subscription.BreakerLimit, CountOccurrences(log, " ERR "));

        // After the window passes the breaker admits reports again.
        rig.Clock.UtcNow = rig.Clock.UtcNow.AddSeconds(11);
        Assert.True(hooks.HandleUiException(new InvalidOperationException("recovered")));
    }

    [Fact]
    public void UnobservedTask_IsLoggedAtWarningWithAGuid_MarkedObserved_AndNoOverlayOpens()
    {
        using var rig = new ReporterRig();
        using var subscription = (IDisposable)GlobalExceptionHooks.Install(rig.Reporter, rig.Clock);
        var hooks = (GlobalExceptionHooks.Subscription)subscription;
        var args = new UnobservedTaskExceptionEventArgs(new AggregateException(new InvalidOperationException("forgotten task")));

        hooks.HandleUnobservedTask(args);

        Assert.True(args.Observed);
        Assert.False(rig.Overlay.IsOpen);
        string log = rig.ReadLog();
        Assert.Contains(" WRN ", log, StringComparison.Ordinal);
        Assert.Contains("forgotten task", log, StringComparison.Ordinal);
        Assert.Matches(@"[0-9a-f]{8}-[0-9a-f]{4}-", log);
    }

    [Fact]
    public void AppDomainFailure_IsLoggedAtCriticalWithAGuid_NoUi_AndDoesNotThrow()
    {
        using var rig = new ReporterRig();
        using var subscription = (IDisposable)GlobalExceptionHooks.Install(rig.Reporter, rig.Clock);
        var hooks = (GlobalExceptionHooks.Subscription)subscription;

        hooks.HandleDomainException(new InvalidOperationException("fatal elsewhere"));
        hooks.HandleDomainException("not an exception object");

        Assert.False(rig.Overlay.IsOpen);
        string log = rig.ReadLog();
        Assert.Equal(2, CountOccurrences(log, " CRT "));
        Assert.Contains("fatal elsewhere", log, StringComparison.Ordinal);
        Assert.Contains("not an exception object", log, StringComparison.Ordinal);
    }

    [Fact]
    public void ReporterThatThrows_NeverLetsAHookThrow()
    {
        using IDisposable subscription = GlobalExceptionHooks.Install(new ThrowingReporter(), new ControlDeFinanzasIA.Tests.Unit.TestDoubles.FakeClock { UtcNow = DateTime.UtcNow });
        var hooks = (GlobalExceptionHooks.Subscription)subscription;
        var args = new UnobservedTaskExceptionEventArgs(new AggregateException(new InvalidOperationException("x")));

        Assert.False(hooks.HandleUiException(new InvalidOperationException("ui")));
        hooks.HandleDomainException(new InvalidOperationException("domain"));
        hooks.HandleUnobservedTask(args);

        Assert.True(args.Observed);
    }


    private static int CountOccurrences(string text, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private sealed class ThrowingReporter : IUnexpectedErrorReporter
    {
        public Guid Report(Exception exception, string source) => throw new InvalidOperationException("reporter down");

        public Guid LogOnly(Exception exception, string source, LogLevel level) =>
            throw new InvalidOperationException("reporter down");
    }
}
