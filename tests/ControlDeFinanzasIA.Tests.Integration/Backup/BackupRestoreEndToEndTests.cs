using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Backup.CreateBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.InspectBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.RestoreBackup;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;
using ControlDeFinanzasIA.Infrastructure.Backup;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.UI.Services;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// F-019 S7: the roadmap completion criterion proven through real DI. Every scenario runs the REAL
/// <c>CreateBackupUseCase</c>, <c>InspectBackupUseCase</c>, <c>RestoreBackupUseCase</c> and the REAL
/// <see cref="SqliteBackupService"/> (registered by <c>AddBackup</c>) over temp-directory SQLite databases
/// built by the real EF migrations. The user's data directory is never touched.
/// </summary>
public sealed class BackupRestoreEndToEndTests : IDisposable
{
    private const string _appVersion = "9.9.9-e2e";
    private const string _futureId = "29990101000000_Future";
    private const string _olderTarget = "20260923052536_AddAnomalyAlerts";
    private const string _localKey = "sk-fresh-install-LOCAL-456";

    private readonly List<IDisposable> _disposables = [];
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), $"backup-e2e-{Guid.NewGuid():N}");

    public BackupRestoreEndToEndTests()
    {
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (IDisposable disposable in _disposables)
        {
            disposable.Dispose();
        }

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
    public async Task Roundtrip_ExportFromA_RestoreIntoAFreshInstall_ReproducesEveryTableExceptTheApiKey()
    {
        Site a = NewSite(seed: true);
        Dictionary<string, List<string>> expected = WithoutApiKey(BackupTestDb.Snapshot(a.Db.DatabasePath));
        AssertSeedIsRichAcrossTables(BackupTestDb.Snapshot(a.Db.DatabasePath));
        string zipPath = await ExportAsync(a);

        Site fresh = NewSite(seed: false);
        Dictionary<string, List<string>> emptyBefore = BackupTestDb.Snapshot(fresh.Db.DatabasePath);
        Assert.Empty(emptyBefore["Expenses"]);
        Assert.DoesNotContain(emptyBefore["Settings"], r => r.Contains(SettingKeys.AiApiKey, StringComparison.Ordinal));

        Result inspected = await fresh.Inspect.ExecuteAsync(OpenFile(zipPath));
        Assert.True(inspected.IsSuccess, inspected.Error?.Message);
        BackupInfo info = Assert.IsType<BackupInfo>(inspected.Value);
        Assert.False(info.IsOlderSchema);
        Assert.False(info.LocalApiKeyWillBeLost);
        Assert.Equal(_appVersion, info.AppVersion);

        Result restored = await fresh.Restore.ExecuteAsync(OpenFile(zipPath));

        Assert.True(restored.IsSuccess, restored.Error?.Code + ": " + restored.Error?.Message);
        RestoreOutcome outcome = Assert.IsType<RestoreOutcome>(restored.Value);
        Assert.False(outcome.ApiKeyCarriedOver);
        Assert.False(outcome.ApiKeyLost);
        Assert.True(File.Exists(fresh.BakPath), "The original is retained until the next successful start.");
        Assert.NotEmpty(Directory.GetFiles(fresh.Db.DataDirectory + "\\backups", "pre-restore-*.zip"));

        await MigrateAsync(fresh);
        Dictionary<string, List<string>> actual = BackupTestDb.Snapshot(fresh.Db.DatabasePath);
        AssertSnapshotsEqual(expected, actual);
        Assert.DoesNotContain(actual["Settings"], IsApiKeyRow);

        fresh.Provider.GetRequiredService<IBackupService>().CompletePendingRestore();

        Assert.False(File.Exists(fresh.BakPath));
        Assert.Equal(1, actual["Settings"].Count(r => r.Contains("seed.other", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Roundtrip_WhenTheFreshInstallHasItsOwnApiKey_TheLocalKeyIsCarriedOver()
    {
        Site a = NewSite(seed: true);
        Dictionary<string, List<string>> expected = WithoutApiKey(BackupTestDb.Snapshot(a.Db.DatabasePath));
        string zipPath = await ExportAsync(a);
        Site fresh = NewSite(seed: false);
        InsertSetting(fresh.Db.DatabasePath, SettingKeys.AiApiKey, _localKey);

        Result inspected = await fresh.Inspect.ExecuteAsync(OpenFile(zipPath));
        Assert.True(inspected.IsSuccess, inspected.Error?.Message);
        Result restored = await fresh.Restore.ExecuteAsync(OpenFile(zipPath));

        Assert.True(restored.IsSuccess, restored.Error?.Code + ": " + restored.Error?.Message);
        Assert.True(Assert.IsType<RestoreOutcome>(restored.Value).ApiKeyCarriedOver);
        await MigrateAsync(fresh);
        Dictionary<string, List<string>> actual = BackupTestDb.Snapshot(fresh.Db.DatabasePath);
        AssertSnapshotsEqual(expected, WithoutApiKey(actual));
        string[] keyRows = actual["Settings"].Where(IsApiKeyRow).ToArray();
        Assert.Single(keyRows);
        Assert.EndsWith("|" + _localKey, keyRows[0], StringComparison.Ordinal);
        Assert.DoesNotContain(actual["Settings"], r => r.Contains(BackupTestDb.ApiKey, StringComparison.Ordinal));
        fresh.Provider.GetRequiredService<IBackupService>().CompletePendingRestore();
        Assert.False(File.Exists(fresh.BakPath));
    }

    [Fact]
    public async Task Roundtrip_ExportNeverLeaksTheApiKeyIntoTheFile()
    {
        Site a = NewSite(seed: true);

        string zipPath = await ExportAsync(a);

        byte[] raw = await File.ReadAllBytesAsync(zipPath);
        Assert.False(
            raw.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(BackupTestDb.ApiKey)) >= 0,
            "The API key must not appear in the exported ZIP.");
    }

    [Fact]
    public async Task FutureSchemaBackup_IsRejectedAsNewerSchemaWithBothVersions_AndTheLiveDatabaseIsByteIdentical()
    {
        Site a = NewSite(seed: true);
        byte[] database = FlatCopy(a.Db.DatabasePath, c => BackupTestDb.Exec(c, $"INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('{_futureId}', '9.0.0');"));
        byte[] zip = BackupZipBuilder.Valid(database, m => m["appVersion"] = "7.7.7");
        string zipPath = Path.Combine(_scratch, "future.zip");
        await File.WriteAllBytesAsync(zipPath, zip);
        Site live = NewSite(seed: true);
        string before = live.Db.LiveSha256();
        string latestKnown = new EfMigrationCatalog().KnownMigrationIds[^1];

        Result inspected = await live.Inspect.ExecuteAsync(OpenFile(zipPath));
        Result restored = await live.Restore.ExecuteAsync(OpenFile(zipPath));

        foreach (Result result in new[] { inspected, restored })
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(BackupErrorCodes.NewerSchema, result.Error!.Code);
            Assert.Contains("7.7.7", result.Error.Message, StringComparison.Ordinal);
            Assert.Contains(_futureId, result.Error.Message, StringComparison.Ordinal);
            Assert.Contains(_appVersion, result.Error.Message, StringComparison.Ordinal);
            Assert.Contains(latestKnown, result.Error.Message, StringComparison.Ordinal);
        }

        AssertLiveUntouched(live, before);
    }

    [Fact]
    public async Task CorruptBackup_IsRejected_AndTheLiveDatabaseIsByteIdentical()
    {
        Site a = NewSite(seed: true);
        string zipPath = await ExportAsync(a);
        byte[] zip = await File.ReadAllBytesAsync(zipPath);
        zip[zip.Length / 2] ^= 0xFF;
        string corruptPath = Path.Combine(_scratch, "corrupt.zip");
        await File.WriteAllBytesAsync(corruptPath, zip);
        Site live = NewSite(seed: true);
        string before = live.Db.LiveSha256();

        Result restored = await live.Restore.ExecuteAsync(OpenFile(corruptPath));

        Assert.False(restored.IsSuccess);
        Assert.Equal(BackupErrorCodes.Corrupt, restored.Error!.Code);
        AssertLiveUntouched(live, before);
    }

    [Fact]
    public async Task OlderSchemaBackup_IsAccepted_ThenMigrateBringsItForward_AndThePendingBakIsCleared()
    {
        string markerId = Guid.NewGuid().ToString();
        byte[] older = BuildOlderDatabase(markerId);
        string zipPath = Path.Combine(_scratch, "older.zip");
        await File.WriteAllBytesAsync(zipPath, BackupZipBuilder.Valid(older, m => m["schemaVersion"] = _olderTarget));
        Site live = NewSite(seed: true);

        Result inspected = await live.Inspect.ExecuteAsync(OpenFile(zipPath));
        Assert.True(inspected.IsSuccess, inspected.Error?.Message);
        Assert.True(Assert.IsType<BackupInfo>(inspected.Value).IsOlderSchema);
        Result restored = await live.Restore.ExecuteAsync(OpenFile(zipPath));

        Assert.True(restored.IsSuccess, restored.Error?.Code + ": " + restored.Error?.Message);
        Assert.True(Assert.IsType<RestoreOutcome>(restored.Value).IsOlderSchema);
        live.Provider.GetRequiredService<IBackupService>().CompletePendingRestore();
        Assert.True(File.Exists(live.BakPath), "Migrations have not run yet, so the original must stay.");

        await MigrateAsync(live);
        Dictionary<string, List<string>> migrated = BackupTestDb.Snapshot(live.Db.DatabasePath);
        Assert.Contains("Notifications", migrated.Keys);
        Assert.Contains(migrated["Settings"], r => r.Contains(markerId, StringComparison.Ordinal));
        Assert.Contains(migrated["__EFMigrationsHistory"], r => r.StartsWith("20260926064030_AddNotifications", StringComparison.Ordinal));

        live.Provider.GetRequiredService<IBackupService>().CompletePendingRestore();

        Assert.False(File.Exists(live.BakPath));
    }

    [Fact]
    public async Task SettingsViewModel_ExportThenRestore_DrivesTheRealPipelineEndToEnd()
    {
        Site a = NewSite(seed: true);
        Dictionary<string, List<string>> expected = WithoutApiKey(BackupTestDb.Snapshot(a.Db.DatabasePath));
        string zipPath = Path.Combine(_scratch, "vm-export.zip");
        var saveDialog = new PathSaveDialog(zipPath);
        BackupSettingsViewModel exporter = NewViewModel(a, saveDialog, new PathOpenDialog(null), new RecordingNotificationCenter(), new RecordingRestartService());

        await exporter.ExportCommand.ExecuteAsync(null);

        Assert.False(exporter.IsError, exporter.StatusMessage);
        Assert.Equal(BackupPanelState.Idle, exporter.State);
        Assert.NotEmpty(exporter.StatusMessage);
        Assert.True(new FileInfo(zipPath).Length > 0);

        Site fresh = NewSite(seed: false);
        var center = new RecordingNotificationCenter();
        var restart = new RecordingRestartService();
        BackupSettingsViewModel importer = NewViewModel(fresh, new PathSaveDialog(null), new PathOpenDialog(zipPath), center, restart);

        await importer.ChooseRestoreFileCommand.ExecuteAsync(null);

        Assert.False(importer.IsError, importer.StatusMessage);
        Assert.Equal(BackupPanelState.ConfirmPending, importer.State);
        Assert.NotNull(importer.PendingInfo);
        Assert.Equal(0, center.Suspends);

        await importer.ConfirmRestoreCommand.ExecuteAsync(null);

        Assert.False(importer.IsError, importer.StatusMessage);
        Assert.Equal(BackupPanelState.RestartRequired, importer.State);
        Assert.Equal(1, center.Suspends);
        Assert.Equal(0, center.Disposals);

        importer.RestartNowCommand.Execute(null);

        Assert.Equal(1, restart.Calls);
        Assert.False(importer.RestartFailed);
        await MigrateAsync(fresh);
        AssertSnapshotsEqual(expected, BackupTestDb.Snapshot(fresh.Db.DatabasePath));
        fresh.Provider.GetRequiredService<IBackupService>().CompletePendingRestore();
        Assert.False(File.Exists(fresh.BakPath));
    }

    private static bool IsApiKeyRow(string row) => row.Contains("|" + SettingKeys.AiApiKey + "|", StringComparison.Ordinal);

    private static Dictionary<string, List<string>> WithoutApiKey(Dictionary<string, List<string>> snapshot)
    {
        var copy = snapshot.ToDictionary(p => p.Key, p => p.Value.ToList(), StringComparer.Ordinal);
        int removed = copy["Settings"].RemoveAll(IsApiKeyRow);
        Assert.Equal(1, removed);
        return copy;
    }

    private static void AssertSeedIsRichAcrossTables(Dictionary<string, List<string>> snapshot)
    {
        string[] mustHaveRows =
        [
            "Categories", "PaymentMethods", "Expenses", "Incomes", "Tags", "SavedViews", "Budgets", "Goals",
            "GoalContributions", "Debts", "DebtPayments", "RecurringRules", "AnomalyAlerts", "Notifications",
            "Settings", "__EFMigrationsHistory",
        ];
        foreach (string table in mustHaveRows)
        {
            Assert.True(snapshot.TryGetValue(table, out List<string>? rows) && rows.Count > 0, $"Seed must populate {table}.");
        }
    }

    private static void AssertSnapshotsEqual(Dictionary<string, List<string>> expected, Dictionary<string, List<string>> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(k => k, StringComparer.Ordinal), actual.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (string table in expected.Keys)
        {
            Assert.True(expected[table].SequenceEqual(actual[table]), $"Table {table} differs after the roundtrip.");
        }
    }

    private static void AssertLiveUntouched(Site live, string shaBefore)
    {
        Assert.Equal(shaBefore, live.Db.LiveSha256());
        Assert.False(File.Exists(live.Db.DatabasePath + "-journal"));
        Assert.False(File.Exists(live.BakPath));
        if (Directory.Exists(live.Db.WorkDirectory))
        {
            Assert.Empty(Directory.GetFileSystemEntries(live.Db.WorkDirectory));
        }

        string backups = Path.Combine(live.Db.DataDirectory, "backups");
        if (Directory.Exists(backups))
        {
            Assert.Empty(Directory.GetFiles(backups, "pre-restore-*.zip"));
        }
    }

    private static OpenTarget OpenFile(string path) =>
        new(Path.GetFileName(path), _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)));

    private static void InsertSetting(string databasePath, string key, string value)
    {
        using SqliteConnection connection = BackupTestDb.Open(databasePath);
        BackupTestDb.Exec(connection, $"INSERT INTO Settings (Id, Key, Value) VALUES ('{Guid.NewGuid()}', '{key}', '{value}');");
    }

    private static AppDbContext NewContext(string path) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);

    private static BackupSettingsViewModel NewViewModel(
        Site site,
        IFileSaveDialog saveDialog,
        IFileOpenDialog openDialog,
        INotificationCenter center,
        IAppRestartService restart) => new(
            site.Create,
            site.Inspect,
            site.Restore,
            saveDialog,
            openDialog,
            center,
            restart,
            new SystemClock(),
            NullLogger<BackupSettingsViewModel>.Instance,
            _appVersion);

    /// <summary>The <c>ApplyMigrations</c> equivalent: migrates the swapped database with the real migrator.</summary>
    private static async Task MigrateAsync(Site site)
    {
        using (IServiceScope scope = site.Provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetPendingMigrationsAsync());
        }

        SqliteConnection.ClearAllPools();
    }

    private static byte[] FlatCopy(string sourcePath, Action<SqliteConnection>? mutate = null)
    {
        string path = Path.Combine(Path.GetTempPath(), $"flat-{Guid.NewGuid():N}.db");
        try
        {
            using (SqliteConnection source = BackupTestDb.Open(sourcePath, SqliteOpenMode.ReadOnly))
            using (SqliteConnection destination = BackupTestDb.Open(path, SqliteOpenMode.ReadWriteCreate))
            {
                source.BackupDatabase(destination);
                BackupTestDb.Exec(destination, "PRAGMA journal_mode = DELETE;");
                mutate?.Invoke(destination);
            }

            return File.ReadAllBytes(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private byte[] BuildOlderDatabase(string markerId)
    {
        string old = Path.Combine(_scratch, $"older-{Guid.NewGuid():N}.db");
        using (AppDbContext context = NewContext(old))
        {
            context.GetService<IMigrator>().Migrate(_olderTarget);
        }

        using (SqliteConnection connection = BackupTestDb.Open(old))
        {
            BackupTestDb.Exec(connection, $"INSERT INTO Settings (Id, Key, Value) VALUES ('{Guid.NewGuid()}', '{markerId}', 'kept-through-migration');");
        }

        SqliteConnection.ClearAllPools();
        return FlatCopy(old);
    }

    private async Task<string> ExportAsync(Site site)
    {
        string path = Path.Combine(_scratch, $"export-{Guid.NewGuid():N}.zip");
        using var target = new SaveTarget(Path.GetFileName(path), _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)));

        Result result = await site.Create.ExecuteAsync(target);

        Assert.True(result.IsSuccess, result.Error?.Code + ": " + result.Error?.Message);
        return path;
    }

    private Site NewSite(bool seed)
    {
        var db = new BackupTestDb(seed);
        _disposables.Add(db);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new SystemClock());
        services.AddSingleton(db.Paths);
        services.AddPersistence(db.Paths);
        services.AddBackup(_appVersion);
        services.AddTransient<CreateBackupUseCase>();
        services.AddTransient<InspectBackupUseCase>();
        services.AddTransient<RestoreBackupUseCase>();
        ServiceProvider provider = services.BuildServiceProvider();
        _disposables.Add(provider);
        return new Site(db, provider);
    }

    private sealed class Site(BackupTestDb db, ServiceProvider provider)
    {
        public BackupTestDb Db { get; } = db;

        public ServiceProvider Provider { get; } = provider;

        public string BakPath => Db.DatabasePath + ".pre-restore.bak";

        public CreateBackupUseCase Create => Provider.GetRequiredService<CreateBackupUseCase>();

        public InspectBackupUseCase Inspect => Provider.GetRequiredService<InspectBackupUseCase>();

        public RestoreBackupUseCase Restore => Provider.GetRequiredService<RestoreBackupUseCase>();
    }

    private sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private sealed class PathSaveDialog(string? path) : IFileSaveDialog
    {
        public Task<SaveTarget?> PickAsync(string suggestedFileName, ReportFormat format, CancellationToken ct = default) =>
            Task.FromResult<SaveTarget?>(null);

        public Task<SaveTarget?> PickAsync(string suggestedFileName, FileTypeFilter filter, string? title = null, CancellationToken ct = default) =>
            Task.FromResult<SaveTarget?>(path is null
                ? null
                : new SaveTarget(Path.GetFileName(path), _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))));
    }

    private sealed class PathOpenDialog(string? path) : IFileOpenDialog
    {
        public Task<OpenTarget?> PickAsync(FileTypeFilter filter, string? title = null, CancellationToken ct = default) =>
            Task.FromResult<OpenTarget?>(path is null ? null : OpenFile(path));
    }

    private sealed class RecordingRestartService : IAppRestartService
    {
        public int Calls { get; private set; }

        public bool TryRestart()
        {
            Calls++;
            return true;
        }
    }

    private sealed class RecordingNotificationCenter : INotificationCenter
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public int Suspends { get; private set; }

        public int Disposals { get; private set; }

        public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> TryRefreshAsync(CancellationToken ct = default) => Task.FromResult(false);

        public Task<NotificationListDto?> GetAsync(CancellationToken ct = default) => Task.FromResult<NotificationListDto?>(null);

        public Task MarkReadAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) => Task.CompletedTask;

        public Task DismissAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IDisposable> SuspendAsync(CancellationToken ct = default)
        {
            Suspends++;
            return Task.FromResult<IDisposable>(new Releaser(this));
        }

        private sealed class Releaser(RecordingNotificationCenter owner) : IDisposable
        {
            public void Dispose() => owner.Disposals++;
        }
    }
}
