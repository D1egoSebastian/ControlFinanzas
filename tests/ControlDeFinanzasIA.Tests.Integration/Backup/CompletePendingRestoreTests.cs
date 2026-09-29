using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Infrastructure.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// F-019 S4b: the <c>.bak</c> lifecycle (DD-6). The startup hook removes the retained original and sweeps
/// the scratch directory, never throws, and never deletes the <c>.bak</c> unless the live database is proven
/// to open and carry every migration of this build.
/// </summary>
public sealed class CompletePendingRestoreTests : IDisposable
{
    private readonly RestoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    private string WorkRoot => _h.Db.WorkDirectory;

    [Fact]
    public async Task AfterASuccessfulRestore_DeletesTheBakAndSweepsTheWorkDirectory_ButNeverTheSafetyZipOrOtherFiles()
    {
        byte[] zip = await _h.CreateZipAsync();
        _h.MutateLive();
        Result restored = await _h.RestoreAsync(zip);
        Assert.True(restored.IsSuccess, restored.Error?.Message);
        Assert.True(File.Exists(_h.BakPath));
        Directory.CreateDirectory(Path.Combine(WorkRoot, "abandoned"));
        await File.WriteAllTextAsync(Path.Combine(WorkRoot, "abandoned", "restore.db"), "left by a crash");
        string[] keep = ["notes.txt", "my-backup.zip", "pre-restore-20200101-000001.zip"];
        foreach (string name in keep)
        {
            await File.WriteAllTextAsync(Path.Combine(_h.BackupsDirectory, name), "content of " + name);
        }

        string aside = _h.LivePath + ".pre-restore-20260101000000000.bak";
        string rejected = _h.LivePath + ".rollback-20260101000000000";
        await File.WriteAllTextAsync(aside, "anomalous leftover moved aside earlier");
        await File.WriteAllTextAsync(rejected, "rejected file");
        string[] safetyBefore = _h.SafetyZips().OrderBy(n => n, StringComparer.Ordinal).ToArray();
        string liveBefore = _h.Db.LiveSha256();

        _h.NewService().CompletePendingRestore();

        Assert.False(File.Exists(_h.BakPath));
        Assert.False(Directory.Exists(WorkRoot));
        Assert.Equal(safetyBefore, _h.SafetyZips().OrderBy(n => n, StringComparer.Ordinal).ToArray());
        foreach (string name in keep)
        {
            Assert.Equal("content of " + name, await File.ReadAllTextAsync(Path.Combine(_h.BackupsDirectory, name)));
        }

        Assert.True(File.Exists(aside));
        Assert.True(File.Exists(rejected));
        Assert.Equal(liveBefore, _h.Db.LiveSha256());
    }

