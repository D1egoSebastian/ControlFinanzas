using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Infrastructure.Backup;
using Microsoft.Data.Sqlite;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// Shared arrangement for the S4b failure-injection, real-lock and pending-restore tests: a private data
/// directory, the real <see cref="SqliteBackupService"/>, a recording (non-sleeping) delay and a capturing
/// logger. Also owns the "the live path always holds a valid database" assertions.
/// </summary>
internal sealed class RestoreHarness : IDisposable
{
    private readonly List<(TimeSpan Delay, bool TokenCanBeCancelled, bool TokenIsCancelled)> _delays = [];
    private readonly object _sync = new();

    public RestoreHarness()
    {
        Scratch = Path.Combine(Path.GetTempPath(), $"restore-harness-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Scratch);
    }

    public BackupTestDb Db { get; } = new();

    public string Scratch { get; }

    public HarnessClock Clock { get; } = new();

    public CapturingLogger<SqliteBackupService> Logger { get; } = new();

    /// <summary>Runs (on the swapping thread) each time the service asks for a backoff delay.</summary>
    public Action? OnDelay { get; set; }

    public string LivePath => Db.DatabasePath;

    public string BakPath => Db.DatabasePath + ".pre-restore.bak";

    public string BackupsDirectory => Path.Combine(Db.DataDirectory, "backups");

    public IReadOnlyList<TimeSpan> Delays
    {
        get
        {
            lock (_sync)
            {
                return _delays.Select(d => d.Delay).ToArray();
            }
        }
    }

    public IReadOnlyList<(TimeSpan Delay, bool TokenCanBeCancelled, bool TokenIsCancelled)> DelayCalls
    {
        get
        {
            lock (_sync)
            {
                return _delays.ToArray();
            }
        }
    }

    public void Dispose()
    {
        Db.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Scratch, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    public BackupOptions Options() => new("9.9.9-test")
    {
        Delay = (delay, token) =>
        {
            lock (_sync)
            {
                _delays.Add((delay, token.CanBeCanceled, token.IsCancellationRequested));
            }

            OnDelay?.Invoke();
            return Task.CompletedTask;
        },
    };

    public SqliteBackupService NewService(IBackupFileSystem? fs = null, BackupOptions? options = null) => new(
        Db.Paths,
        Clock,
        new EfMigrationCatalog(),
        fs ?? new PhysicalBackupFileSystem(),
        options ?? Options(),
        Logger);

    public async Task<byte[]> CreateZipAsync()
    {
        using var buffer = new MemoryStream();
        Result result = await NewService().CreateAsync(_ => Task.FromResult<Stream>(buffer));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return buffer.ToArray();
    }

    public Task<Result> RestoreAsync(byte[] zip, IBackupFileSystem? fs = null, BackupOptions? options = null, CancellationToken ct = default) =>
        NewService(fs, options).RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)), ct);

    /// <summary>A copy of the live database (rollback-journal mode) after an optional mutation, as bytes.</summary>
    public byte[] MakeBackupDatabase(Action<SqliteConnection>? mutate = null)
    {
        string path = Path.Combine(Scratch, $"backup-{Guid.NewGuid():N}.db");
        using (SqliteConnection source = BackupTestDb.Open(LivePath, SqliteOpenMode.ReadOnly))
        using (SqliteConnection destination = BackupTestDb.Open(path, SqliteOpenMode.ReadWriteCreate))
        {
            source.BackupDatabase(destination);
            BackupTestDb.Exec(destination, "PRAGMA journal_mode = DELETE;");
            mutate?.Invoke(destination);
        }

        return File.ReadAllBytes(path);
    }

    public void MutateLive()
    {
        using SqliteConnection live = BackupTestDb.Open(LivePath);
        BackupTestDb.Exec(live, "UPDATE Settings SET Value = 'mutated' WHERE Key = 'seed.other';");
        BackupTestDb.Exec(live, "DELETE FROM Notifications;");
        BackupTestDb.Exec(live, "DELETE FROM GoalContributions;");
    }

    public string[] SafetyZips() =>
        Directory.Exists(BackupsDirectory) ? Directory.GetFiles(BackupsDirectory, "pre-restore-*.zip") : [];

    /// <summary>
    /// Opens a pooled connection and returns it to the pool, which keeps the OS handle open. Returns whether
    /// a handle is really held: <c>ClearAllPools</c> is process-wide and another fixture may clear the pool
    /// right after, so the attempt is repeated.
    /// </summary>
    public static bool RepopulatePool(string databasePath)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            var pooled = new SqliteConnection($"Data Source={databasePath}");
            pooled.Open();
            BackupTestDb.Scalar(pooled, "SELECT count(*) FROM sqlite_master;");
            pooled.Close();
            pooled.Dispose();
            if (!CanOpenExclusively(databasePath))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when nothing else holds a handle on the file (an exclusive open succeeds).</summary>
    public static bool CanOpenExclusively(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static void AssertOpensAndIsHealthy(string path)
    {
        Assert.True(File.Exists(path), $"{path} must exist.");
        using SqliteConnection connection = BackupTestDb.Open(path, SqliteOpenMode.ReadOnly);
        Assert.Equal("ok", BackupTestDb.Scalar(connection, "PRAGMA quick_check;"));
        Assert.NotEqual("0", BackupTestDb.Scalar(connection, "SELECT count(*) FROM sqlite_master;"));
    }

    /// <summary>The data-loss invariant: the live path holds a valid, openable database with the given bytes.</summary>
    public void AssertLiveIsOriginal(string originalSha256)
    {
        AssertOpensAndIsHealthy(LivePath);
        Assert.True(originalSha256 == Db.LiveSha256(), "The live database bytes changed.");
    }

    public void AssertWorkDirectoryEmpty()
    {
        if (Directory.Exists(Db.WorkDirectory))
        {
            Assert.Empty(Directory.GetFileSystemEntries(Db.WorkDirectory));
        }

        if (Directory.Exists(BackupsDirectory))
        {
            Assert.Empty(Directory.GetFiles(BackupsDirectory, "*.tmp"));
        }
    }

    /// <summary>Failed restore that must have left the original untouched and the safety ZIP in place.</summary>
    public void AssertFailedClosed(Result result, string originalSha256, bool expectSafetyZip)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(ControlDeFinanzasIA.Application.Backup.BackupErrorCodes.IoError, result.Error!.Code);
        AssertLiveIsOriginal(originalSha256);
        AssertWorkDirectoryEmpty();
        Assert.False(File.Exists(BakPath), "No .bak may remain next to an unchanged live database.");
        if (expectSafetyZip)
        {
            Assert.NotEmpty(SafetyZips());
        }
        else
        {
            Assert.Empty(SafetyZips());
        }
    }

    /// <summary>
    /// Leaves a real hot rollback journal next to the live database (journal mode DELETE, a transaction that
    /// spilled dirty pages, file and journal copied while it was open). Returns the committed content.
    /// </summary>
    public Dictionary<string, List<string>> PlantHotJournal()
    {
        using (SqliteConnection setup = BackupTestDb.Open(LivePath))
        {
            BackupTestDb.Exec(setup, "PRAGMA journal_mode = DELETE;");
            BackupTestDb.Exec(setup, "CREATE TABLE Filler (Id INTEGER PRIMARY KEY, Pad TEXT NOT NULL);");
            BackupTestDb.Exec(setup, "WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 3000) INSERT INTO Filler (Id, Pad) SELECT i, printf('original-%05d-', i) || hex(randomblob(200)) FROM n;");
        }

        Dictionary<string, List<string>> committed = BackupTestDb.Snapshot(LivePath);
        string committedSha = Db.LiveSha256();
        string dbCopy = Path.Combine(Scratch, "hot.db");
        string journalCopy = Path.Combine(Scratch, "hot.db-journal");
        using (SqliteConnection writer = BackupTestDb.Open(LivePath))
        {
            BackupTestDb.Exec(writer, "PRAGMA cache_size = 4;");
            BackupTestDb.Exec(writer, "BEGIN;");
            BackupTestDb.Exec(writer, "UPDATE Filler SET Pad = 'DIRTY-' || Pad;");
            CopyShared(LivePath, dbCopy);
            CopyShared(LivePath + "-journal", journalCopy);
            BackupTestDb.Exec(writer, "ROLLBACK;");
        }

        Assert.True(new FileInfo(journalCopy).Length > 512, "Precondition: the journal must hold data.");
        Assert.NotEqual(committedSha, BackupTestDb.Sha256Of(dbCopy));
        File.Copy(dbCopy, LivePath, overwrite: true);
        File.Copy(journalCopy, LivePath + "-journal", overwrite: true);
        return committed;
    }

    public static void AssertSnapshotsEqual(Dictionary<string, List<string>> expected, Dictionary<string, List<string>> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(k => k, StringComparer.Ordinal), actual.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (string table in expected.Keys)
        {
            Assert.True(expected[table].SequenceEqual(actual[table]), $"Table {table} differs.");
        }
    }

    private static void CopyShared(string source, string destination)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write);
        input.CopyTo(output);
    }

    internal sealed class HarnessClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        public DateOnly Today => DateOnly.FromDateTime(UtcNow);
    }
}
