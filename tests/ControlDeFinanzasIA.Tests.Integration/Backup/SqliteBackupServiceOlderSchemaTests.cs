using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Infrastructure.Backup;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>F-019 S4b (S-15, D-14): a backup from an older schema is restored and then migrated forward without loss.</summary>
public sealed class SqliteBackupServiceOlderSchemaTests : IDisposable
{
    private const string _olderTarget = "20260923052536_AddAnomalyAlerts";
    private const string _marker = "older.schema.marker";

    private readonly RestoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Restore_OfABackupWithAProperSubsetOfTheMigrations_ThenMigrate_SucceedsAndKeepsEveryRow()
    {
        (byte[] database, Dictionary<string, List<string>> olderRows) = BuildOlderDatabase();
        byte[] zip = BackupZipBuilder.Valid(database, m => m["schemaVersion"] = _olderTarget);
        SqliteBackupService service = _h.NewService();
        Result inspected = await service.InspectAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));
        Assert.True(inspected.IsSuccess, inspected.Error?.Message);
        Assert.True(Assert.IsType<BackupInfo>(inspected.Value).IsOlderSchema);

        Result restored = await service.RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)));

        Assert.True(restored.IsSuccess, restored.Error?.Code + ": " + restored.Error?.Message);
        Assert.True(Assert.IsType<RestoreOutcome>(restored.Value).IsOlderSchema);
        using (SqliteConnection connection = BackupTestDb.Open(_h.LivePath, SqliteOpenMode.ReadOnly))
        {
            Assert.Equal("0", BackupTestDb.Scalar(connection, "SELECT count(*) FROM sqlite_master WHERE name = 'Notifications';"));
        }

        using (AppDbContext context = NewContext(_h.LivePath))
        {
            context.Database.Migrate();
            Assert.Empty(context.Database.GetPendingMigrations());
        }

        SqliteConnection.ClearAllPools();
        Dictionary<string, List<string>> migrated = BackupTestDb.Snapshot(_h.LivePath);
        foreach ((string table, List<string> rows) in olderRows.Where(t => t.Key is not "__EFMigrationsHistory" and not "Settings"))
        {
            Assert.True(rows.SequenceEqual(migrated[table]), $"Table {table} lost or changed rows.");
        }

        // The local API key is carried into a backup that has none, so Settings is the old rows plus that one.
        Assert.Empty(olderRows["Settings"].Except(migrated["Settings"]));
        Assert.Equal(olderRows["Settings"].Count + 1, migrated["Settings"].Count);
        Assert.Contains(migrated["Settings"], r => r.Contains(_marker, StringComparison.Ordinal));
        Assert.Contains("Notifications", migrated.Keys);
        Assert.Contains(migrated["__EFMigrationsHistory"], r => r.StartsWith("20260926064030_AddNotifications", StringComparison.Ordinal));

        service.CompletePendingRestore();

        Assert.False(File.Exists(_h.BakPath));
    }

    [Fact]
    public async Task CompletePendingRestore_BeforeTheOlderRestoredDatabaseWasMigrated_KeepsTheBak()
    {
        (byte[] database, _) = BuildOlderDatabase();
        byte[] zip = BackupZipBuilder.Valid(database, m => m["schemaVersion"] = _olderTarget);
        SqliteBackupService service = _h.NewService();
        Assert.True((await service.RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip)))).IsSuccess);

        service.CompletePendingRestore();

        Assert.True(File.Exists(_h.BakPath), "Migrations have not run yet, so the original must stay.");
    }

    private static AppDbContext NewContext(string path) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);

    private (byte[] Database, Dictionary<string, List<string>> Rows) BuildOlderDatabase()
    {
        string old = Path.Combine(_h.Scratch, $"older-{Guid.NewGuid():N}.db");
        using (AppDbContext context = NewContext(old))
        {
            context.GetService<IMigrator>().Migrate(_olderTarget);
        }

        using (SqliteConnection connection = BackupTestDb.Open(old))
        {
            BackupTestDb.Exec(connection, $"INSERT INTO Settings (Id, Key, Value) VALUES ('{Guid.NewGuid()}', '{_marker}', 'kept-through-migration');");
        }

        string flat = Path.Combine(_h.Scratch, $"older-flat-{Guid.NewGuid():N}.db");
        using (SqliteConnection source = BackupTestDb.Open(old, SqliteOpenMode.ReadOnly))
        using (SqliteConnection destination = BackupTestDb.Open(flat, SqliteOpenMode.ReadWriteCreate))
        {
            source.BackupDatabase(destination);
            BackupTestDb.Exec(destination, "PRAGMA journal_mode = DELETE;");
        }

        SqliteConnection.ClearAllPools();
        return (File.ReadAllBytes(flat), BackupTestDb.Snapshot(flat));
    }
}
