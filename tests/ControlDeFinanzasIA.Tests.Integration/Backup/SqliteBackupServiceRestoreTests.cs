using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Infrastructure.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// F-019 restore pipeline (S4a) against the REAL <see cref="SqliteBackupService"/> on real temp SQLite
/// files: validation reuse, free space, key carry-over, safety backup, retention, WAL and journal
/// handling, and the first real swap. Every failure path proves the live database is byte-identical.
/// </summary>
public sealed class SqliteBackupServiceRestoreTests : IDisposable
{
    private const string _futureId = "29990101000000_Future";

    private readonly BackupTestDb _db = new();
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), $"restore-scratch-{Guid.NewGuid():N}");
    private readonly TestClock _clock = new();

    public SqliteBackupServiceRestoreTests()
    {
        Directory.CreateDirectory(_scratch);
    }

    private string BackupsDirectory => Path.Combine(_db.DataDirectory, "backups");

    private string BakPath => _db.DatabasePath + ".pre-restore.bak";

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    // ---- Roundtrip and validation reuse -------------------------------------------------------

    [Fact]
    public async Task Restore_Roundtrip_RestoresEveryTableAndKeepsTheLocalKey()
    {
        byte[] zip = await CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_db.DatabasePath);
        MutateLive();
        Assert.NotEqual(atExport["Settings"], BackupTestDb.Snapshot(_db.DatabasePath)["Settings"]);

        RestoreOutcome outcome = await RestoreOkAsync(zip);

        AssertLiveEquals(atExport);
        Assert.Contains(BackupTestDb.Snapshot(_db.DatabasePath)["Settings"], r => r.Contains(BackupTestDb.ApiKey, StringComparison.Ordinal));
        Assert.True(outcome.ApiKeyCarriedOver);
        Assert.False(outcome.ApiKeyLost);
        Assert.False(outcome.IsOlderSchema);
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Restore_ZipAlteredAfterInspect_IsRejectedBecauseItIsRevalidated()
    {
        byte[] good = await CreateZipAsync();
        SqliteBackupService service = NewService();
        Result inspected = await service.InspectAsync(_ => Task.FromResult<Stream>(new MemoryStream(good)));
        Assert.True(inspected.IsSuccess, inspected.Error?.Message);
        byte[] database = MakeBackupDatabase();
        byte[] manifestSource = (byte[])database.Clone();
        database[database.Length / 2] ^= 0xFF;
        byte[] tampered = BackupZipBuilder.Build(
            ("manifest.json", BackupZipBuilder.Json(BackupZipBuilder.ManifestFor(manifestSource))),
            ("data.db", database));
        string before = _db.LiveSha256();

        Result result = await service.RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(tampered)));

        AssertRejected(result, BackupErrorCodes.Corrupt, before);
    }

    [Theory]
    [InlineData("garbage", BackupErrorCodes.NotABackup)]
    [InlineData("empty", BackupErrorCodes.NotABackup)]
    [InlineData("missingManifest", BackupErrorCodes.NotABackup)]
    [InlineData("extraEntry", BackupErrorCodes.NotABackup)]
    [InlineData("zipSlip", BackupErrorCodes.NotABackup)]
    [InlineData("oversized", BackupErrorCodes.NotABackup)]
    [InlineData("hashFlip", BackupErrorCodes.Corrupt)]
    [InlineData("notSqlite", BackupErrorCodes.Corrupt)]
    [InlineData("foreignKey", BackupErrorCodes.Corrupt)]
    [InlineData("noHistory", BackupErrorCodes.NotABackup)]
    [InlineData("newerSchema", BackupErrorCodes.NewerSchema)]
    [InlineData("formatVersionTwo", BackupErrorCodes.NewerSchema)]
    public async Task Restore_RejectedBackups_ReturnTheSameCodeAndLeaveTheLiveDatabaseIdentical(string scenario, string expectedCode)
    {
        (byte[] zip, BackupOptions? options) = BuildRejectedCase(scenario);
        string before = _db.LiveSha256();

        Result result = await NewService(options: options).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

        AssertRejected(result, expectedCode, before);
    }

    // ---- Key carry-over -----------------------------------------------------------------------

    [Fact]
    public async Task Restore_BackupWithoutKey_CarriesTheLocalKeyIntoTheFileBeforeTheSwap_AndTheLiveFileChangesOnlyAtTheSwap()
    {
        byte[] zip = await CreateZipAsync();
        string before = _db.LiveSha256();
        string? liveShaAtSwap = null;
        string? keyInStagedFile = null;
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace").Before(args =>
        {
            liveShaAtSwap = _db.LiveSha256();
            using SqliteConnection staged = BackupTestDb.Open(args[0], SqliteOpenMode.ReadOnly);
            keyInStagedFile = BackupTestDb.Scalar(staged, "SELECT Value FROM Settings WHERE Key = 'ai.apiKey';");
        });

        RestoreOutcome outcome = await RestoreOkAsync(zip, fs);

        Assert.Equal(before, liveShaAtSwap);
        Assert.Equal(BackupTestDb.ApiKey, keyInStagedFile);
        Assert.True(outcome.ApiKeyCarriedOver);
    }

    [Fact]
    public async Task Restore_BackupWithAnEmptyKey_IsTreatedAsMissing()
    {
        byte[] database = MakeBackupDatabase(c => BackupTestDb.Exec(c, "UPDATE Settings SET Value = '' WHERE Key = 'ai.apiKey';"));

        RestoreOutcome outcome = await RestoreOkAsync(BackupZipBuilder.Valid(database));

        using SqliteConnection live = BackupTestDb.Open(_db.DatabasePath, SqliteOpenMode.ReadOnly);
        Assert.Equal(BackupTestDb.ApiKey, BackupTestDb.Scalar(live, "SELECT Value FROM Settings WHERE Key = 'ai.apiKey';"));
        Assert.Equal("1", BackupTestDb.Scalar(live, "SELECT count(*) FROM Settings WHERE Key = 'ai.apiKey';"));
        Assert.True(outcome.ApiKeyCarriedOver);
    }

    [Fact]
    public async Task Restore_BackupWithItsOwnKey_KeepsIt()
    {
        byte[] database = MakeBackupDatabase(c => BackupTestDb.Exec(c, "UPDATE Settings SET Value = 'sk-from-backup' WHERE Key = 'ai.apiKey';"));

        RestoreOutcome outcome = await RestoreOkAsync(BackupZipBuilder.Valid(database));

        using SqliteConnection live = BackupTestDb.Open(_db.DatabasePath, SqliteOpenMode.ReadOnly);
        Assert.Equal("sk-from-backup", BackupTestDb.Scalar(live, "SELECT Value FROM Settings WHERE Key = 'ai.apiKey';"));
        Assert.False(outcome.ApiKeyCarriedOver);
        Assert.False(outcome.ApiKeyLost);
    }

    [Fact]
    public async Task Restore_BackupWithoutKeyRowAndLiveWithoutKey_CarriesNothing()
    {
        using (SqliteConnection live = BackupTestDb.Open(_db.DatabasePath))
        {
            BackupTestDb.Exec(live, "DELETE FROM Settings WHERE Key = 'ai.apiKey';");
        }

        byte[] zip = await CreateZipAsync();

        RestoreOutcome outcome = await RestoreOkAsync(zip);

        using SqliteConnection restored = BackupTestDb.Open(_db.DatabasePath, SqliteOpenMode.ReadOnly);
        Assert.Equal("0", BackupTestDb.Scalar(restored, "SELECT count(*) FROM Settings WHERE Key = 'ai.apiKey';"));
        Assert.False(outcome.ApiKeyCarriedOver);
        Assert.False(outcome.ApiKeyLost);
    }

    [Fact]
    public async Task Restore_PreSettingsBackup_RestoresAndReportsTheKeyAsLost()
    {
        byte[] database = MakeBackupDatabase(DropSettingsMigration);

        RestoreOutcome outcome = await RestoreOkAsync(BackupZipBuilder.Valid(database));

        using SqliteConnection restored = BackupTestDb.Open(_db.DatabasePath, SqliteOpenMode.ReadOnly);
        Assert.Equal("0", BackupTestDb.Scalar(restored, "SELECT count(*) FROM sqlite_master WHERE name = 'Settings';"));
        Assert.True(outcome.ApiKeyLost);
        Assert.False(outcome.ApiKeyCarriedOver);
        Assert.True(outcome.IsOlderSchema);
    }

    [Fact]
    public async Task Restore_OlderBackup_IsFlaggedInTheOutcome()
    {
        byte[] database = MakeBackupDatabase(c => BackupTestDb.Exec(c, "DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE '%_AddNotifications';"));

        RestoreOutcome outcome = await RestoreOkAsync(BackupZipBuilder.Valid(database));

        Assert.True(outcome.IsOlderSchema);
        Assert.False(outcome.ApiKeyLost);
    }

    // ---- Safety backup ------------------------------------------------------------------------

    [Fact]
    public async Task Restore_WritesAValidSafetyBackupThatKeepsTheKeyAndEqualsThePreRestoreDatabase()
    {
        byte[] zip = await CreateZipAsync();
        MutateLive();
        Dictionary<string, List<string>> preRestore = BackupTestDb.Snapshot(_db.DatabasePath);

        RestoreOutcome outcome = await RestoreOkAsync(zip);

        string expectedStamp = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, TimeZoneInfo.Local).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        Assert.Equal($"pre-restore-{expectedStamp}.zip", outcome.SafetyBackupFileName);
        string safety = Path.Combine(BackupsDirectory, outcome.SafetyBackupFileName);
        Assert.True(File.Exists(safety));
        byte[] bytes = await File.ReadAllBytesAsync(safety);

        Result inspected = await NewService().InspectAsync(_ => Task.FromResult<Stream>(new MemoryStream(bytes)));
        Assert.True(inspected.IsSuccess, inspected.Error?.Message);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var manifest = JsonDocument.Parse(ReadEntry(archive, "manifest.json"));
        Assert.True(manifest.RootElement.GetProperty("apiKeyIncluded").GetBoolean());
        string extracted = Path.Combine(_scratch, "safety.db");
        await File.WriteAllBytesAsync(extracted, ReadEntry(archive, "data.db"));
        Dictionary<string, List<string>> safetyRows = BackupTestDb.Snapshot(extracted);
        Assert.Equal(preRestore.Keys.OrderBy(k => k, StringComparer.Ordinal), safetyRows.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (string table in preRestore.Keys)
        {
            Assert.Equal(preRestore[table], safetyRows[table]);
        }

        Assert.Contains(safetyRows["Settings"], r => r.Contains(BackupTestDb.ApiKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Restore_SafetyBackupCreateFails_IsIoErrorAndNeverSwaps()
    {
        byte[] zip = await CreateZipAsync();
        string before = _db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("Create", a => a[0].EndsWith(".zip.tmp", StringComparison.Ordinal)).Throws(new IOException("disk full"));

        Result result = await NewService(fs).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

        AssertRejected(result, BackupErrorCodes.IoError, before);
        Assert.Equal(0, fs.CountCalls("Replace"));
    }

    [Fact]
    public async Task Restore_SafetyBackupCannotBePublished_IsIoErrorAndTheTempFileIsRemoved()
    {
        byte[] zip = await CreateZipAsync();
        string before = _db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("Move", a => a[1].EndsWith(".zip", StringComparison.Ordinal)).Throws(new IOException("cannot rename"));

        Result result = await NewService(fs).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

        AssertRejected(result, BackupErrorCodes.IoError, before);
        Assert.Equal(0, fs.CountCalls("Replace"));
    }

    [Fact]
    public async Task Restore_SafetyBackupCorruptedOnDisk_IsCaughtByTheReRead()
    {
        byte[] zip = await CreateZipAsync();
        string before = _db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("OpenRead", a => a[0].EndsWith(".zip.tmp", StringComparison.Ordinal)).Before(a =>
        {
            byte[] bytes = File.ReadAllBytes(a[0]);
            bytes[bytes.Length / 2] ^= 0xFF;
            File.WriteAllBytes(a[0], bytes);
        });

        Result result = await NewService(fs).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

        AssertRejected(result, BackupErrorCodes.IoError, before);
        Assert.Equal(0, fs.CountCalls("Replace"));
    }

    // ---- Retention ----------------------------------------------------------------------------

    [Fact]
    public async Task Restore_Retention_KeepsTheThreeNewestSafetyZipsAndTouchesNothingElse()
    {
        byte[] zip = await CreateZipAsync();
        Directory.CreateDirectory(BackupsDirectory);
        string[] old = ["pre-restore-20200101-000001.zip", "pre-restore-20200101-000002.zip", "pre-restore-20200101-000003.zip"];
        string[] others = ["notes.txt", "my-backup.zip", "pre-restore-notes.zip", "pre-restore-20200101-000009.zip.tmp"];
        foreach (string name in old.Concat(others))
        {
            await File.WriteAllTextAsync(Path.Combine(BackupsDirectory, name), "content of " + name);
        }

        RestoreOutcome outcome = await RestoreOkAsync(zip);

        string[] zips = Directory.GetFiles(BackupsDirectory, "pre-restore-2*.zip").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(new[] { old[1], old[2], outcome.SafetyBackupFileName }, zips);
        foreach (string name in others)
        {
            Assert.Equal("content of " + name, await File.ReadAllTextAsync(Path.Combine(BackupsDirectory, name)));
        }
    }

    [Fact]
    public async Task Restore_FiveConsecutiveRestoresInTheSameSecond_KeepTheThreeNewestAndUseCollisionSuffixes()
    {
        byte[] zip = await CreateZipAsync();
        var names = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            names.Add((await RestoreOkAsync(zip)).SafetyBackupFileName);
        }

        string stamp = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, TimeZoneInfo.Local).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        Assert.Equal(
            new[] { $"pre-restore-{stamp}.zip", $"pre-restore-{stamp}-2.zip", $"pre-restore-{stamp}-3.zip", $"pre-restore-{stamp}-4.zip", $"pre-restore-{stamp}-5.zip" },
            names);
        string[] remaining = Directory.GetFiles(BackupsDirectory, "pre-restore-*.zip").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(new[] { $"pre-restore-{stamp}-3.zip", $"pre-restore-{stamp}-4.zip", $"pre-restore-{stamp}-5.zip" }, remaining);
    }

    [Fact]
    public async Task Restore_FiveConsecutiveRestoresAtDifferentSeconds_KeepTheThreeNewest()
    {
        byte[] zip = await CreateZipAsync();
        var names = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            _clock.UtcNow = _clock.UtcNow.AddSeconds(7);
            names.Add((await RestoreOkAsync(zip)).SafetyBackupFileName);
        }

        string[] remaining = Directory.GetFiles(BackupsDirectory, "pre-restore-*.zip").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(names.Skip(2).OrderBy(n => n, StringComparer.Ordinal), remaining);
    }

    // ---- Free space ---------------------------------------------------------------------------

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    public async Task Restore_FreeSpaceBoundary_IsThreeTimesTheLargerOfLiveAndIncoming(long offset, bool succeeds)
    {
        byte[] zip = await CreateZipAsync();
        long incoming;
        using (var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
        {
            incoming = archive.GetEntry("data.db")!.Length;
        }

        long required = 3 * Math.Max(new FileInfo(_db.DatabasePath).Length, incoming);
        string before = _db.LiveSha256();
        var fs = new FaultyBackupFileSystem { FreeSpace = required + offset };

        Result result = await NewService(fs).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

        if (succeeds)
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
        else
        {
            AssertRejected(result, BackupErrorCodes.IoError, before);
            Assert.Equal(BackupErrorCodes.ReasonInsufficientSpace, result.Error!.Details![BackupErrorCodes.DetailReason].Single());
            Assert.DoesNotContain(fs.Calls, c => c.StartsWith("Create ", StringComparison.Ordinal) && c.EndsWith("restore.db", StringComparison.Ordinal));
            Assert.Equal(0, fs.CountCalls("Replace"));
        }
    }

    // ---- Cancellation -------------------------------------------------------------------------

    [Fact]
    public async Task Restore_CancelledBeforeStart_ThrowsAndLeavesTheLiveDatabaseIdentical()
    {
        byte[] zip = await CreateZipAsync();
        string before = _db.LiveSha256();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NewService().RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)), cts.Token));

        Assert.Equal(before, _db.LiveSha256());
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Restore_CancelledWhileTheSafetyBackupIsBeingWritten_ThrowsWithNoSafetyZipAndNoSwap()
    {
        byte[] zip = await CreateZipAsync();
        string before = _db.LiveSha256();
        using var cts = new CancellationTokenSource();
        var fs = new FaultyBackupFileSystem();
        fs.On("Create", a => a[0].EndsWith(".zip.tmp", StringComparison.Ordinal)).After(_ => cts.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NewService(fs).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)), cts.Token));

        Assert.Equal(before, _db.LiveSha256());
        Assert.Equal(0, fs.CountCalls("Replace"));
        Assert.Empty(Directory.GetFiles(BackupsDirectory, "pre-restore-*"));
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Restore_CancelledAfterTheSafetyBackupIsPublished_IsIgnoredAndTheSwapCompletes()
    {
        byte[] zip = await CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_db.DatabasePath);
        MutateLive();
        using var cts = new CancellationTokenSource();
        var fs = new FaultyBackupFileSystem();
        fs.On("Move", a => a[1].EndsWith(".zip", StringComparison.Ordinal)).After(_ => cts.Cancel());

        Result result = await NewService(fs).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)), cts.Token);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(cts.IsCancellationRequested);
        AssertLiveEquals(atExport);
    }

    // ---- The swap -----------------------------------------------------------------------------

    [Fact]
    public async Task Restore_SwapAlwaysFails_IsIoErrorWithTheLiveDatabaseIdenticalAndTheSafetyZipKept()
    {
        byte[] zip = await CreateZipAsync();
        string before = _db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace").Throws(new IOException("file in use"));

        Result result = await NewService(fs).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

        AssertRejected(result, BackupErrorCodes.IoError, before, expectSafetyZip: true);
        Assert.Equal(1 + new BackupOptions("x").SwapRetryDelays.Count, fs.CountCalls("Replace"));
        Assert.False(File.Exists(BakPath));
    }

    [Fact]
    public async Task Restore_SwapFailsAfterTheOriginalWasMovedToTheBakName_RepairsTheNameInvariant()
    {
        byte[] zip = await CreateZipAsync();
        string before = _db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace").Before(_ => File.Move(_db.DatabasePath, BakPath)).Throws(new IOException("simulated ERROR_UNABLE_TO_MOVE_REPLACEMENT_2"));

        Result result = await NewService(fs).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

        AssertRejected(result, BackupErrorCodes.IoError, before, expectSafetyZip: true);
        Assert.False(File.Exists(BakPath));
    }

    [Fact]
    public async Task Restore_Success_LeavesTheOriginalBytesInThePreRestoreBak()
    {
        byte[] zip = await CreateZipAsync();
        MutateLive();
        string original = _db.LiveSha256();

        await RestoreOkAsync(zip);

        Assert.True(File.Exists(BakPath));
        Assert.Equal(original, BackupTestDb.Sha256Of(BakPath));
        Assert.NotEqual(original, _db.LiveSha256());
    }

    [Fact]
    public async Task Restore_AnomalousLeftoverBak_IsMovedAsideNotOverwritten()
    {
        byte[] zip = await CreateZipAsync();
        byte[] leftover = [1, 2, 3, 4, 5];
        await File.WriteAllBytesAsync(BakPath, leftover);
        string original = _db.LiveSha256();

        await RestoreOkAsync(zip);

        Assert.Equal(original, BackupTestDb.Sha256Of(BakPath));
        string[] aside = Directory.GetFiles(_db.DataDirectory, "data.db.pre-restore-*.bak");
        Assert.Single(aside);
        Assert.Equal(leftover, await File.ReadAllBytesAsync(aside[0]));
    }

    [Fact]
    public async Task Restore_TheLiveFileIsReplacedByExactlyOneReplaceCall()
    {
        byte[] zip = await CreateZipAsync();
        var fs = new FaultyBackupFileSystem();

        await RestoreOkAsync(zip, fs);

        Assert.Equal(1, fs.CountCalls("Replace"));
        string replace = fs.Calls.Single(c => c.StartsWith("Replace ", StringComparison.Ordinal));
        Assert.EndsWith(_db.DatabasePath + ".pre-restore.bak", replace, StringComparison.Ordinal);
        Assert.Contains(" -> " + _db.DatabasePath + " -> ", replace, StringComparison.Ordinal);
        Assert.DoesNotContain(fs.Calls, c => c.StartsWith("Delete ", StringComparison.Ordinal) && c.EndsWith(_db.DatabasePath, StringComparison.Ordinal));
    }

    // ---- Post-swap verification and rollback --------------------------------------------------

    [Fact]
    public async Task Restore_InstalledFileDiffersFromTheValidatedOne_IsRolledBackToTheOriginal()
    {
        byte[] zip = await CreateZipAsync();
        MutateLive();
        string before = _db.LiveSha256();
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace", occurrence: 1).After(_ => File.WriteAllBytes(_db.DatabasePath, new byte[8192]));

        Result result = await NewService(fs).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

        AssertRejected(result, BackupErrorCodes.IoError, before, expectSafetyZip: true);
        Assert.False(File.Exists(BakPath));
        Assert.Empty(Directory.GetFiles(_db.DataDirectory, "data.db.rollback-*"));
    }

    // ---- WAL and journal sidecars -------------------------------------------------------------

    [Fact]
    public async Task Restore_LiveInWalModeWithPendingFramesHeldByAPooledConnection_RestoresExactlyTheBackupAndPreservesTheOldFramesInTheBak()
    {
        byte[] zip = await CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_db.DatabasePath);
        using (SqliteConnection probe = BackupTestDb.Open(_db.DatabasePath, SqliteOpenMode.ReadOnly))
        {
            Assert.Equal("wal", BackupTestDb.Scalar(probe, "PRAGMA journal_mode;"), ignoreCase: true);
        }

        // SqliteConnection.ClearAllPools is process-wide and other test fixtures call it while running in
        // parallel; if one closes the pooled connection first (which checkpoints the WAL), arm it again.
        bool armed = false;
        for (int attempt = 0; attempt < 50 && !armed; attempt++)
        {
            var pooled = new SqliteConnection($"Data Source={_db.DatabasePath}");
            pooled.Open();
            BackupTestDb.Exec(pooled, "PRAGMA wal_autocheckpoint = 0;");
            BackupTestDb.Exec(pooled, $"INSERT INTO Settings (Id, Key, Value) VALUES ('{Guid.NewGuid()}', 'pending.frame.{attempt}', 'only-in-the-old-wal');");
            pooled.Close();
            pooled.Dispose();
            armed = WalHasFrames();
        }

        Assert.True(armed, "Precondition: uncheckpointed frames must exist.");
        Dictionary<string, List<string>> preRestore = BackupTestDb.Snapshot(_db.DatabasePath);
        Assert.Contains(preRestore["Settings"], r => r.Contains("pending.frame.", StringComparison.Ordinal));

        await RestoreOkAsync(zip);

        AssertLiveEquals(atExport);
        Assert.DoesNotContain(BackupTestDb.Snapshot(_db.DatabasePath)["Settings"], r => r.Contains("pending.frame.", StringComparison.Ordinal));
        AssertNoStaleSidecars();
        AssertSnapshotsEqual(preRestore, BackupTestDb.Snapshot(BakPath));
    }

    [Fact]
    public async Task Restore_LeftoverWalFromACrashedProcess_IsFoldedIntoTheBakAndNeverReplayedIntoTheRestoredFile()
    {
        byte[] zip = await CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_db.DatabasePath);
        string crashDb = Path.Combine(_scratch, "crash.db");
        string crashWal = crashDb + "-wal";
        using (SqliteConnection writer = BackupTestDb.Open(_db.DatabasePath))
        {
            BackupTestDb.Exec(writer, "PRAGMA wal_autocheckpoint = 0;");
            BackupTestDb.Exec(writer, $"INSERT INTO Settings (Id, Key, Value) VALUES ('{Guid.NewGuid()}', 'crash.frame', 'only-in-the-orphan-wal');");
            CopyShared(_db.DatabasePath, crashDb);
            CopyShared(_db.DatabasePath + "-wal", crashWal);
        }

        AssertOrphanWalIsValid(crashDb, crashWal);
        File.Copy(crashDb, _db.DatabasePath, overwrite: true);
        File.Copy(crashWal, _db.DatabasePath + "-wal", overwrite: true);
        File.Delete(_db.DatabasePath + "-shm");
        Assert.True(new FileInfo(_db.DatabasePath + "-wal").Length > 0);
        Assert.False(File.Exists(_db.DatabasePath + "-shm"));

        await RestoreOkAsync(zip);

        AssertLiveEquals(atExport);
        Assert.DoesNotContain(BackupTestDb.Snapshot(_db.DatabasePath)["Settings"], r => r.Contains("crash.frame", StringComparison.Ordinal));
        AssertNoStaleSidecars();
        Assert.Contains(BackupTestDb.Snapshot(BakPath)["Settings"], r => r.Contains("crash.frame", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Restore_HotJournalLeftByAnInterruptedTransaction_IsRolledBackBeforeTheSwap()
    {
        byte[] zip = await CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_db.DatabasePath);
        using (SqliteConnection setup = BackupTestDb.Open(_db.DatabasePath))
        {
            BackupTestDb.Exec(setup, "PRAGMA journal_mode = DELETE;");
            BackupTestDb.Exec(setup, "CREATE TABLE Filler (Id INTEGER PRIMARY KEY, Pad TEXT NOT NULL);");
            BackupTestDb.Exec(setup, "WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 3000) INSERT INTO Filler (Id, Pad) SELECT i, printf('original-%05d-', i) || hex(randomblob(200)) FROM n;");
        }

        Dictionary<string, List<string>> committed = BackupTestDb.Snapshot(_db.DatabasePath);
        string committedSha = _db.LiveSha256();
        string dbCopy = Path.Combine(_scratch, "hot.db");
        string journalCopy = Path.Combine(_scratch, "hot.db-journal");
        using (SqliteConnection writer = BackupTestDb.Open(_db.DatabasePath))
        {
            BackupTestDb.Exec(writer, "PRAGMA cache_size = 4;");
            BackupTestDb.Exec(writer, "BEGIN;");
            BackupTestDb.Exec(writer, "UPDATE Filler SET Pad = 'DIRTY-' || Pad;");
            CopyShared(_db.DatabasePath, dbCopy);
            CopyShared(_db.DatabasePath + "-journal", journalCopy);
            BackupTestDb.Exec(writer, "ROLLBACK;");
        }

        Assert.True(new FileInfo(journalCopy).Length > 512, "Precondition: the journal must hold data.");
        Assert.NotEqual(committedSha, BackupTestDb.Sha256Of(dbCopy));
        File.Copy(dbCopy, _db.DatabasePath, overwrite: true);
        File.Copy(journalCopy, _db.DatabasePath + "-journal", overwrite: true);

        await RestoreOkAsync(zip);

        Assert.False(File.Exists(_db.DatabasePath + "-journal"));
        AssertLiveEquals(atExport);
        AssertSnapshotsEqual(committed, BackupTestDb.Snapshot(BakPath));
    }

    [Fact]
    public async Task Restore_JournalThatSurvivesResolution_FailsClosedWithoutSwapping()
    {
        byte[] zip = await CreateZipAsync();
        using (SqliteConnection setup = BackupTestDb.Open(_db.DatabasePath))
        {
            BackupTestDb.Exec(setup, "PRAGMA journal_mode = DELETE;");
        }

        string before = _db.LiveSha256();
        string journal = _db.DatabasePath + "-journal";
        var fs = new FaultyBackupFileSystem();
        fs.On("FileExists", a => a[0] == journal).Before(_ => File.WriteAllBytes(journal, new byte[512]));

        Result result = await NewService(fs).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.IoError, result.Error!.Code);
        Assert.Equal(0, fs.CountCalls("Replace"));
        Assert.Equal(before, _db.LiveSha256());
        Assert.True(File.Exists(journal), "The journal belongs to the live database and must be left alone.");
        Assert.False(File.Exists(BakPath));
        if (Directory.Exists(_db.WorkDirectory))
        {
            Assert.Empty(Directory.GetFileSystemEntries(_db.WorkDirectory));
        }
    }

    [Fact]
    public async Task Restore_StaleSidecarAppearingRightAfterTheSwap_IsDeletedBeforeTheFileIsOpened()
    {
        byte[] zip = await CreateZipAsync();
        Dictionary<string, List<string>> atExport = BackupTestDb.Snapshot(_db.DatabasePath);
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace", occurrence: 1).After(_ =>
        {
            File.WriteAllBytes(_db.DatabasePath + "-wal", new byte[4096]);
            File.WriteAllBytes(_db.DatabasePath + "-shm", new byte[32768]);
            File.WriteAllBytes(_db.DatabasePath + "-journal", new byte[512]);
        });

        await RestoreOkAsync(zip, fs);

        AssertNoStaleSidecars();
        Assert.False(File.Exists(_db.DatabasePath + "-journal"));
        AssertLiveEquals(atExport);
    }

    [Fact]
    public async Task Restore_StaleSidecarThatCannotBeDeleted_RollsBackToTheOriginal()
    {
        byte[] zip = await CreateZipAsync();
        string before = _db.LiveSha256();
        FileStream? held = null;
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace", occurrence: 1).After(_ => held = new FileStream(_db.DatabasePath + "-wal", FileMode.Create, FileAccess.ReadWrite, FileShare.None));

        try
        {
            Result result = await NewService(fs).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

            AssertRejected(result, BackupErrorCodes.IoError, before, expectSafetyZip: true);
            Assert.False(File.Exists(BakPath));
        }
        finally
        {
            held?.Dispose();
        }
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private bool WalHasFrames()
    {
        try
        {
            return new FileInfo(_db.DatabasePath + "-wal") is { Exists: true, Length: > 0 };
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static byte[] ReadEntry(ZipArchive archive, string name)
    {
        using Stream stream = archive.GetEntry(name)!.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static void CopyShared(string source, string destination)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write);
        input.CopyTo(output);
    }

    private static void DropSettingsMigration(SqliteConnection connection)
    {
        BackupTestDb.Exec(connection, "DROP TABLE Settings;");
        BackupTestDb.Exec(connection, "DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE '%_AddSettings';");
    }

    private static void AssertSnapshotsEqual(Dictionary<string, List<string>> expected, Dictionary<string, List<string>> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(k => k, StringComparer.Ordinal), actual.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (string table in expected.Keys)
        {
            Assert.True(expected[table].SequenceEqual(actual[table]), $"Table {table} differs.");
        }
    }

    private static void AssertOrphanWalIsValid(string database, string wal)
    {
        string verifyDirectory = Path.Combine(Path.GetDirectoryName(database)!, "verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(verifyDirectory);
        string verifyDb = Path.Combine(verifyDirectory, "v.db");
        File.Copy(database, verifyDb);
        File.Copy(wal, verifyDb + "-wal");
        using SqliteConnection connection = BackupTestDb.Open(verifyDb);
        Assert.Equal("1", BackupTestDb.Scalar(connection, "SELECT count(*) FROM Settings WHERE Key = 'crash.frame';"));
    }

    private (byte[] Zip, BackupOptions? Options) BuildRejectedCase(string scenario)
    {
        byte[] database = MakeBackupDatabase();
        switch (scenario)
        {
            case "garbage":
                byte[] garbage = new byte[4096];
                new Random(42).NextBytes(garbage);
                return (garbage, null);
            case "empty":
                return ([], null);
            case "missingManifest":
                return (BackupZipBuilder.Build(("data.db", database)), null);
            case "extraEntry":
                return (BackupZipBuilder.Build(("manifest.json", BackupZipBuilder.Json(BackupZipBuilder.ManifestFor(database))), ("data.db", database), ("extra.txt", "x"u8.ToArray())), null);
            case "zipSlip":
                return (BackupZipBuilder.Valid(database, databaseEntryName: "..\\..\\evil.db"), null);
            case "oversized":
                return (BackupZipBuilder.Valid(database), Options(o => o with { MaxDatabaseBytes = 1024 }));
            case "hashFlip":
                byte[] flipped = (byte[])database.Clone();
                flipped[flipped.Length / 2] ^= 0xFF;
                return (BackupZipBuilder.Build(("manifest.json", BackupZipBuilder.Json(BackupZipBuilder.ManifestFor(database))), ("data.db", flipped)), null);
            case "notSqlite":
                return (BackupZipBuilder.Valid(System.Text.Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("definitely not a sqlite database. ", 200)))), null);
            case "foreignKey":
                return (BackupZipBuilder.Valid(MakeBackupDatabase(c =>
                {
                    BackupTestDb.Exec(c, "PRAGMA foreign_keys = OFF;");
                    BackupTestDb.Exec(c, "UPDATE Expenses SET CategoryId = '99999999-9999-9999-9999-999999999999';");
                })), null);
            case "noHistory":
                return (BackupZipBuilder.Valid(MakeBackupDatabase(c => BackupTestDb.Exec(c, "DROP TABLE __EFMigrationsHistory;"))), null);
            case "newerSchema":
                return (BackupZipBuilder.Valid(MakeBackupDatabase(c => BackupTestDb.Exec(c, $"INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('{_futureId}', '9.0.0');"))), null);
            case "formatVersionTwo":
                return (BackupZipBuilder.Valid(database, m => m["formatVersion"] = 2), null);
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown scenario.");
        }
    }

    private static BackupOptions Options(Func<BackupOptions, BackupOptions> configure) =>
        configure(new BackupOptions("9.9.9-test") { Delay = (_, _) => Task.CompletedTask });

    /// <summary>Exports the current live database through the real create pipeline.</summary>
    private async Task<byte[]> CreateZipAsync()
    {
        using var buffer = new MemoryStream();
        Result result = await NewService().CreateAsync(_ => Task.FromResult<Stream>(buffer));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return buffer.ToArray();
    }

    /// <summary>A copy of the live database (rollback-journal mode) after an optional mutation.</summary>
    private byte[] MakeBackupDatabase(Action<SqliteConnection>? mutate = null)
    {
        string path = Path.Combine(_scratch, $"backup-{Guid.NewGuid():N}.db");
        using (SqliteConnection source = BackupTestDb.Open(_db.DatabasePath, SqliteOpenMode.ReadOnly))
        using (SqliteConnection destination = BackupTestDb.Open(path, SqliteOpenMode.ReadWriteCreate))
        {
            source.BackupDatabase(destination);
            BackupTestDb.Exec(destination, "PRAGMA journal_mode = DELETE;");
            mutate?.Invoke(destination);
        }

        return File.ReadAllBytes(path);
    }

    private void MutateLive()
    {
        using SqliteConnection live = BackupTestDb.Open(_db.DatabasePath);
        BackupTestDb.Exec(live, "UPDATE Settings SET Value = 'mutated' WHERE Key = 'seed.other';");
        BackupTestDb.Exec(live, "DELETE FROM Notifications;");
        BackupTestDb.Exec(live, "DELETE FROM GoalContributions;");
    }

    private SqliteBackupService NewService(IBackupFileSystem? fs = null, BackupOptions? options = null) => new(
        _db.Paths,
        _clock,
        new EfMigrationCatalog(),
        fs ?? new PhysicalBackupFileSystem(),
        options ?? Options(o => o),
        NullLogger<SqliteBackupService>.Instance);

    private async Task<RestoreOutcome> RestoreOkAsync(byte[] zip, IBackupFileSystem? fs = null)
    {
        Result result = await NewService(fs).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

        Assert.True(result.IsSuccess, result.Error?.Code + ": " + result.Error?.Message);
        AssertNoLeftovers();
        return Assert.IsType<RestoreOutcome>(result.Value);
    }

    private void AssertLiveEquals(Dictionary<string, List<string>> expected) =>
        AssertSnapshotsEqual(expected, BackupTestDb.Snapshot(_db.DatabasePath));

    private void AssertRejected(Result result, string expectedCode, string liveShaBefore, bool expectSafetyZip = false)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.True(liveShaBefore == _db.LiveSha256(), "The live database changed. Service said: " + result.Error.Message);
        AssertNoLeftovers();
        AssertNoStaleSidecars();
        Assert.False(File.Exists(BakPath));
        string[] safetyZips = Directory.Exists(BackupsDirectory) ? Directory.GetFiles(BackupsDirectory, "pre-restore-*.zip") : [];
        if (expectSafetyZip)
        {
            Assert.NotEmpty(safetyZips);
        }
        else
        {
            Assert.Empty(safetyZips);
        }

        if (Directory.Exists(BackupsDirectory))
        {
            Assert.Empty(Directory.GetFiles(BackupsDirectory, "*.tmp"));
        }
    }

    private void AssertNoLeftovers()
    {
        Assert.False(File.Exists(_db.DatabasePath + "-journal"));
        if (Directory.Exists(_db.WorkDirectory))
        {
            Assert.Empty(Directory.GetFileSystemEntries(_db.WorkDirectory));
        }
    }

    private void AssertNoStaleSidecars()
    {
        string wal = _db.DatabasePath + "-wal";
        Assert.True(!File.Exists(wal) || new FileInfo(wal).Length == 0, "A non-empty -wal must not survive next to the database.");
        Assert.False(File.Exists(_db.DatabasePath + "-journal"));
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        public DateOnly Today => DateOnly.FromDateTime(UtcNow);
    }
}
