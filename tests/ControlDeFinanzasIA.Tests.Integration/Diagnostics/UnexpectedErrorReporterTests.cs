using System.Text.RegularExpressions;
using ControlDeFinanzasIA.Infrastructure.Logging;
using ControlDeFinanzasIA.Tests.Integration.Logging;
using ControlDeFinanzasIA.Tests.Integration.TestDoubles;
using ControlDeFinanzasIA.UI.Diagnostics;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Diagnostics;

/// <summary>
/// F-021 S3 (R-7): the REAL <see cref="UnexpectedErrorReporter"/> over the REAL rolling file provider and the REAL
/// <see cref="ErrorOverlayViewModel"/> as presenter.
/// </summary>
[Collection(StderrCollection.Name)]
public sealed class UnexpectedErrorReporterTests
{
    private static readonly DateTime _now = new(2026, 9, 30, 14, 3, 11, DateTimeKind.Utc);

    private static (UnexpectedErrorReporter Reporter, RollingFileLoggerProvider Provider, ErrorOverlayViewModel Overlay, ILoggerFactory Factory)
        Build(string logsDirectory, IErrorPresenter? presenter = null)
    {
        var clock = new SettableClock(_now);
        var provider = new RollingFileLoggerProvider(new RollingFileLoggerOptions(logsDirectory), clock);
        ILoggerFactory factory = LoggerFactory.Create(b => b.AddProvider(provider));
        var overlay = new ErrorOverlayViewModel(new RecordingClipboardService(), "1.0.0");
        var reporter = new UnexpectedErrorReporter(
            factory.CreateLogger<UnexpectedErrorReporter>(),
            clock,
            presenter ?? overlay);
        return (reporter, provider, overlay, factory);
    }

    private static string ReadLog(string logsDirectory)
    {
        string file = Assert.Single(Directory.GetFiles(logsDirectory, "app-*.log"));
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Report_WritesFullGuidTypeMessageAndStackToTheRealLog_AndShowsTheShortId()
    {
        using var temp = new TempDir();
        using var stderr = new StderrCapture();
        (UnexpectedErrorReporter reporter, RollingFileLoggerProvider provider, ErrorOverlayViewModel overlay, ILoggerFactory factory) =
            Build(temp.Path);
        using (factory)
        using (provider)
        {
            Exception failure = Capture(() => throw new InvalidOperationException("outer boom", new ArgumentException("inner cause")));

            Guid id = reporter.Report(failure, "ui");

            string log = ReadLog(temp.Path);
            Assert.Matches(new Regex(@"^2026-09-30T14:03:11\.\d{3}Z ERR ", RegexOptions.Multiline), log);
            Assert.Contains(id.ToString("D"), log, StringComparison.Ordinal);
            Assert.Contains("System.InvalidOperationException: outer boom", log, StringComparison.Ordinal);
            Assert.Contains("System.ArgumentException: inner cause", log, StringComparison.Ordinal);
            Assert.Contains("    at ", log, StringComparison.Ordinal);
            Assert.Contains("in ui", log, StringComparison.Ordinal);

            string shortId = CorrelationId.Short(id);
            Assert.Matches("^[0-9a-f]{8}$", shortId);
            Assert.Equal(id.ToString("N")[..8], shortId);
            Assert.True(overlay.IsOpen);
            Assert.Equal(id, overlay.CorrelationId);
            Assert.Equal(_now, overlay.OccurredAtUtc);
            Assert.Equal(shortId, overlay.ShortId);
        }
    }

    [Fact]
    public void Report_EachCallGetsItsOwnGuid()
    {
        using var temp = new TempDir();
        using var stderr = new StderrCapture();
        (UnexpectedErrorReporter reporter, RollingFileLoggerProvider provider, _, ILoggerFactory factory) = Build(temp.Path);
        using (factory)
        using (provider)
        {
            Guid first = reporter.Report(new InvalidOperationException("a"), "ui");
            Guid second = reporter.Report(new InvalidOperationException("b"), "ui");

            Assert.NotEqual(first, second);
            string log = ReadLog(temp.Path);
            Assert.Contains(first.ToString("D"), log, StringComparison.Ordinal);
            Assert.Contains(second.ToString("D"), log, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Report_PresenterThatThrows_StillLeavesTheLogEntryAndDoesNotThrow()
    {
        using var temp = new TempDir();
        using var stderr = new StderrCapture();
        (UnexpectedErrorReporter reporter, RollingFileLoggerProvider provider, _, ILoggerFactory factory) =
            Build(temp.Path, new ThrowingPresenter());
        using (factory)
        using (provider)
        {
            Guid id = reporter.Report(new InvalidOperationException("kept"), "ui");

            Assert.Contains(id.ToString("D"), ReadLog(temp.Path), StringComparison.Ordinal);
            Assert.Contains("Error overlay failed", stderr.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Report_WithADisabledFileLogger_DoesNotThrowAndWritesAStderrLineWithTheGuid()
    {
        using var temp = new TempDir();
        string logs = Path.Combine(temp.Path, "logs");
        File.WriteAllText(logs, "a file where the directory should be");
        using var stderr = new StderrCapture();
        (UnexpectedErrorReporter reporter, RollingFileLoggerProvider provider, ErrorOverlayViewModel overlay, ILoggerFactory factory) =
            Build(logs);
        using (factory)
        using (provider)
        {
            Guid id = reporter.Report(new InvalidOperationException("disk gone"), "ui");

            Assert.True(provider.IsDisabled);
            Assert.Contains(id.ToString("D"), stderr.Text, StringComparison.Ordinal);
            Assert.Equal(id, overlay.CorrelationId);
        }
    }

    [Fact]
    public void LogOnly_WritesAtTheRequestedLevelAndNeverTouchesThePresenter()
    {
        using var temp = new TempDir();
        using var stderr = new StderrCapture();
        (UnexpectedErrorReporter reporter, RollingFileLoggerProvider provider, ErrorOverlayViewModel overlay, ILoggerFactory factory) =
            Build(temp.Path);
        using (factory)
        using (provider)
        {
            Guid id = reporter.LogOnly(new InvalidOperationException("quiet"), "task", LogLevel.Warning);

            string log = ReadLog(temp.Path);
            Assert.Contains(" WRN ", log, StringComparison.Ordinal);
            Assert.Contains(id.ToString("D"), log, StringComparison.Ordinal);
            Assert.False(overlay.IsOpen);
        }
    }

    private static Exception Capture(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            return ex;
        }

        throw new InvalidOperationException("The action did not throw.");
    }

    private sealed class ThrowingPresenter : IErrorPresenter
    {
        public void Present(Guid correlationId, DateTime occurredAtUtc) =>
            throw new InvalidOperationException("presenter down");
    }
}
