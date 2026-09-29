using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Infrastructure.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// F-019 S4b failure-injection suite (threat table T4..T11, DD-6 R4..R8) against the REAL
/// <see cref="SqliteBackupService"/> on real temp SQLite files. After every injected fault the live path must
/// hold a valid database that is either the original bytes or the fully verified restored database.
/// </summary>
public sealed class SqliteBackupServiceSwapFailureTests : IDisposable
{
    private readonly RestoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    /// <summary>SHA-256 of a file another connection still has open for writing.</summary>
    private static string ShaWhileOpen(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
    }

    [Fact]
    public void SwapRetryDelays_DefaultToTheDocumentedSchedule()
    {
        Assert.Equal(
            new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(1800) },
            new BackupOptions("1.0.0").SwapRetryDelays);
    }

    // ---- R6: retry with backoff ---------------------------------------------------------------

    [Fact]
    public async Task Swap_TwoTransientLockFailures_ThenSucceeds_WithBackoffAndPoolsReleasedBeforeEveryAttempt()
    {
        byte[] zip = await _h.CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_h.LivePath);
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        var exclusiveAtAttempt = new List<bool>();
        var poolHoldsTheHandle = new List<bool>();
        int attempts = 0;
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace").Before(_ =>
        {
            attempts++;
            exclusiveAtAttempt.Add(RestoreHarness.CanOpenExclusively(_h.LivePath));
            if (attempts <= 2)
            {
                // A component that touched the database during the backoff leaves a pooled handle behind.
                poolHoldsTheHandle.Add(RestoreHarness.RepopulatePool(_h.LivePath));
                throw new IOException("file in use");
            }
        });

        Result result = await _h.RestoreAsync(zip, fs);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(3, fs.CountCalls("Replace"));
        Assert.Equal(new[] { true, true, true }, exclusiveAtAttempt);
        Assert.Equal(new[] { true, true }, poolHoldsTheHandle);
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(600) }, _h.Delays);
        Assert.True(File.Exists(_h.BakPath));
        Assert.Equal(original, BackupTestDb.Sha256Of(_h.BakPath));
        RestoreHarness.AssertSnapshotsEqual(atExport, BackupTestDb.Snapshot(_h.LivePath));
        RestoreHarness.AssertOpensAndIsHealthy(_h.LivePath);
        _h.AssertWorkDirectoryEmpty();
    }

    [Fact]
    public async Task Swap_EveryAttemptFails_IsIoErrorAfterTheWholeScheduleWithTheLiveDatabaseByteIdentical()
    {
        byte[] zip = await _h.CreateZipAsync();
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        int n = 0;
        fs.On("Replace").Before(_ =>
        {
            Exception failure = n++ % 2 == 0 ? new IOException("sharing violation") : new UnauthorizedAccessException("access denied");
            throw failure;
        });

        Result result = await _h.RestoreAsync(zip, fs);

        _h.AssertFailedClosed(result, original, expectSafetyZip: true);
        Assert.Equal(4, fs.CountCalls("Replace"));
        Assert.Equal(
            new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(1800) },
            _h.Delays);
        Assert.DoesNotContain(fs.Calls, c => c.StartsWith("Delete ", StringComparison.Ordinal) && c.EndsWith(_h.LivePath, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Swap_CustomSchedule_IsHonoured()
    {
        byte[] zip = await _h.CreateZipAsync();
        string original = _h.Db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace").Throws(new IOException("locked"));
        BackupOptions options = _h.Options() with { SwapRetryDelays = [TimeSpan.FromMilliseconds(5)] };

        Result result = await _h.RestoreAsync(zip, fs, options);

        _h.AssertFailedClosed(result, original, expectSafetyZip: true);
        Assert.Equal(2, fs.CountCalls("Replace"));
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(5) }, _h.Delays);
    }

    [Fact]
    public async Task Swap_CancellationIsIgnoredFromTheSwapOnward_AndBackoffIsNotCancellable()
    {
        byte[] zip = await _h.CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_h.LivePath);
        _h.MutateLive();
        using var cts = new CancellationTokenSource();
        var fs = new FaultyBackupFileSystem();
        int attempts = 0;
        fs.On("Replace").Before(_ =>
        {
            cts.Cancel();
            if (++attempts == 1)
            {
                throw new IOException("locked once");
            }
        });

        Result result = await _h.RestoreAsync(zip, fs, ct: cts.Token);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(cts.IsCancellationRequested);
        Assert.Equal(2, fs.CountCalls("Replace"));
        Assert.All(_h.DelayCalls, d => Assert.False(d.TokenCanBeCancelled));
        RestoreHarness.AssertSnapshotsEqual(atExport, BackupTestDb.Snapshot(_h.LivePath));
    }

    // ---- R6: name-invariant repair ------------------------------------------------------------

    [Fact]
    public async Task Swap_PartialWin32FailureMovesTheOriginalUnderTheBakName_IsRepairedBeforeTheNextAttempt()
    {
        byte[] zip = await _h.CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_h.LivePath);
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        var liveShaAtAttempt = new List<string?>();
        var bakExistedAtAttempt = new List<bool>();
        int attempts = 0;
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace").Before(_ =>
        {
            attempts++;
            liveShaAtAttempt.Add(File.Exists(_h.LivePath) ? _h.Db.LiveSha256() : null);
            bakExistedAtAttempt.Add(File.Exists(_h.BakPath));
            if (attempts == 1)
            {
                File.Move(_h.LivePath, _h.BakPath);
                throw new IOException("simulated ERROR_UNABLE_TO_MOVE_REPLACEMENT_2 (1177)");
            }
        });

        Result result = await _h.RestoreAsync(zip, fs);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new string?[] { original, original }, liveShaAtAttempt);
        Assert.Equal(new[] { false, false }, bakExistedAtAttempt);
        Assert.Equal(original, BackupTestDb.Sha256Of(_h.BakPath));
        RestoreHarness.AssertSnapshotsEqual(atExport, BackupTestDb.Snapshot(_h.LivePath));
    }

    [Fact]
    public async Task Swap_EveryAttemptIsAPartialFailure_EndsWithTheOriginalBackAtTheLivePath()
    {
        byte[] zip = await _h.CreateZipAsync();
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace").Before(_ =>
        {
            File.Move(_h.LivePath, _h.BakPath);
            throw new IOException("simulated 1177");
        });

        Result result = await _h.RestoreAsync(zip, fs);

        _h.AssertFailedClosed(result, original, expectSafetyZip: true);
        Assert.Equal(4, fs.CountCalls("Replace"));
    }

    [Fact]
    public async Task Swap_TheRepairItselfFails_LogsCriticalAndKeepsTheOriginalUnderTheBakName()
    {
        byte[] zip = await _h.CreateZipAsync();
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace").Before(_ =>
        {
            File.Move(_h.LivePath, _h.BakPath);
            throw new IOException("simulated 1177");
        });
        fs.On("Move", a => a[0] == _h.BakPath && a[1] == _h.LivePath).Throws(new IOException("cannot move back"));

        Result result = await _h.RestoreAsync(zip, fs);

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.IoError, result.Error!.Code);
        Assert.Equal(original, BackupTestDb.Sha256Of(_h.BakPath));
        string safety = Assert.Single(_h.SafetyZips());
        Assert.Contains(
            _h.Logger.Entries,
            e => e.Level == LogLevel.Critical && e.Message.Contains(_h.BakPath, StringComparison.Ordinal) && e.Message.Contains(safety, StringComparison.Ordinal));
        _h.AssertWorkDirectoryEmpty();
    }

    [Fact]
    public async Task Swap_ReplaceReportsFailureAfterItAlreadyInstalledTheFile_IsVerifiedInsteadOfRetried()
    {
        byte[] zip = await _h.CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_h.LivePath);
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace").After(_ => throw new IOException("failed after completing"));

        Result result = await _h.RestoreAsync(zip, fs);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, fs.CountCalls("Replace"));
        Assert.Empty(_h.Delays);
        Assert.Equal(original, BackupTestDb.Sha256Of(_h.BakPath));
        RestoreHarness.AssertSnapshotsEqual(atExport, BackupTestDb.Snapshot(_h.LivePath));
    }

    // ---- R4: hot journal, sidecars --------------------------------------------------------------

    [Fact]
    public async Task HotJournal_FromAnInterruptedTransaction_IsRolledBackAndNeverReplayedIntoTheRestoredDatabase()
    {
        byte[] zip = await _h.CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_h.LivePath);
        Dictionary<string, List<string>> committed = _h.PlantHotJournal();

        Result result = await _h.RestoreAsync(zip);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(File.Exists(_h.LivePath + "-journal"));
        RestoreHarness.AssertSnapshotsEqual(atExport, BackupTestDb.Snapshot(_h.LivePath));
        RestoreHarness.AssertSnapshotsEqual(committed, BackupTestDb.Snapshot(_h.BakPath));
    }

    [Fact]
    public async Task HotJournal_ThatPersistsThroughTheWholeSchedule_NeverSwaps()
    {
        byte[] zip = await _h.CreateZipAsync();
        string journal = _h.LivePath + "-journal";
        var fs = new FaultyBackupFileSystem();
        fs.On("FileExists", a => a[0] == journal).Before(_ =>
        {
            // Only once the safety backup exists, i.e. at the swap gate (the earlier gate has its own test).
            if (_h.SafetyZips().Length > 0)
            {
                File.WriteAllBytes(journal, new byte[512]);
            }
        });
        string original = _h.Db.LiveSha256();

        Result result = await _h.RestoreAsync(zip, fs);

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.IoError, result.Error!.Code);
        Assert.Equal(0, fs.CountCalls("Replace"));
        Assert.Equal(3, _h.Delays.Count);
        Assert.True(File.Exists(journal), "The journal belongs to the live database and must be left alone.");
        Assert.Equal(new byte[512], await File.ReadAllBytesAsync(journal));
        Assert.Equal(original, _h.Db.LiveSha256());
        RestoreHarness.AssertOpensAndIsHealthy(_h.LivePath);
        Assert.NotEmpty(_h.SafetyZips());
        Assert.False(File.Exists(_h.BakPath));
        _h.AssertWorkDirectoryEmpty();
    }

    [Fact]
    public async Task HotJournal_ThatClearsDuringTheBackoff_LetsALaterAttemptSwap()
    {
        byte[] zip = await _h.CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_h.LivePath);
        string journal = _h.LivePath + "-journal";
        var fs = new FaultyBackupFileSystem();
        fs.On("FileExists", a => a[0] == journal).Before(_ =>
        {
            if (_h.SafetyZips().Length == 0)
            {
                return;
            }

            if (_h.Delays.Count < 2)
            {
                File.WriteAllBytes(journal, new byte[512]);
            }
            else
            {
                File.Delete(journal);
            }
        });

        Result result = await _h.RestoreAsync(zip, fs);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, _h.Delays.Count);
        Assert.Equal(1, fs.CountCalls("Replace"));
        RestoreHarness.AssertSnapshotsEqual(atExport, BackupTestDb.Snapshot(_h.LivePath));
    }

    [Fact]
    public async Task Sidecar_HeldByAnotherProcessDuringTheFirstAttempt_IsRetriedAfterTheBackoff()
    {
        byte[] zip = await _h.CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_h.LivePath);
        FileStream? held = null;
        var fs = new FaultyBackupFileSystem();
        fs.On("Move", a => a[1].EndsWith(".zip", StringComparison.Ordinal))
            .After(_ => held = new FileStream(_h.LivePath + "-wal", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        _h.OnDelay = () =>
        {
            held?.Dispose();
            held = null;
        };

        try
        {
            Result result = await _h.RestoreAsync(zip, fs);

            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.NotEmpty(_h.Delays);
            RestoreHarness.AssertSnapshotsEqual(atExport, BackupTestDb.Snapshot(_h.LivePath));
        }
        finally
        {
            held?.Dispose();
        }
    }

    [Fact]
    public async Task Checkpoint_BlockedByAnOpenReader_FailsClosedThenSucceedsAfterRelease()
    {
        byte[] zip = await _h.CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_h.LivePath);
        SqliteConnection reader = BackupTestDb.Open(_h.LivePath);
        Dictionary<string, List<string>> pending;
        string blockedSha;
        try
        {
            Assert.Equal("wal", BackupTestDb.Scalar(reader, "PRAGMA journal_mode;"), ignoreCase: true);
            BackupTestDb.Exec(reader, "BEGIN;");
            BackupTestDb.Scalar(reader, "SELECT count(*) FROM Settings;");
            using (SqliteConnection writer = BackupTestDb.Open(_h.LivePath))
            {
                BackupTestDb.Exec(writer, "PRAGMA wal_autocheckpoint = 0;");
                BackupTestDb.Exec(writer, $"INSERT INTO Settings (Id, Key, Value) VALUES ('{Guid.NewGuid()}', 'pending.frame', 'in-the-wal');");
            }

            Assert.True(new FileInfo(_h.LivePath + "-wal").Length > 0, "Precondition: uncheckpointed frames must exist.");
            blockedSha = ShaWhileOpen(_h.LivePath);
            pending = BackupTestDb.Snapshot(_h.LivePath);

            Result result = await _h.RestoreAsync(zip);

            Assert.False(result.IsSuccess);
            Assert.Equal(BackupErrorCodes.IoError, result.Error!.Code);
            Assert.Contains("pending changes", result.Error.Message, StringComparison.Ordinal);
            Assert.Equal(blockedSha, ShaWhileOpen(_h.LivePath));
            RestoreHarness.AssertSnapshotsEqual(pending, BackupTestDb.Snapshot(_h.LivePath));
            Assert.Equal(3, _h.Delays.Count);
            Assert.False(File.Exists(_h.BakPath));
            _h.AssertWorkDirectoryEmpty();
        }
        finally
        {
            reader.Dispose();
        }

        Result second = await _h.RestoreAsync(zip);

        Assert.True(second.IsSuccess, second.Error?.Message);
        RestoreHarness.AssertSnapshotsEqual(atExport, BackupTestDb.Snapshot(_h.LivePath));
        RestoreHarness.AssertSnapshotsEqual(pending, BackupTestDb.Snapshot(_h.BakPath));
    }

    // ---- R7: post-swap verification and rollback ----------------------------------------------

    [Fact]
    public async Task Verify_HashMismatchAfterTheSwap_RollsBackToTheOriginalBytes()
    {
        byte[] zip = await _h.CreateZipAsync();
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace", a => !a[2].Contains(".rollback-", StringComparison.Ordinal))
            .After(_ => File.WriteAllBytes(_h.LivePath, new byte[8192]));

        Result result = await _h.RestoreAsync(zip, fs);

        _h.AssertFailedClosed(result, original, expectSafetyZip: true);
        Assert.Equal(2, fs.CountCalls("Replace"));
        string rollback = fs.Calls.Last(c => c.StartsWith("Replace ", StringComparison.Ordinal));
        Assert.StartsWith("Replace " + _h.BakPath + " -> " + _h.LivePath + " -> " + _h.LivePath + ".rollback-", rollback, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_h.Db.DataDirectory, "data.db.rollback-*"));
        Assert.DoesNotContain(_h.Logger.Entries, e => e.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task Verify_QuickCheckFailsAfterTheSwap_RollsBackToTheOriginalBytes()
    {
        byte[] zip = await _h.CreateZipAsync();
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        var fs = new FaultyBackupFileSystem();

        // The second read of the staged file is the one that computes the expected hash, after the last
        // integrity check: damage made right before it is hashed into the expectation, so only the quick
        // check of the installed file can notice it.
        fs.On("OpenRead", a => a[0].EndsWith("restore.db", StringComparison.Ordinal), occurrence: 2).Before(a =>
        {
            using var stream = new FileStream(a[0], FileMode.Open, FileAccess.Write, FileShare.None);
            Assert.True(stream.Length > 12 * 4096, "Precondition: the staged database must be large enough to damage.");
            stream.Position = 4096;
            stream.Write(Enumerable.Repeat((byte)0xFF, 8 * 4096).ToArray());
        });

        Result result = await _h.RestoreAsync(zip, fs);

        _h.AssertFailedClosed(result, original, expectSafetyZip: true);
        Assert.Contains(_h.Logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("quick check", StringComparison.Ordinal));
        Assert.Equal(2, fs.CountCalls("Replace"));
    }

    [Fact]
    public async Task Verify_TheInstalledFileCannotBeOpened_RollsBackToTheOriginalBytes()
    {
        byte[] zip = await _h.CreateZipAsync();
        string original = _h.Db.LiveSha256();
        var fs = new FaultyBackupFileSystem();

        // The staged file passes every check, then a stale sidecar that cannot be removed appears.
        FileStream? held = null;
        fs.On("Replace", a => !a[2].Contains(".rollback-", StringComparison.Ordinal))
            .After(_ => held = new FileStream(_h.LivePath + "-shm", FileMode.Create, FileAccess.ReadWrite, FileShare.None));

        Result result;
        try
        {
            result = await _h.RestoreAsync(zip, fs);
            Assert.Equal(original, _h.Db.LiveSha256());
        }
        finally
        {
            held?.Dispose();
        }

        _h.AssertFailedClosed(result, original, expectSafetyZip: true);
    }

    [Fact]
    public async Task Rollback_ReplaceKeepsFailing_LogsCriticalNamingTheBakAndTheSafetyZip_AndKeepsBoth()
    {
        byte[] zip = await _h.CreateZipAsync();
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace", a => !a[2].Contains(".rollback-", StringComparison.Ordinal))
            .After(_ => File.WriteAllBytes(_h.LivePath, new byte[8192]));
        fs.On("Replace", a => a[2].Contains(".rollback-", StringComparison.Ordinal)).Throws(new IOException("still locked"));

        Result result = await _h.RestoreAsync(zip, fs);

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.IoError, result.Error!.Code);
        Assert.Equal(original, BackupTestDb.Sha256Of(_h.BakPath));
        string safety = Assert.Single(_h.SafetyZips());
        Assert.Contains(
            _h.Logger.Entries,
            e => e.Level == LogLevel.Critical && e.Message.Contains(_h.BakPath, StringComparison.Ordinal) && e.Message.Contains(safety, StringComparison.Ordinal));
        Assert.Equal(4, fs.Calls.Count(c => c.StartsWith("Replace ", StringComparison.Ordinal) && c.Contains(".rollback-", StringComparison.Ordinal)));
        _h.AssertWorkDirectoryEmpty();
    }

    [Fact]
    public async Task Rollback_PartialWin32FailureOfTheRollbackItself_IsRepairedAndTheOriginalIsBack()
    {
        byte[] zip = await _h.CreateZipAsync();
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace", a => !a[2].Contains(".rollback-", StringComparison.Ordinal))
            .After(_ => File.WriteAllBytes(_h.LivePath, new byte[8192]));
        int rollbackAttempts = 0;
        fs.On("Replace", a => a[2].Contains(".rollback-", StringComparison.Ordinal)).Before(a =>
        {
            if (++rollbackAttempts == 1)
            {
                File.Move(_h.LivePath, a[2]);
                throw new IOException("simulated 1177 during the rollback");
            }
        });

        Result result = await _h.RestoreAsync(zip, fs);

        _h.AssertFailedClosed(result, original, expectSafetyZip: true);
        Assert.Empty(Directory.GetFiles(_h.Db.DataDirectory, "data.db.rollback-*"));
    }

    [Fact]
    public async Task Retention_StillRunsAfterARollback()
    {
        byte[] zip = await _h.CreateZipAsync();
        Directory.CreateDirectory(_h.BackupsDirectory);
        string[] old = ["pre-restore-20200101-000001.zip", "pre-restore-20200101-000002.zip", "pre-restore-20200101-000003.zip"];
        foreach (string name in old)
        {
            await File.WriteAllTextAsync(Path.Combine(_h.BackupsDirectory, name), "old " + name);
        }

        var fs = new FaultyBackupFileSystem();
        fs.On("Replace", a => !a[2].Contains(".rollback-", StringComparison.Ordinal))
            .After(_ => File.WriteAllBytes(_h.LivePath, new byte[8192]));

        Result result = await _h.RestoreAsync(zip, fs);

        Assert.False(result.IsSuccess);
        string[] remaining = _h.SafetyZips().Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(3, remaining.Length);
        Assert.DoesNotContain(old[0], remaining);
        Assert.Contains(old[1], remaining);
    }

    // ---- Every stage: the live path is never left without a valid database ----------------------

    [Theory]
    [InlineData("incoming-open", false)]
    [InlineData("free-space", false)]
    [InlineData("extract", false)]
    [InlineData("hash", false)]
    [InlineData("safety-create", false)]
    [InlineData("safety-reread", false)]
    [InlineData("safety-publish", false)]
    [InlineData("swap", true)]
    [InlineData("swap-unauthorized", true)]
    [InlineData("post-verify", true)]
    public async Task EveryInjectedStageFailure_LeavesTheLiveDatabaseByteIdenticalAndOpenable(string stage, bool safetyZipExpected)
    {
        byte[] zip = await _h.CreateZipAsync();
        _h.MutateLive();
        string original = _h.Db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        switch (stage)
        {
            case "incoming-open":
                fs.On("OpenRead", a => a[0].EndsWith("incoming.zip", StringComparison.Ordinal)).Throws(new IOException("read error"));
                break;
            case "free-space":
                fs.FreeSpace = 1;
                break;
            case "extract":
                fs.On("Create", a => a[0].EndsWith("restore.db", StringComparison.Ordinal)).Throws(new IOException("disk full"));
                break;
            case "hash":
                fs.On("OpenRead", a => a[0].EndsWith("restore.db", StringComparison.Ordinal), occurrence: 2).Throws(new IOException("read error"));
                break;
            case "safety-create":
                fs.On("Create", a => a[0].EndsWith(".zip.tmp", StringComparison.Ordinal)).Throws(new IOException("disk full"));
                break;
            case "safety-reread":
                fs.On("OpenRead", a => a[0].EndsWith(".zip.tmp", StringComparison.Ordinal)).Throws(new IOException("read error"));
                break;
            case "safety-publish":
                fs.On("Move", a => a[1].EndsWith(".zip", StringComparison.Ordinal)).Throws(new IOException("cannot rename"));
                break;
            case "swap":
                fs.On("Replace").Throws(new IOException("locked"));
                break;
            case "swap-unauthorized":
                fs.On("Replace").Throws(new UnauthorizedAccessException("denied"));
                break;
            case "post-verify":
                fs.On("Replace", a => !a[2].Contains(".rollback-", StringComparison.Ordinal))
                    .After(_ => File.WriteAllBytes(_h.LivePath, [1, 2, 3]));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown stage.");
        }

        Result result = await _h.RestoreAsync(zip, fs);

        _h.AssertFailedClosed(result, original, safetyZipExpected);
    }
}
