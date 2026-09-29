using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Infrastructure.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// F-019 create pipeline (S2) against the REAL <see cref="SqliteBackupService"/> on a real temp
/// SQLite file. Only the clock and the retry delay are substituted.
/// </summary>
public sealed class SqliteBackupServiceCreateTests : IDisposable
{
    private static readonly DateTime _now = new(2026, 9, 29, 14, 30, 15, 123, DateTimeKind.Utc);

    private readonly BackupTestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Create_ProducesAZipWithExactlyManifestAndDataDb()
    {
        (Result result, byte[] zip) = await CreateToMemoryAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        using ZipArchive archive = new(new MemoryStream(zip), ZipArchiveMode.Read);
        Assert.Equal(new[] { "data.db", "manifest.json" }, archive.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task Create_ManifestCarriesTheDocumentedFields()
    {
        (Result result, byte[] zip) = await CreateToMemoryAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        using var manifest = JsonDocument.Parse(ReadEntry(zip, "manifest.json"));
        JsonElement root = manifest.RootElement;
        string latest = LatestMigrationId(_db.DatabasePath);

        Assert.Equal(1, root.GetProperty("formatVersion").GetInt32());
        Assert.Equal(_now.ToString("O"), root.GetProperty("exportedAt").GetString());
        Assert.Equal("9.9.9-test", root.GetProperty("appVersion").GetString());
        Assert.Equal(latest, root.GetProperty("schemaVersion").GetString());
        Assert.False(root.GetProperty("apiKeyIncluded").GetBoolean());
        Assert.Equal(6, root.EnumerateObject().Count());

        BackupCreated created = Assert.IsType<BackupCreated>(result.Value);
        Assert.Equal(latest, created.SchemaVersion);
        Assert.Equal(_now, created.ExportedAtUtc);
        Assert.Equal(zip.Length, created.SizeBytes);
    }

    [Fact]
    public async Task Create_DbSha256MatchesTheZippedDataDbBytes()
    {
        (_, byte[] zip) = await CreateToMemoryAsync();

        using var manifest = JsonDocument.Parse(ReadEntry(zip, "manifest.json"));
        string expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ReadEntry(zip, "data.db"))).ToLowerInvariant();

        Assert.Equal(expected, manifest.RootElement.GetProperty("dbSha256").GetString());
    }

    [Fact]
    public async Task Create_ExtractedDbPassesIntegrityCheckAndMatchesLiveExceptTheApiKeyRow()
    {
        (Result result, byte[] zip) = await CreateToMemoryAsync();
        Assert.True(result.IsSuccess, result.Error?.Message);
        string extracted = Path.Combine(_db.DataDirectory, "extracted.db");
        await File.WriteAllBytesAsync(extracted, ReadEntry(zip, "data.db"));

        using (SqliteConnection connection = BackupTestDb.Open(extracted, SqliteOpenMode.ReadOnly))
        {
            Assert.Equal("ok", BackupTestDb.Scalar(connection, "PRAGMA integrity_check;"));
        }

        Dictionary<string, List<string>> live = BackupTestDb.Snapshot(_db.DatabasePath);
        Dictionary<string, List<string>> copy = BackupTestDb.Snapshot(extracted);

        Assert.Equal(live.Keys.OrderBy(k => k, StringComparer.Ordinal), copy.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (string table in live.Keys.Where(k => k != "Settings"))
        {
            Assert.Equal(live[table], copy[table]);
        }

        Assert.Contains(live["Settings"], r => r.Contains(BackupTestDb.ApiKey, StringComparison.Ordinal));
        Assert.Equal(live["Settings"].Where(r => !r.Contains("ai.apiKey", StringComparison.Ordinal)), copy["Settings"]);
        Assert.Single(copy["Settings"]);
        Assert.NotEmpty(live["Expenses"]);
        Assert.NotEmpty(live["Notifications"]);
        Assert.NotEmpty(live["DebtPayments"]);
    }

    [Fact]
    public async Task Create_KeyBytesNeverAppearInTheZipOrTheDatabase_EvenWhenAPreviousKeyLingersInFreeSpace()
    {
        // A deleted row (for example a previous key) lingers in the dead cells of the live Settings page.
        const string previousKey = "sk-test-PREVIOUS-KEY-0123456789-abcdef";
        using (SqliteConnection live = BackupTestDb.Open(_db.DatabasePath))
        {
            BackupTestDb.Exec(live, $"INSERT INTO Settings (Id, Key, Value) VALUES ('{Guid.NewGuid()}', 'ai.previous', '{previousKey}');");
            BackupTestDb.Exec(live, "DELETE FROM Settings WHERE Key = 'ai.previous';");
            BackupTestDb.Exec(live, "DELETE FROM Settings WHERE Key = 'ai.apiKey';");
            BackupTestDb.Exec(live, $"INSERT INTO Settings (Id, Key, Value) VALUES ('{Guid.NewGuid()}', 'ai.apiKey', '{BackupTestDb.ApiKey}');");
        }

        byte[] liveBytes = await File.ReadAllBytesAsync(_db.DatabasePath);
        Assert.True(liveBytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(BackupTestDb.ApiKey)) >= 0);
        Assert.True(liveBytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(previousKey)) >= 0, "Precondition: the previous key must linger in the live file.");

