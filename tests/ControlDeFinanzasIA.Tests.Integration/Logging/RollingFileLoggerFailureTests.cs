using ControlDeFinanzasIA.Infrastructure.Logging;
using ControlDeFinanzasIA.Tests.Integration.Backup;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Logging;

[Collection(StderrCollection.Name)]
public sealed class RollingFileLoggerFailureTests
{
    private static readonly DateTime _now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LogsPathThatIsARegularFile_DisablesOnceWithoutThrowing()
    {
        using var temp = new TempDir();
        string logs = Path.Combine(temp.Path, "logs");
        File.WriteAllText(logs, "i am a file");

        AssertDisablesOnFirstFailure(new RollingFileLoggerOptions(logs));
    }

    [Fact]
    public void InvalidDirectoryName_DisablesOnceWithoutThrowing() =>
        AssertDisablesOnFirstFailure(new RollingFileLoggerOptions("bad\0dir"));

    [WindowsOnlyFact]
    public void LockedLogFile_DisablesAndNeverRetriesEvenAfterTheLockIsReleased()
    {
        using var temp = new TempDir();
        string file = Path.Combine(temp.Path, "app-20260930.log");
        File.WriteAllText(file, string.Empty);

        using var stderr = new StderrCapture();
        var hold = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var provider = new RollingFileLoggerProvider(
            new RollingFileLoggerOptions(temp.Path),
            new SettableClock(_now));
        ILogger logger = provider.CreateLogger("Locked");

        logger.LogInformation("one");
        Assert.True(provider.IsDisabled);

        hold.Dispose();
        logger.LogInformation("two");
        logger.LogError(new InvalidOperationException("x"), "three");

        Assert.Equal(0, new FileInfo(file).Length);
        Assert.Single(stderr.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void ConsoleProviderComposedInTheSameFactoryStillReceivesEntries()
    {
        using var temp = new TempDir();
        string logs = Path.Combine(temp.Path, "logs");
        File.WriteAllText(logs, "file, not directory");
        var other = new ListProvider();

        using var stderr = new StderrCapture();
        using ILoggerFactory factory = LoggerFactory.Create(b => b.AddProvider(other));
        var file = new RollingFileLoggerProvider(new RollingFileLoggerOptions(logs), new SettableClock(_now));
        factory.AddProvider(file);

        ILogger logger = factory.CreateLogger("Both");
        logger.LogWarning("still delivered");
        logger.LogWarning("and again");

        Assert.True(file.IsDisabled);
        Assert.Equal(2, other.Messages.Count);
        Assert.Equal("still delivered", other.Messages[0]);
    }

    [Fact]
    public void LogEnabledAndDispose_NeverThrowOnADisabledProvider()
    {
        using var temp = new TempDir();
        string logs = Path.Combine(temp.Path, "logs");
        File.WriteAllText(logs, "file");
        using var stderr = new StderrCapture();
        var provider = new RollingFileLoggerProvider(new RollingFileLoggerOptions(logs), new SettableClock(_now));
        ILogger logger = provider.CreateLogger("X");

        logger.LogInformation("trigger");

        Assert.False(logger.IsEnabled(LogLevel.Error));
        logger.LogCritical(new InvalidOperationException("e"), "after");
        provider.Dispose();
        provider.Dispose();
        logger.LogInformation("after dispose");
    }

    private static void AssertDisablesOnFirstFailure(RollingFileLoggerOptions options)
    {
        using var stderr = new StderrCapture();
        using var provider = new RollingFileLoggerProvider(options, new SettableClock(_now));
        ILogger logger = provider.CreateLogger("Failing");

        Assert.False(provider.IsDisabled);
        Exception? thrown = Record.Exception(() =>
        {
            logger.LogInformation("first");
            logger.LogError(new InvalidOperationException("boom"), "second");
            _ = logger.IsEnabled(LogLevel.Information);
        });

        Assert.Null(thrown);
        Assert.True(provider.IsDisabled);
        Assert.Single(stderr.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed class ListProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new ListLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class ListLogger(List<string> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                sink.Add(formatter(state, exception));
        }
    }
}