    [Fact]
    public void WhenNothingIsPending_IsANoOpThatCreatesNothing()
    {
        string liveBefore = _h.Db.LiveSha256();
        string[] filesBefore = Directory.GetFileSystemEntries(_h.Db.DataDirectory, "*", SearchOption.AllDirectories).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        _h.NewService().CompletePendingRestore();

        Assert.Equal(filesBefore, Directory.GetFileSystemEntries(_h.Db.DataDirectory, "*", SearchOption.AllDirectories).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.Equal(liveBefore, _h.Db.LiveSha256());
        Assert.DoesNotContain(_h.Logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public void WhenTheDataDirectoryDoesNotExistAtAll_DoesNotThrowAndCreatesNothing()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"never-created-{Guid.NewGuid():N}");
        var paths = new MissingPaths(missing);
        var service = new SqliteBackupService(paths, _h.Clock, new EfMigrationCatalog(), new PhysicalBackupFileSystem(), _h.Options(), _h.Logger);

        service.CompletePendingRestore();

        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public async Task IsIdempotent()
    {
        byte[] zip = await _h.CreateZipAsync();
        Assert.True((await _h.RestoreAsync(zip)).IsSuccess);
        SqliteBackupService service = _h.NewService();

        service.CompletePendingRestore();
        service.CompletePendingRestore();
        service.CompletePendingRestore();

        Assert.False(File.Exists(_h.BakPath));
        RestoreHarness.AssertOpensAndIsHealthy(_h.LivePath);
    }

    [WindowsOnlyFact]
    public async Task WhenTheBakIsLocked_DoesNotThrow_KeepsTheFile_AndDeletesItOnALaterCall()
    {
        byte[] zip = await _h.CreateZipAsync();
        Assert.True((await _h.RestoreAsync(zip)).IsSuccess);
        string bakSha = BackupTestDb.Sha256Of(_h.BakPath);
        SqliteBackupService service = _h.NewService();

        using (var locked = new FileStream(_h.BakPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Exception? thrown = Record.Exception(service.CompletePendingRestore);

            Assert.Null(thrown);
            Assert.True(File.Exists(_h.BakPath));
            Assert.Contains(_h.Logger.Entries, e => e.Level == LogLevel.Warning);
        }

        Assert.Equal(bakSha, BackupTestDb.Sha256Of(_h.BakPath));

        service.CompletePendingRestore();

        Assert.False(File.Exists(_h.BakPath));
    }

    [Fact]
    public async Task WhenTheFileSystemMisbehaves_NeverThrows()
    {
        byte[] zip = await _h.CreateZipAsync();
        Assert.True((await _h.RestoreAsync(zip)).IsSuccess);
        var fs = new FaultyBackupFileSystem();
        fs.On("FileExists").Throws(new InvalidOperationException("boom"));

        Exception? thrown = Record.Exception(() => _h.NewService(fs).CompletePendingRestore());

        Assert.Null(thrown);
        Assert.True(File.Exists(_h.BakPath));
        Assert.Contains(_h.Logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task WhenTheLiveDatabaseDoesNotCarryEveryMigration_KeepsTheBak()
    {
        byte[] zip = await _h.CreateZipAsync();
        Assert.True((await _h.RestoreAsync(zip)).IsSuccess);
        using (SqliteConnection live = BackupTestDb.Open(_h.LivePath))
        {
            BackupTestDb.Exec(live, "DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE '%_AddNotifications';");
        }

        _h.NewService().CompletePendingRestore();

        Assert.True(File.Exists(_h.BakPath), "A restored database that was not migrated must not cost the user the original.");
        Assert.Contains(_h.Logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("kept", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WhenTheLiveDatabaseIsNotADatabase_KeepsTheBak()
    {
        byte[] zip = await _h.CreateZipAsync();
        Assert.True((await _h.RestoreAsync(zip)).IsSuccess);
        SqliteConnection.ClearAllPools();
        await File.WriteAllBytesAsync(_h.LivePath, new byte[8192]);

        _h.NewService().CompletePendingRestore();

        Assert.True(File.Exists(_h.BakPath));
    }

    [Fact]
    public async Task WhenTheLiveDatabaseIsMissing_KeepsTheBak()
    {
        byte[] zip = await _h.CreateZipAsync();
        Assert.True((await _h.RestoreAsync(zip)).IsSuccess);
        SqliteConnection.ClearAllPools();
        File.Delete(_h.LivePath);

        _h.NewService().CompletePendingRestore();

        Assert.True(File.Exists(_h.BakPath));
    }

    [Fact]
    public async Task WhileARestoreIsInFlight_DoesNothing_SoTheRunningRestoreKeepsItsScratchFiles()
    {
        byte[] zip = await _h.CreateZipAsync();
        using var atSwap = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        bool workDirWasThere = false;
        var fs = new FaultyBackupFileSystem();
        fs.On("Replace", occurrence: 1).Before(args =>
        {
            atSwap.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(30)));
            workDirWasThere = File.Exists(args[0]);
        });

        // One instance, like the singleton in the app: the hook and the restore share the same gate.
        SqliteBackupService service = _h.NewService(fs);
        var restoring = Task.Run(() => service.RestoreAsync(_ => Task.FromResult<Stream>(new MemoryStream(zip))));
        Assert.True(atSwap.Wait(TimeSpan.FromSeconds(30)));

        service.CompletePendingRestore();

        Assert.True(Directory.Exists(WorkRoot), "A concurrent hook must not sweep the running restore's scratch directory.");
        release.Set();
        Result result = await restoring;
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(workDirWasThere);
    }
}

internal sealed class MissingPaths(string dataDirectory) : ControlDeFinanzasIA.Application.Abstractions.IPathResolver
{
    public string DataDirectory => dataDirectory;

    public string DatabaseFilePath => Path.Combine(dataDirectory, "data.db");
}
