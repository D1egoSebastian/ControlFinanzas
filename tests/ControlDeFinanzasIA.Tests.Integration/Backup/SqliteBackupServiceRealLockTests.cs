using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Infrastructure.Backup;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// F-019 S4b real file-lock test (threat T8): no fake file system. A handle opened WITHOUT
/// <c>FileShare.Delete</c> on the live database blocks the real <c>ReplaceFile</c>, exactly like SQLite's
/// own handle, an antivirus scan or a backup tool would.
/// </summary>
public sealed class SqliteBackupServiceRealLockTests : IDisposable
{
    private readonly RestoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    [WindowsOnlyFact]
    public async Task RealLock_OnTheLiveFile_FailsClosedAfterTheWholeScheduleThenSucceedsOnceReleased()
    {
        byte[] zip = await _h.CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_h.LivePath);
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        Dictionary<string, List<string>> mutated = BackupTestDb.Snapshot(_h.LivePath);

        Result blocked;
        using (var lockHandle = new FileStream(_h.LivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            Assert.False(RestoreHarness.CanOpenExclusively(_h.LivePath), "Precondition: the lock must be effective.");

            blocked = await _h.RestoreAsync(zip, new PhysicalBackupFileSystem());

            Assert.Equal(original, await Task.Run(() => BackupTestDb.Sha256Of(_h.LivePath)));
        }

        _h.AssertFailedClosed(blocked, original, expectSafetyZip: true);
        RestoreHarness.AssertSnapshotsEqual(mutated, BackupTestDb.Snapshot(_h.LivePath));
        Assert.Equal(
            new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(1800) },
            _h.Delays);
        Assert.Contains(_h.Logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("could not be replaced", StringComparison.Ordinal));
        Assert.Empty(Directory.GetFiles(_h.Db.DataDirectory, "data.db.*.bak"));

        Result released = await _h.RestoreAsync(zip, new PhysicalBackupFileSystem());

        Assert.True(released.IsSuccess, released.Error?.Code + ": " + released.Error?.Message);
        RestoreHarness.AssertSnapshotsEqual(atExport, BackupTestDb.Snapshot(_h.LivePath));
        Assert.Equal(original, BackupTestDb.Sha256Of(_h.BakPath));
        Assert.Equal(2, _h.SafetyZips().Length);
        _h.AssertWorkDirectoryEmpty();
    }

    [WindowsOnlyFact]
    public async Task RealLock_ReleasedDuringTheBackoff_LetsALaterAttemptSucceed()
    {
        byte[] zip = await _h.CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_h.LivePath);
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        FileStream? lockHandle = new(_h.LivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        _h.OnDelay = () =>
        {
            if (_h.Delays.Count == 2)
            {
                lockHandle?.Dispose();
                lockHandle = null;
            }
        };

        try
        {
            Result result = await _h.RestoreAsync(zip, new PhysicalBackupFileSystem());

            Assert.True(result.IsSuccess, result.Error?.Code + ": " + result.Error?.Message);
            Assert.Equal(2, _h.Delays.Count);
            RestoreHarness.AssertSnapshotsEqual(atExport, BackupTestDb.Snapshot(_h.LivePath));
            Assert.Equal(original, BackupTestDb.Sha256Of(_h.BakPath));
        }
        finally
        {
            lockHandle?.Dispose();
        }
    }
}
