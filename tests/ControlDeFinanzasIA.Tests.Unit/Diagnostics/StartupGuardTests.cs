using System.Text.RegularExpressions;
using ControlDeFinanzasIA.UI.Diagnostics;

namespace ControlDeFinanzasIA.Tests.Unit.Diagnostics;

/// <summary>
/// F-021 S3 (R-9, S-18, T7): the REAL <see cref="StartupGuard"/> and <see cref="StartupFailureWriter"/> with an
/// injected stderr, clock and a private fallback folder (the real %TEMP% is never polluted).
/// </summary>
public sealed class StartupGuardTests : IDisposable
{
    private static readonly DateTime _utc = new(2026, 9, 30, 14, 3, 11, DateTimeKind.Utc);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdfia-startup-" + Guid.NewGuid().ToString("N"));

    public StartupGuardTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of a temp folder.
        }
    }

    [Fact]
    public void BodyThatReturnsZero_ReturnsZeroAndWritesNothing()
    {
        var stderr = new StringWriter();

        int code = StartupGuard.Run(() => 0, () => _root, stderr, () => _utc, Path.Combine(_root, "fallback"));

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Empty(Directory.GetFiles(_root, "startup-error-*.log"));
    }

    [Fact]
    public void BodyThatThrows_ReturnsOne_AndWritesTheReportWithVersionTimeOsGuidAndException()
    {
        const string secretName = "CDFIA_TEST_SECRET_S3";
        Environment.SetEnvironmentVariable(secretName, "sk-SENTINEL-startup-9f3a");
        try
        {
            var stderr = new StringWriter();

            int code = StartupGuard.Run(
                () => throw new InvalidOperationException("host build exploded"),
                () => _root,
                stderr,
                () => _utc,
                Path.Combine(_root, "fallback"));

            Assert.Equal(1, code);
            string file = Assert.Single(Directory.GetFiles(_root, "startup-error-*.log"));
            Assert.Equal("startup-error-20260930T140311Z.log", Path.GetFileName(file));
            string content = File.ReadAllText(file);
            Assert.Contains("ControlDeFinanzasIA " + AppVersionInfo.Current, content, StringComparison.Ordinal);
            Assert.Contains("Time (UTC): 2026-09-30T14:03:11Z", content, StringComparison.Ordinal);
            Assert.Contains("OS: ", content, StringComparison.Ordinal);
            Assert.Contains("System.InvalidOperationException: host build exploded", content, StringComparison.Ordinal);
            Assert.DoesNotContain("sk-SENTINEL-startup-9f3a", content, StringComparison.Ordinal);
            Assert.DoesNotContain(secretName, content, StringComparison.Ordinal);

            Match id = Regex.Match(content, @"Error ID: (?<id>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})");
            Assert.True(id.Success);
            string line = stderr.ToString().Trim();
            Assert.Contains(id.Groups["id"].Value, line, StringComparison.Ordinal);
            Assert.Contains(file, line, StringComparison.Ordinal);
            Assert.DoesNotContain("\n", line, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(secretName, null);
        }
    }

    [Fact]
    public void UnwritablePreferredDirectory_FallsBackToTheFallbackFolder()
    {
        string blocker = Path.Combine(_root, "not-a-directory");
        File.WriteAllText(blocker, "regular file");
        string fallback = Path.Combine(_root, "fallback");
        var stderr = new StringWriter();

        int code = StartupGuard.Run(
            () => throw new IOException("db unreachable"),
            () => blocker,
            stderr,
            () => _utc,
            fallback);

        Assert.Equal(1, code);
        string file = Assert.Single(Directory.GetFiles(fallback, "startup-error-*.log"));
        Assert.Contains("db unreachable", File.ReadAllText(file), StringComparison.Ordinal);
        Assert.Contains(file, stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DoubleFailure_PrintsTheExceptionToStderrOnly_AndStillReturnsOne()
    {
        string blockerA = Path.Combine(_root, "blocker-a");
        string blockerB = Path.Combine(_root, "blocker-b");
        File.WriteAllText(blockerA, "file");
        File.WriteAllText(blockerB, "file");
        var stderr = new StringWriter();

        int code = StartupGuard.Run(
            () => throw new InvalidOperationException("nowhere to write"),
            () => blockerA,
            stderr,
            () => _utc,
            blockerB);

        Assert.Equal(1, code);
        string text = stderr.ToString();
        Assert.Contains("could not be written", text, StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException: nowhere to write", text, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_root, "startup-error-*.log", SearchOption.AllDirectories));
    }

    [Fact]
    public void OverrideReaderThatThrows_StillReportsToFallbackAndReturnsOne()
    {
        string fallback = Path.Combine(_root, "fallback");
        var stderr = new StringWriter();

        int code = StartupGuard.Run(
            () => throw new InvalidOperationException("boom"),
            () => throw new InvalidOperationException("env unreadable"),
            stderr,
            () => _utc,
            fallback);

        Assert.Equal(1, code);
        string file = Assert.Single(Directory.GetFiles(fallback, "startup-error-*.log"));
        Assert.Contains("System.InvalidOperationException: boom", File.ReadAllText(file), StringComparison.Ordinal);
        Assert.Contains(file, stderr.ToString(), StringComparison.Ordinal);
    }
}
