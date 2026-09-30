using System.Text.RegularExpressions;
using ControlDeFinanzasIA.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Logging;

public sealed class LogEntryFormatterTests
{
    private static readonly DateTime _at = new(2026, 9, 30, 14, 3, 11, 123, DateTimeKind.Utc);

    [Fact]
    public void Format_ProducesTimestampLevelCategoryEventIdAndMessage()
    {
        string line = LogEntryFormatter.Format(_at, LogLevel.Information, "Cat.Name", new EventId(7), "hello", null, 1000);

        Assert.Equal("2026-09-30T14:03:11.123Z INF Cat.Name[7] hello", line);
    }

    [Theory]
    [InlineData(LogLevel.Trace, "TRC")]
    [InlineData(LogLevel.Debug, "DBG")]
    [InlineData(LogLevel.Information, "INF")]
    [InlineData(LogLevel.Warning, "WRN")]
    [InlineData(LogLevel.Error, "ERR")]
    [InlineData(LogLevel.Critical, "CRT")]
    public void Format_UsesThreeLetterLevels(LogLevel level, string expected)
    {
        string line = LogEntryFormatter.Format(_at, level, "C", default, "m", null, 1000);

        Assert.StartsWith("2026-09-30T14:03:11.123Z " + expected + " ", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_AppendsExceptionWithIndentedContinuationLinesAndInnerChain()
    {
        Exception ex = Throw();

        string entry = LogEntryFormatter.Format(_at, LogLevel.Error, "C", default, "boom", ex, 64 * 1024);

        string[] lines = entry.Split('\n');
        Assert.StartsWith("2026-09-30T14:03:11.123Z ERR C[0] boom", lines[0], StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException: outer", entry, StringComparison.Ordinal);
        Assert.Contains("System.ArgumentException: inner", entry, StringComparison.Ordinal);
        Assert.True(lines.Length > 2);
        Assert.All(lines.Skip(1), l => Assert.StartsWith("    ", l, StringComparison.Ordinal));
    }

    [Fact]
    public void Format_MultiLineMessageKeepsEveryContinuationIndented()
    {
        string entry = LogEntryFormatter.Format(_at, LogLevel.Warning, "C", default, "first\nsecond\r\nthird", null, 1000);

        string[] lines = entry.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Matches(new Regex(@"^\d{4}-\d\d-\d\dT"), lines[0]);
        Assert.StartsWith("    second", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("    third", lines[2], StringComparison.Ordinal);
        Assert.DoesNotContain('\r', entry);
    }

    [Fact]
    public void Format_TruncatesEntriesOverTheCap()
    {
        string entry = LogEntryFormatter.Format(_at, LogLevel.Information, "C", default, new string('x', 500), null, 100);

        Assert.EndsWith("…[truncated]", entry, StringComparison.Ordinal);
        Assert.True(entry.Length <= 100 + "…[truncated]".Length);
    }

    [Fact]
    public void Format_NormalizesLocalKindValuesToUtc()
    {
        // The same instant expressed as local time must print the identical UTC text, whatever the machine zone is.
        DateTime local = _at.ToLocalTime();

        string fromLocal = LogEntryFormatter.Format(local, LogLevel.Information, "C", default, "m", null, 1000);
        string fromUtc = LogEntryFormatter.Format(_at, LogLevel.Information, "C", default, "m", null, 1000);

        Assert.Equal(fromUtc, fromLocal);
        Assert.StartsWith("2026-09-30T14:03:11.123Z", fromLocal, StringComparison.Ordinal);
    }

    private static Exception Throw()
    {
        try
        {
            try
            {
                throw new ArgumentException("inner");
            }
            catch (ArgumentException inner)
            {
                throw new InvalidOperationException("outer", inner);
            }
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }
}