        (Result result, byte[] zip) = await CreateToMemoryAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        byte[] db = ReadEntry(zip, "data.db");
        string[] secrets = [BackupTestDb.ApiKey, previousKey];
        foreach (byte[] needle in secrets.SelectMany(s => new[] { Encoding.UTF8.GetBytes(s), Encoding.Unicode.GetBytes(s) }))
        {
            Assert.False(db.AsSpan().IndexOf(needle) >= 0, "The API key bytes must not remain in the exported data.db.");
            Assert.False(zip.AsSpan().IndexOf(needle) >= 0, "The API key bytes must not appear in the raw ZIP.");
            Assert.False(ReadEntry(zip, "manifest.json").AsSpan().IndexOf(needle) >= 0);
        }
    }

    [Fact]
    public async Task Create_LeavesTheLiveDatabaseUntouchedAndKeepsItsKey()
    {
        string before = _db.LiveSha256();

        (Result result, _) = await CreateToMemoryAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(before, _db.LiveSha256());
        using SqliteConnection live = BackupTestDb.Open(_db.DatabasePath, SqliteOpenMode.ReadOnly);
        Assert.Equal(BackupTestDb.ApiKey, BackupTestDb.Scalar(live, "SELECT Value FROM Settings WHERE Key = 'ai.apiKey';"));
    }

    [Fact]
    public async Task Create_WithoutASettingsTable_StillExports()
    {
        using (SqliteConnection live = BackupTestDb.Open(_db.DatabasePath))
        {
            BackupTestDb.Exec(live, "DROP TABLE Settings;");
        }

        (Result result, byte[] zip) = await CreateToMemoryAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        string extracted = Path.Combine(_db.DataDirectory, "extracted.db");
        await File.WriteAllBytesAsync(extracted, ReadEntry(zip, "data.db"));
        Assert.DoesNotContain("Settings", BackupTestDb.Snapshot(extracted).Keys);
    }

    [Theory]
    [InlineData("wal")]
    [InlineData("delete")]
    public async Task Create_WhileAnotherConnectionIsWriting_YieldsAConsistentCopy(string journalMode)
    {
        using (SqliteConnection live = BackupTestDb.Open(_db.DatabasePath))
        {
            Assert.Equal(journalMode, BackupTestDb.Scalar(live, $"PRAGMA journal_mode={journalMode};"));
            BackupTestDb.Exec(live, "CREATE TABLE WriterProbe (Id INTEGER PRIMARY KEY, V TEXT NOT NULL);");
        }

        using var stop = new CancellationTokenSource();
        var writing = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = Task.Run(() =>
        {
            using SqliteConnection w = BackupTestDb.Open(_db.DatabasePath);
            int inserted = 0;
            while (!stop.IsCancellationRequested)
            {
                BackupTestDb.Exec(w, "INSERT INTO WriterProbe (V) VALUES ('x');");
                if (++inserted == 3)
                {
                    writing.TrySetResult(true);
                }

                Thread.Sleep(5);
            }
        });
        await writing.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var options = new BackupOptions("9.9.9-test");
        SqliteBackupService service = NewService(options);
        var buffer = new MemoryStream();
        Result result;
        try
        {
            result = await service.CreateAsync(_ => Task.FromResult<Stream>(buffer));
        }
        finally
        {
            await stop.CancelAsync();
            await writer;
        }

        Assert.True(result.IsSuccess, result.Error?.Message);
        string extracted = Path.Combine(_db.DataDirectory, "extracted.db");
        await File.WriteAllBytesAsync(extracted, ReadEntry(buffer.ToArray(), "data.db"));
        using SqliteConnection copy = BackupTestDb.Open(extracted, SqliteOpenMode.ReadOnly);
        Assert.Equal("ok", BackupTestDb.Scalar(copy, "PRAGMA integrity_check;"));
        Assert.Equal(
            BackupTestDb.Scalar(copy, "SELECT count(*) FROM WriterProbe;"),
            BackupTestDb.Scalar(copy, "SELECT max(Id) FROM WriterProbe;"));
    }

    [Fact]
    public async Task Create_WhenTheLiveDatabaseStaysLocked_RetriesThenReturnsIoErrorWithoutOpeningTheDestination()
    {
        // A rollback-journal database blocks readers while a writer holds the exclusive lock.
        using (SqliteConnection mode = BackupTestDb.Open(_db.DatabasePath))
        {
            Assert.Equal("delete", BackupTestDb.Scalar(mode, "PRAGMA journal_mode=DELETE;"));
        }

        var delays = new List<TimeSpan>();
        var options = new BackupOptions("9.9.9-test")
        {
            SnapshotRetryDelays = [TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2)],
            Delay = (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            },
        };
        var destination = new CountingDestination();

        Result result;
        using (SqliteConnection blocker = BackupTestDb.Open(_db.DatabasePath))
        {
            BackupTestDb.Exec(blocker, "BEGIN EXCLUSIVE;");
            result = await NewService(options).CreateAsync(destination.OpenAsync);
            BackupTestDb.Exec(blocker, "ROLLBACK;");
        }

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.IoError, result.Error!.Code);
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2) }, delays);
        Assert.Equal(0, destination.Opens);
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Create_WhenTheLiveDatabaseIsMissing_ReturnsIoErrorAndNeverOpensTheDestination()
    {
        File.Delete(_db.DatabasePath);
        var destination = new CountingDestination();

        Result result = await NewService().CreateAsync(destination.OpenAsync);

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.IoError, result.Error!.Code);
        Assert.Equal(0, destination.Opens);
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Create_WhenTheLiveFileIsNotADatabase_ReturnsCorruptAndNeverOpensTheDestination()
    {
        await File.WriteAllTextAsync(_db.DatabasePath, new string('x', 8192));
        var destination = new CountingDestination();

        Result result = await NewService().CreateAsync(destination.OpenAsync);

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.Corrupt, result.Error!.Code);
        Assert.Equal(0, destination.Opens);
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Create_WhenTheCopyFailsIntegrityCheck_ReturnsCorruptAndWritesZeroBytes()
    {
        DamageInteriorPages(_db.DatabasePath);
        var destination = new CountingDestination();

        Result result = await NewService().CreateAsync(destination.OpenAsync);

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.Corrupt, result.Error!.Code);
        Assert.Equal(0, destination.Opens);
        Assert.Equal(0, destination.Buffer.Length);
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Create_TruncatesALongerExistingDestination()
    {
        string path = Path.Combine(_db.DataDirectory, "user-backup.zip");
        await File.WriteAllBytesAsync(path, new byte[5 * 1024 * 1024]);

        Result result;
        await using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
        {
            result = await NewService().CreateAsync(_ => Task.FromResult<Stream>(stream));
        }

        Assert.True(result.IsSuccess, result.Error?.Message);
        BackupCreated created = Assert.IsType<BackupCreated>(result.Value);
        Assert.Equal(created.SizeBytes, new FileInfo(path).Length);
        using ZipArchive archive = ZipFile.OpenRead(path);
        Assert.Equal(2, archive.Entries.Count);
    }

    [Fact]
    public async Task Create_LeavesNoTempFilesAfterSuccess()
    {
        (Result result, _) = await CreateToMemoryAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Create_LeavesNoTempFilesAfterCancellation()
    {
        using var cts = new CancellationTokenSource();
        var destination = new CountingDestination();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await NewService().CreateAsync(
                async ct =>
                {
                    await cts.CancelAsync();
                    ct.ThrowIfCancellationRequested();
                    return await destination.OpenAsync(ct);
                },
                cts.Token));

        Assert.Equal(0, destination.Buffer.Length);
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Create_WhenCancelledBeforeStart_ThrowsAndTouchesNothing()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var destination = new CountingDestination();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await NewService().CreateAsync(destination.OpenAsync, cts.Token));

        Assert.Equal(0, destination.Opens);
        AssertNoLeftovers();
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task Create_WhenTheDestinationFails_MapsToIoErrorAndCleansUp(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "boom")!;

        Result result = await NewService().CreateAsync(_ => throw exception);

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.IoError, result.Error!.Code);
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Create_FromTheWalModeLiveDatabaseEfCreates_ProducesADeleteModeCopyAndLeavesTheLiveModeAlone()
    {
        // FINDING: EF Core 8 creates SQLite databases in WAL mode, so the live file is WAL.
        using (SqliteConnection live = BackupTestDb.Open(_db.DatabasePath, SqliteOpenMode.ReadOnly))
        {
            Assert.Equal("wal", BackupTestDb.Scalar(live, "PRAGMA journal_mode;"));
        }

        (Result result, byte[] zip) = await CreateToMemoryAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        string extracted = Path.Combine(_db.DataDirectory, "extracted.db");
        await File.WriteAllBytesAsync(extracted, ReadEntry(zip, "data.db"));
        using SqliteConnection connection = BackupTestDb.Open(extracted, SqliteOpenMode.ReadOnly);
        Assert.Equal("delete", BackupTestDb.Scalar(connection, "PRAGMA journal_mode;"));
        Assert.Equal("ok", BackupTestDb.Scalar(connection, "PRAGMA integrity_check;"));
        using SqliteConnection after = BackupTestDb.Open(_db.DatabasePath, SqliteOpenMode.ReadOnly);
        Assert.Equal("wal", BackupTestDb.Scalar(after, "PRAGMA journal_mode;"));
    }

    private static byte[] ReadEntry(byte[] zip, string name)
    {
        using ZipArchive archive = new(new MemoryStream(zip), ZipArchiveMode.Read);
        using Stream entry = archive.GetEntry(name)!.Open();
        using var buffer = new MemoryStream();
        entry.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string LatestMigrationId(string path)
    {
        using SqliteConnection connection = BackupTestDb.Open(path, SqliteOpenMode.ReadOnly);
        return BackupTestDb.Scalar(connection, "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1;")!;
    }

    private static void DamageInteriorPages(string path)
    {
        SqliteConnection.ClearAllPools();
        byte[] bytes = File.ReadAllBytes(path);
        const int pageSize = 4096;
        for (int offset = pageSize * 2; offset + 32 <= bytes.Length; offset += pageSize)
        {
            Array.Fill(bytes, (byte)0xFF, offset, 32);
        }

        File.WriteAllBytes(path, bytes);
    }

    private SqliteBackupService NewService(BackupOptions? options = null) => new(
        _db.Paths,
        new TestClock(_now),
        new EfMigrationCatalog(),
        new PhysicalBackupFileSystem(),
        options ?? new BackupOptions("9.9.9-test") { Delay = (_, _) => Task.CompletedTask },
        NullLogger<SqliteBackupService>.Instance);

    private async Task<(Result Result, byte[] Zip)> CreateToMemoryAsync()
    {
        var buffer = new MemoryStream();
        Result result = await NewService().CreateAsync(_ => Task.FromResult<Stream>(buffer));
        return (result, buffer.ToArray());
    }

    private void AssertNoLeftovers()
    {
        if (Directory.Exists(_db.WorkDirectory))
        {
            Assert.Empty(Directory.GetFileSystemEntries(_db.WorkDirectory));
        }

        string[] unexpected = Directory.GetFiles(_db.DataDirectory)
            .Select(f => Path.GetFileName(f))
            .Where(n => n is not ("data.db" or "data.db-wal" or "data.db-shm")
                && !n.StartsWith("extracted", StringComparison.Ordinal)
                && !n.StartsWith("user-backup", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(Array.Empty<string>(), unexpected);
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow => utcNow;

        public DateOnly Today => DateOnly.FromDateTime(utcNow);
    }

    private sealed class CountingDestination
    {
        public MemoryStream Buffer { get; } = new();

        public int Opens { get; private set; }

        public Task<Stream> OpenAsync(CancellationToken ct)
        {
            Opens++;
            return Task.FromResult<Stream>(Buffer);
        }
    }
}
