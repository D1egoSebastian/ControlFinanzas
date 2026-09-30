using System.Text.RegularExpressions;
using ControlDeFinanzasIA.Infrastructure.Logging;
using ControlDeFinanzasIA.Tests.Integration.Backup;

namespace ControlDeFinanzasIA.Tests.Integration.Logging;

[Collection(StderrCollection.Name)]
public sealed class RollingFileSinkTests
{
    private static readonly DateTime _day1 = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);

    private static RollingFileSink Sink(string dir, long max = 1024 * 1024, int keep = 5) =>
        new(new RollingFileLoggerOptions(dir, max, keep));

    private static string[] Names(string dir) =>
        Directory.GetFiles(dir).Select(Path.GetFileName).OfType<string>().OrderBy(n => n, StringComparer.Ordinal).ToArray();

    [Fact]
    public void FirstWrite_CreatesTheDirectoryAndTheUtcDatedFile()
    {
        using var temp = new TempDir();
        string dir = Path.Combine(temp.Path, "logs");
        using (RollingFileSink sink = Sink(dir))
        {
            sink.Write(_day1, "line one");
        }

        Assert.Equal(new[] { "app-20260930.log" }, Names(dir));
        Assert.Equal("line one\n", File.ReadAllText(Path.Combine(dir, "app-20260930.log")));
    }

    [Fact]
    public void Write_RollsToIndexedFilesAtTheByteCap()
    {
        using var temp = new TempDir();
        using (RollingFileSink sink = Sink(temp.Path, max: 50))
        {
            for (int i = 0; i < 7; i++)
            {
                sink.Write(_day1, new string((char)('a' + i), 19)); // 20 bytes with the newline
            }
        }

        Assert.Equal(
            new[] { "app-20260930.1.log", "app-20260930.2.log", "app-20260930.3.log", "app-20260930.log" },
            Names(temp.Path));
        foreach (string file in Directory.GetFiles(temp.Path))
        {
            Assert.True(new FileInfo(file).Length <= 50, file);
        }

        // Nothing is lost across the rolls: 7 intact entries in total.
        int entries = Directory.GetFiles(temp.Path).Sum(f => File.ReadAllLines(f).Length);
        Assert.Equal(7, entries);
    }

    [Fact]
    public void Write_AnEntryLargerThanTheCapIsStillWrittenWholeInItsOwnFile()
    {
        using var temp = new TempDir();
        using (RollingFileSink sink = Sink(temp.Path, max: 10))
        {
            sink.Write(_day1, new string('z', 100));
            sink.Write(_day1, "next");
        }

        Assert.Equal(new[] { "app-20260930.1.log", "app-20260930.log" }, Names(temp.Path));
        Assert.Equal(101, new FileInfo(Path.Combine(temp.Path, "app-20260930.log")).Length);
    }

    [Fact]
    public void Write_AcrossUtcMidnightOpensTheNextDatedFile()
    {
        using var temp = new TempDir();
        using (RollingFileSink sink = Sink(temp.Path))
        {
            sink.Write(new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc), "before");
            sink.Write(new DateTime(2026, 10, 1, 0, 0, 1, DateTimeKind.Utc), "after");
        }

        Assert.Equal(new[] { "app-20260930.log", "app-20261001.log" }, Names(temp.Path));
        Assert.Equal("after\n", File.ReadAllText(Path.Combine(temp.Path, "app-20261001.log")));
    }

    [Fact]
    public void FreshSink_ResumesTodaysHighestIndexWhenUnderTheCap()
    {
        using var temp = new TempDir();
        File.WriteAllText(Path.Combine(temp.Path, "app-20260930.log"), new string('x', 500));
        File.WriteAllText(Path.Combine(temp.Path, "app-20260930.1.log"), "seed\n");

        using (RollingFileSink sink = Sink(temp.Path, max: 1000))
        {
            sink.Write(_day1, "resumed");
        }

        Assert.Equal(new[] { "app-20260930.1.log", "app-20260930.log" }, Names(temp.Path));
        Assert.Equal("seed\nresumed\n", File.ReadAllText(Path.Combine(temp.Path, "app-20260930.1.log")));
    }

    [Fact]
    public void FreshSink_StartsANewIndexWhenTheHighestIsFull()
    {
        using var temp = new TempDir();
        File.WriteAllText(Path.Combine(temp.Path, "app-20260930.log"), new string('x', 200));

        using (RollingFileSink sink = Sink(temp.Path, max: 100))
        {
            sink.Write(_day1, "next");
        }

        Assert.Equal(new[] { "app-20260930.1.log", "app-20260930.log" }, Names(temp.Path));
    }

    [Fact]
    public void Retention_KeepsTheNewestFiveByParsedNameIgnoringTimestamps()
    {
        using var temp = new TempDir();
        string[] old =
        [
            "app-20260920.log", "app-20260921.log", "app-20260922.log", "app-20260923.log",
            "app-20260924.log", "app-20260925.log", "app-20260926.log", "app-20260929.log",
        ];
        for (int i = 0; i < old.Length; i++)
        {
            string path = Path.Combine(temp.Path, old[i]);
            File.WriteAllText(path, "old\n");

            // Inverted timestamps: the oldest name gets the newest write time.
            File.SetLastWriteTimeUtc(path, new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-i));
        }

        File.WriteAllText(Path.Combine(temp.Path, "notes.txt"), "not a log");

        using (RollingFileSink sink = Sink(temp.Path, keep: 5))
        {
            sink.Write(_day1, "today");
        }

        string[] logs = Names(temp.Path).Where(n => n.StartsWith("app-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(
            new[] { "app-20260924.log", "app-20260925.log", "app-20260926.log", "app-20260929.log", "app-20260930.log" },
            logs);
        Assert.Contains("notes.txt", Names(temp.Path));
    }

    [Fact]
    public void Retention_OrdersIndexesNumericallyNotLexically()
    {
        using var temp = new TempDir();
        for (int i = 0; i <= 11; i++)
        {
            string name = i == 0 ? "app-20260930.log" : $"app-20260930.{i}.log";
            File.WriteAllText(Path.Combine(temp.Path, name), new string('x', 10));
        }

        using (RollingFileSink sink = Sink(temp.Path, max: 10, keep: 5))
        {
            sink.Write(_day1, "new"); // highest (11) is full -> index 12 -> prune
        }

        Assert.Equal(
            new[] { "app-20260930.10.log", "app-20260930.11.log", "app-20260930.12.log", "app-20260930.8.log", "app-20260930.9.log" },
            Names(temp.Path));
    }

    [WindowsOnlyFact]
    public void Retention_DeleteFailureIsReportedOnceAndDoesNotDisableTheSink()
    {
        using var temp = new TempDir();
        string locked = Path.Combine(temp.Path, "app-20260901.log");
        for (int day = 1; day <= 7; day++)
        {
            File.WriteAllText(Path.Combine(temp.Path, $"app-2026090{day}.log"), "old\n");
        }

        using var stderr = new StderrCapture();
        using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);
        using RollingFileSink sink = Sink(temp.Path, max: 30, keep: 2);

        sink.Write(_day1, "first entry");
        sink.Write(_day1, "second entry that rolls");
        sink.Write(_day1, "third entry that rolls again");

        Assert.False(sink.IsDisabled);
        sink.Dispose();
        Assert.Single(Regex.Matches(stderr.Text, "retention", RegexOptions.IgnoreCase));
        Assert.True(File.Exists(locked));
        Assert.Contains("third entry", File.ReadAllText(Path.Combine(temp.Path, "app-20260930.2.log")), StringComparison.Ordinal);
    }
}
