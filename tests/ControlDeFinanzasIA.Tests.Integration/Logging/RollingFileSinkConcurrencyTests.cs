using System.Text.RegularExpressions;
using ControlDeFinanzasIA.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Logging;

public sealed class RollingFileSinkConcurrencyTests
{
    [Fact]
    public void ThirtyTwoThreadsProduceIntactNonInterleavedLines()
    {
        using var temp = new TempDir();
        var clock = new SettableClock(new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc));
        var provider = new RollingFileLoggerProvider(new RollingFileLoggerOptions(temp.Path), clock);
        ILogger logger = provider.CreateLogger("Concurrency");

        Parallel.For(
            0,
            32,
            new ParallelOptions { MaxDegreeOfParallelism = 32 },
            t =>
            {
                for (int i = 0; i < 200; i++)
                {
                    logger.LogInformation("thread {Thread} item {Item} payload-{Payload}", t, i, new string('p', 40));
                }
            });

        provider.Dispose();

        string file = Assert.Single(Directory.GetFiles(temp.Path));
        string[] lines = File.ReadAllLines(file);
        Assert.Equal(6400, lines.Length);
        var pattern = new Regex(@"^2026-09-30T12:00:00\.000Z INF Concurrency\[0\] thread \d+ item \d+ payload-p{40}$");
        Assert.All(lines, l => Assert.Matches(pattern, l));
    }
}
