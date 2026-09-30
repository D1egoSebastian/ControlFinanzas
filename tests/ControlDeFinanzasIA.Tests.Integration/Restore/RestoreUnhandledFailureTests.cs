using ControlDeFinanzasIA.Infrastructure.Logging;
using ControlDeFinanzasIA.Tests.Integration.Backup;
using ControlDeFinanzasIA.Tests.Integration.Logging;
using ControlDeFinanzasIA.UI.Diagnostics;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Restore;

/// <summary>
/// F-021 S3 (R-8, R-11, S-23, T4): an UNEXPECTED exception (not an I/O or SQLite one) escaping the REAL
/// <c>SqliteBackupService.RestoreAsync</c> swap leaves the original database byte-identical and is then reported
/// by the REAL reporter with a Guid that appears in the REAL log. The fault is injected through the
/// <c>IBackupFileSystem</c> seam the Phase 19 tests already use; no Phase 19 production code is involved.
/// </summary>
[Collection(StderrCollection.Name)]
public sealed class RestoreUnhandledFailureTests : IDisposable
{
    private readonly RestoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task UnexpectedExceptionInTheSwap_LeavesTheOriginalIntact_AndIsReportedWithAGuidInTheLog()
    {
        byte[] zip = await _h.CreateZipAsync();
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace").Throws(new InvalidOperationException("unexpected swap failure"));

        InvalidOperationException escaped =
            await Assert.ThrowsAsync<InvalidOperationException>(() => _h.RestoreAsync(zip, fs));

        // The original is intact before anything reports the failure.
        _h.AssertLiveIsOriginal(original);
        _h.AssertWorkDirectoryEmpty();
        Assert.NotEmpty(_h.SafetyZips());

        using var temp = new TempDir();
        using var stderr = new StderrCapture();
        var clock = new SettableClock(new DateTime(2026, 9, 30, 14, 3, 11, DateTimeKind.Utc));
        using var provider = new RollingFileLoggerProvider(new RollingFileLoggerOptions(temp.Path), clock);
        using ILoggerFactory factory = LoggerFactory.Create(b => b.AddProvider(provider));
        var overlay = new ErrorOverlayViewModel("1.0.0");
        var reporter = new UnexpectedErrorReporter(factory.CreateLogger<UnexpectedErrorReporter>(), clock, overlay);
        using IDisposable hooks = GlobalExceptionHooks.Install(reporter, clock);

        Guid id = reporter.Report(escaped, "ui");

        string file = Assert.Single(Directory.GetFiles(temp.Path, "app-*.log"));
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        string log = new StreamReader(stream).ReadToEnd();
        Assert.Contains(id.ToString("D"), log, StringComparison.Ordinal);
        Assert.Contains("unexpected swap failure", log, StringComparison.Ordinal);
        Assert.True(overlay.IsOpen);
        _h.AssertLiveIsOriginal(original);
    }
}
