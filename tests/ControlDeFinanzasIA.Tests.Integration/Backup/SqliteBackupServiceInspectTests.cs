using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Infrastructure.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// F-019 inspect pipeline (S3) against the REAL <see cref="SqliteBackupService"/> on a real temp SQLite
/// file and crafted ZIPs. Every case also proves the live database is byte-identical afterwards, no
/// journal appeared, and the work directory is empty.
/// </summary>
public sealed class SqliteBackupServiceInspectTests : IDisposable
{
    private const string _futureId = "29990101000000_Future";

    private readonly BackupTestDb _db = new();
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), $"inspect-scratch-{Guid.NewGuid():N}");

    public SqliteBackupServiceInspectTests()
    {
        Directory.CreateDirectory(_scratch);
    }

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

    [Fact]
    public async Task Inspect_ValidBackup_ReturnsBackupInfo()
    {
        byte[] database = MakeBackupDatabase();

        BackupInfo info = await InspectOkAsync(BackupZipBuilder.Valid(database));

        Assert.Equal(BackupZipBuilder.ExportedAt, info.ExportedAtUtc);
        Assert.Equal(DateTimeKind.Utc, info.ExportedAtUtc.Kind);
        Assert.Equal("1.2.3", info.AppVersion);
        Assert.Equal(new EfMigrationCatalog().KnownMigrationIds[^1], info.SchemaVersion);
        Assert.Equal(database.Length, info.DatabaseSizeBytes);
        Assert.False(info.IsOlderSchema);
        Assert.False(info.LocalApiKeyWillBeLost);
    }

    [Fact]
    public async Task Inspect_OlderBackup_IsAcceptedAndFlagged()
    {
        byte[] database = MakeBackupDatabase(c => BackupTestDb.Exec(c, "DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE '%_AddNotifications';"));

        BackupInfo info = await InspectOkAsync(BackupZipBuilder.Valid(database));

        Assert.True(info.IsOlderSchema);
        Assert.EndsWith("_AddAnomalyAlerts", info.SchemaVersion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_UnknownMigrationId_IsNewerSchemaWithBothVersions()
    {
        byte[] database = MakeBackupDatabase(c => BackupTestDb.Exec(c, $"INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('{_futureId}', '9.0.0');"));
        string latestKnown = new EfMigrationCatalog().KnownMigrationIds[^1];

        Result result = await InspectRejectedAsync(BackupZipBuilder.Valid(database, m => m["appVersion"] = "7.7.7"), BackupErrorCodes.NewerSchema);

        Error error = result.Error!;
        Assert.Equal(_futureId, error.Details![BackupErrorCodes.DetailBackupSchema].Single());
        Assert.Equal(latestKnown, error.Details[BackupErrorCodes.DetailAppSchema].Single());
        Assert.Equal("7.7.7", error.Details[BackupErrorCodes.DetailBackupAppVersion].Single());
        Assert.Contains("7.7.7", error.Message, StringComparison.Ordinal);
        Assert.Contains(_futureId, error.Message, StringComparison.Ordinal);
        Assert.Contains("9.9.9-test", error.Message, StringComparison.Ordinal);
        Assert.Contains(latestKnown, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_FormatVersionTwo_IsNewerSchema()
    {
        byte[] zip = BackupZipBuilder.Valid(MakeBackupDatabase(), m => m["formatVersion"] = 2);

        Result result = await InspectRejectedAsync(zip, BackupErrorCodes.NewerSchema);

        Assert.Equal("1.2.3", result.Error!.Details![BackupErrorCodes.DetailBackupAppVersion].Single());
    }

    [Fact]
    public async Task Inspect_FormatVersionTwoWithForeignShape_IsStillNewerSchema()
    {
        byte[] zip = BackupZipBuilder.Build(
            ("manifest.json", BackupZipBuilder.Json(new Dictionary<string, object?> { ["formatVersion"] = 2, ["somethingNew"] = true })),
            ("data.db", MakeBackupDatabase()));

        await InspectRejectedAsync(zip, BackupErrorCodes.NewerSchema);
    }

    [Fact]
    public async Task Inspect_RandomBytes_IsNotABackup()
    {
        byte[] garbage = new byte[4096];
        new Random(42).NextBytes(garbage);

        await InspectRejectedAsync(garbage, BackupErrorCodes.NotABackup);
    }

    [Fact]
    public async Task Inspect_EmptyStream_IsNotABackup() => await InspectRejectedAsync([], BackupErrorCodes.NotABackup);

    [Fact]
    public async Task Inspect_TextFile_IsNotABackup() => await InspectRejectedAsync("hello, I am not a zip"u8.ToArray(), BackupErrorCodes.NotABackup);

    [Fact]
    public async Task Inspect_MissingManifest_IsNotABackup() =>
        await InspectRejectedAsync(BackupZipBuilder.Build(("data.db", MakeBackupDatabase())), BackupErrorCodes.NotABackup);

    [Fact]
    public async Task Inspect_MissingDatabase_IsNotABackup()
    {
        byte[] database = MakeBackupDatabase();

        await InspectRejectedAsync(BackupZipBuilder.Build(("manifest.json", BackupZipBuilder.Json(BackupZipBuilder.ManifestFor(database)))), BackupErrorCodes.NotABackup);
    }

    [Fact]
    public async Task Inspect_ExtraThirdEntry_IsNotABackup()
    {
        byte[] database = MakeBackupDatabase();
        byte[] zip = BackupZipBuilder.Build(
            ("manifest.json", BackupZipBuilder.Json(BackupZipBuilder.ManifestFor(database))),
            ("data.db", database),
            ("extra.txt", "x"u8.ToArray()));

        await InspectRejectedAsync(zip, BackupErrorCodes.NotABackup);
    }

    [Fact]
    public async Task Inspect_DirectoryEntry_IsNotABackup()
    {
        byte[] database = MakeBackupDatabase();
        byte[] zip = BackupZipBuilder.Build(
            ("manifest.json", BackupZipBuilder.Json(BackupZipBuilder.ManifestFor(database))),
            ("data.db/", []));

        await InspectRejectedAsync(zip, BackupErrorCodes.NotABackup);
    }

    [Theory]
    [InlineData("..\\..\\evil.db")]
    [InlineData("../../evil.db")]
    [InlineData("/abs/data.db")]
    [InlineData("sub/data.db")]
    [InlineData("C:\\x\\data.db")]
    public async Task Inspect_ZipSlipEntryNames_AreNotABackupAndWriteNothing(string entryName)
    {
        byte[] zip = BackupZipBuilder.Valid(MakeBackupDatabase(), databaseEntryName: entryName);
        string[] before = ListFiles(_db.DataDirectory);

        await InspectRejectedAsync(zip, BackupErrorCodes.NotABackup);

        Assert.Equal(before, ListFiles(_db.DataDirectory));
        Assert.False(File.Exists(Path.GetFullPath(Path.Combine(_db.DataDirectory, "..", "evil.db"))));
        Assert.False(File.Exists(Path.GetFullPath(Path.Combine(_db.DataDirectory, "..", "..", "evil.db"))));
    }

    [Fact]
    public async Task Inspect_DeclaredDatabaseLengthAboveCap_IsNotABackup()
    {
        byte[] zip = BackupZipBuilder.Valid(MakeBackupDatabase());

        await InspectRejectedAsync(zip, BackupErrorCodes.NotABackup, Options(o => o with { MaxDatabaseBytes = 1024 }));
    }

    [Fact]
    public async Task Inspect_ManifestAboveCap_IsNotABackup()
    {
        byte[] zip = BackupZipBuilder.Valid(MakeBackupDatabase(), m => m["padding"] = new string('x', 70 * 1024));

        await InspectRejectedAsync(zip, BackupErrorCodes.NotABackup);
    }

    [Fact]
    public async Task Inspect_DeclaredLengthLies_IsRejectedAsCorrupt()
    {
        // Real bytes exceed the cap while the central directory declares a tiny length. The BCL bounds
        // inflation at the declared size, so the truncated payload fails its checksum: never accepted,
        // never read past the cap (the service-side capped copy is the second line of defence).
        byte[] database = new byte[64 * 1024];
        new Random(7).NextBytes(database);
        byte[] zip = BackupZipBuilder.PatchDeclaredLength(BackupZipBuilder.Valid(database), "data.db", 10);

        Result result = await InspectAsync(zip, Options(o => o with { MaxDatabaseBytes = 4096 }));

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.Corrupt, result.Error!.Code);
        AssertUntouched(_db.LiveSha256());
    }

    [Fact]
    public async Task Inspect_ZipBombOverTheCap_IsNotABackup()
    {
        // A highly compressible payload: tiny archive, huge inflated size, cap lowered to make it cheap.
        byte[] bomb = new byte[2 * 1024 * 1024];
        byte[] zip = BackupZipBuilder.Valid(bomb);
        Assert.True(zip.Length < 64 * 1024);

        await InspectRejectedAsync(zip, BackupErrorCodes.NotABackup, Options(o => o with { MaxDatabaseBytes = 512 * 1024 }));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("{\"formatVersion\":1}")]
    public async Task Inspect_MalformedManifestJson_IsNotABackup(string manifestJson)
    {
        byte[] zip = BackupZipBuilder.Build(("manifest.json", System.Text.Encoding.UTF8.GetBytes(manifestJson)), ("data.db", MakeBackupDatabase()));

        await InspectRejectedAsync(zip, BackupErrorCodes.NotABackup);
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("exportedAt")]
    [InlineData("appVersion")]
    [InlineData("dbSha256")]
    [InlineData("apiKeyIncluded")]
    [InlineData("formatVersion")]
    public async Task Inspect_MissingManifestMember_IsNotABackup(string member)
    {
        byte[] zip = BackupZipBuilder.Valid(MakeBackupDatabase(), m => m.Remove(member));

        await InspectRejectedAsync(zip, BackupErrorCodes.NotABackup);
    }

    [Theory]
    [InlineData("schemaVersion", 5)]
    [InlineData("exportedAt", "yesterday")]
    [InlineData("exportedAt", 12)]
    [InlineData("appVersion", true)]
    [InlineData("dbSha256", 1)]
    [InlineData("apiKeyIncluded", "no")]
    [InlineData("formatVersion", "one")]
    public async Task Inspect_WrongManifestMemberType_IsNotABackup(string member, object value)
    {
        byte[] zip = BackupZipBuilder.Valid(MakeBackupDatabase(), m => m[member] = value);

        await InspectRejectedAsync(zip, BackupErrorCodes.NotABackup);
    }

    [Fact]
    public async Task Inspect_FlippedByteInDatabase_IsCorruptByHash()
    {
        byte[] database = MakeBackupDatabase();
        byte[] manifestSource = (byte[])database.Clone();
        database[database.Length / 2] ^= 0xFF;
        byte[] zip = BackupZipBuilder.Build(
            ("manifest.json", BackupZipBuilder.Json(BackupZipBuilder.ManifestFor(manifestSource))),
            ("data.db", database));

        await InspectRejectedAsync(zip, BackupErrorCodes.Corrupt);
    }

    [Fact]
    public async Task Inspect_HashComparisonIsCaseInsensitive()
    {
        byte[] database = MakeBackupDatabase();

        await InspectOkAsync(BackupZipBuilder.Valid(database, m => m["dbSha256"] = BackupZipBuilder.Sha256(database).ToUpperInvariant()));
    }

    [Fact]
    public async Task Inspect_NonSqliteBytesWithMatchingHash_IsCorrupt()
    {
        byte[] notSqlite = System.Text.Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("definitely not a sqlite database. ", 200)));

        await InspectRejectedAsync(BackupZipBuilder.Valid(notSqlite), BackupErrorCodes.Corrupt);
    }

    [Fact]
    public async Task Inspect_DamagedPagesWithMatchingHash_IsCorrupt()
    {
        byte[] database = MakeBackupDatabase();
        const int pageSize = 4096;
        for (int offset = pageSize * 2; offset + 32 <= database.Length; offset += pageSize)
        {
            Array.Fill(database, (byte)0xFF, offset, 32);
        }

        await InspectRejectedAsync(BackupZipBuilder.Valid(database), BackupErrorCodes.Corrupt);
    }

    [Fact]
    public async Task Inspect_ForeignKeyViolationWithMatchingHash_IsCorrupt()
    {
        byte[] database = MakeBackupDatabase(c =>
        {
            BackupTestDb.Exec(c, "PRAGMA foreign_keys = OFF;");
            BackupTestDb.Exec(c, "UPDATE Expenses SET CategoryId = '99999999-9999-9999-9999-999999999999';");
        });

        await InspectRejectedAsync(BackupZipBuilder.Valid(database), BackupErrorCodes.Corrupt);
    }

    [Fact]
    public async Task Inspect_DatabaseWithoutMigrationHistory_IsNotABackup()
    {
        byte[] database = MakeBackupDatabase(c => BackupTestDb.Exec(c, "DROP TABLE __EFMigrationsHistory;"));

        await InspectRejectedAsync(BackupZipBuilder.Valid(database), BackupErrorCodes.NotABackup);
    }

    [Fact]
    public async Task Inspect_OversizedAndWrongHash_ReportsTheSizeCapFirst()
    {
        byte[] database = MakeBackupDatabase();
        byte[] zip = BackupZipBuilder.Valid(database, m => m["dbSha256"] = new string('0', 64));

        await InspectRejectedAsync(zip, BackupErrorCodes.NotABackup, Options(o => o with { MaxDatabaseBytes = 1024 }));
    }

    [Fact]
    public async Task Inspect_PreSettingsBackup_WarnsWhenTheLiveDatabaseHasAKey()
    {
        byte[] database = MakeBackupDatabase(DropSettingsMigration);

        BackupInfo info = await InspectOkAsync(BackupZipBuilder.Valid(database));

        Assert.True(info.LocalApiKeyWillBeLost);
        Assert.True(info.IsOlderSchema);
    }

    [Fact]
    public async Task Inspect_PreSettingsBackup_DoesNotWarnWhenTheLiveDatabaseHasNoKey()
    {
        using var emptyLive = new BackupTestDb(seed: false);
        byte[] database = MakeBackupDatabase(DropSettingsMigration);
        byte[] zip = BackupZipBuilder.Valid(database);

        Result result = await NewService(emptyLive.Paths).InspectAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(((BackupInfo)result.Value!).LocalApiKeyWillBeLost);
    }

    [Fact]
    public async Task Inspect_BackupWithSettingsTable_NeverWarnsAboutTheKey()
    {
        BackupInfo info = await InspectOkAsync(BackupZipBuilder.Valid(MakeBackupDatabase()));

        Assert.False(info.LocalApiKeyWillBeLost);
    }

    [Fact]
    public async Task Inspect_NonSeekableStream_IsAccepted()
    {
        byte[] zip = BackupZipBuilder.Valid(MakeBackupDatabase());
        string before = _db.LiveSha256();

        Result result = await NewService().InspectAsync(_ => Task.FromResult<Stream>(new ForwardOnlyStream(zip)));

        Assert.True(result.IsSuccess, result.Error?.Message);
        AssertUntouched(before);
    }

    [Fact]
    public async Task Inspect_PreCancelledToken_ThrowsAndLeavesNothing()
    {
        byte[] zip = BackupZipBuilder.Valid(MakeBackupDatabase());
        string before = _db.LiveSha256();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NewService().InspectAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)), cts.Token));

        AssertUntouched(before);
    }

    [Fact]
    public async Task Inspect_OpenReadThrowsIoException_IsIoError()
    {
        string before = _db.LiveSha256();

        Result result = await NewService().InspectAsync(_ => throw new IOException("picker failed"));

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.IoError, result.Error!.Code);
        AssertUntouched(before);
    }

    [Fact]
    public async Task Inspect_CalledTwice_GivesTheSameAnswer()
    {
        byte[] zip = BackupZipBuilder.Valid(MakeBackupDatabase());
        SqliteBackupService service = NewService();

        Result first = await service.InspectAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));
        Result second = await service.InspectAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

        Assert.Equal((BackupInfo)first.Value!, (BackupInfo)second.Value!);
    }

    private static void DropSettingsMigration(SqliteConnection connection)
    {
        BackupTestDb.Exec(connection, "DROP TABLE Settings;");
        BackupTestDb.Exec(connection, "DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE '%_AddSettings';");
    }

    private static string[] ListFiles(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f))
            .Where(f => !f.StartsWith("data.db-", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

    private static BackupOptions Options(Func<BackupOptions, BackupOptions> configure) =>
        configure(new BackupOptions("9.9.9-test") { Delay = (_, _) => Task.CompletedTask });

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

    private SqliteBackupService NewService(IPathResolver? paths = null, BackupOptions? options = null) => new(
        paths ?? _db.Paths,
        new FixedClock(),
        new EfMigrationCatalog(),
        new PhysicalBackupFileSystem(),
        options ?? Options(o => o),
        NullLogger<SqliteBackupService>.Instance);

    private Task<Result> InspectAsync(byte[] zip, BackupOptions? options = null) =>
        NewService(options: options).InspectAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

    private async Task<BackupInfo> InspectOkAsync(byte[] zip)
    {
        string before = _db.LiveSha256();

        Result result = await InspectAsync(zip);

        Assert.True(result.IsSuccess, result.Error?.Code + ": " + result.Error?.Message);
        AssertUntouched(before);
        return Assert.IsType<BackupInfo>(result.Value);
    }

    private async Task<Result> InspectRejectedAsync(byte[] zip, string expectedCode, BackupOptions? options = null)
    {
        string before = _db.LiveSha256();

        Result result = await InspectAsync(zip, options);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error!.Code);
        AssertUntouched(before);
        return result;
    }

    private void AssertUntouched(string liveShaBefore)
    {
        Assert.Equal(liveShaBefore, _db.LiveSha256());
        Assert.False(File.Exists(_db.DatabasePath + "-journal"));
        if (Directory.Exists(_db.WorkDirectory))
        {
            Assert.Empty(Directory.GetFileSystemEntries(_db.WorkDirectory));
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        public DateOnly Today => new(2026, 9, 29);
    }

    private sealed class ForwardOnlyStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
