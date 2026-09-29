using System.Security.Cryptography;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Infrastructure.Backup;

/// <summary>
/// SQLite backup engine (F-019). This slice provides the create pipeline; inspect and restore are
/// added to this partial class by later slices, which is also when it starts implementing
/// <see cref="IBackupService"/> (no half-implemented interface, no placeholder members).
/// </summary>
/// <remarks>
/// Every connection is non-pooled and independent of <c>AppDbContext</c>, so disposing it really
/// releases the OS file handle. Scratch files live in <c>&lt;DataDir&gt;\backups\.work\&lt;guid&gt;\</c>,
/// on the same volume as the live database, and are removed in every outcome.
/// </remarks>
public sealed partial class SqliteBackupService
{
    private const int _sqliteBusy = 5;
    private const int _sqliteLocked = 6;
    private const int _sqliteCorrupt = 11;
    private const int _sqliteNotADatabase = 26;

    private readonly IPathResolver _paths;
    private readonly IClock _clock;
    private readonly IBackupFileSystem _fs;
    private readonly BackupOptions _options;
    private readonly ILogger<SqliteBackupService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates the service.</summary>
    public SqliteBackupService(
        IPathResolver paths,
        IClock clock,
        IBackupFileSystem fs,
        BackupOptions options,
        ILogger<SqliteBackupService> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _paths = paths;
        _clock = clock;
        _fs = fs;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Builds a redacted, integrity-checked, hash-verified backup ZIP in scratch space and only then
    /// opens the destination and copies it, so a failed backup never creates or truncates the file.
    /// </summary>
    /// <param name="openDestination">Opens the destination stream; called last, at most once.</param>
    /// <param name="ct">Cancellation token; cancellation is rethrown.</param>
    /// <returns>Success with a <see cref="BackupCreated"/>, or a coded failure.</returns>
    public async Task<Result> CreateAsync(Func<CancellationToken, Task<Stream>> openDestination, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(openDestination);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        string workDirectory = Path.Combine(_paths.DataDirectory, "backups", ".work", Guid.NewGuid().ToString("N"));
        try
        {
            return await CreateCoreAsync(workDirectory, openDestination, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqliteException ex)
        {
            _logger.LogWarning(ex, "Creating the backup failed with SQLite error {Code}", ex.SqliteErrorCode);
            return IsCorruption(ex)
                ? Result.Failure(BackupErrorCodes.Corrupt, "The database is damaged and cannot be backed up.")
                : Result.Failure(BackupErrorCodes.IoError, "The database could not be read for the backup.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Creating the backup failed with an I/O error");
            return Result.Failure(BackupErrorCodes.IoError, "The backup could not be written.");
        }
        finally
        {
            DeleteWorkDirectory(workDirectory);
            _gate.Release();
        }
    }

    // Microsoft.Data.Sqlite reports extended result codes (e.g. 261 = BUSY_RECOVERY, which appears
    // when a WAL database is being recovered while it is snapshotted); the low byte is the primary code.
    private static int PrimaryCode(SqliteException ex) => ex.SqliteErrorCode & 0xFF;

    private static bool IsCorruption(SqliteException ex) => PrimaryCode(ex) is _sqliteCorrupt or _sqliteNotADatabase;

    private static bool IsBusy(SqliteException ex) => PrimaryCode(ex) is _sqliteBusy or _sqliteLocked;

    private static SqliteConnection OpenConnection(string path, SqliteOpenMode mode)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false };
        var connection = new SqliteConnection(builder.ConnectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static string? Scalar(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    private static bool IntegrityIsOk(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read()
            && string.Equals(reader.GetString(0), "ok", StringComparison.Ordinal)
            && !reader.Read();
    }

    private async Task<Result> CreateCoreAsync(string workDirectory, Func<CancellationToken, Task<Stream>> openDestination, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string livePath = _paths.DatabaseFilePath;
        if (!_fs.FileExists(livePath))
        {
            return Result.Failure(BackupErrorCodes.IoError, "The database file was not found.");
        }

        _fs.CreateDirectory(workDirectory);
        string exportDatabase = Path.Combine(workDirectory, "export.db");
        string exportZip = Path.Combine(workDirectory, "export.zip");

        await SnapshotAsync(livePath, exportDatabase, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        Prepared prepared = await Task.Run(() => PrepareExport(exportDatabase), ct).ConfigureAwait(false);
        if (prepared.Failure is not null)
        {
            return prepared.Failure;
        }

        var exportedAt = DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc);
        var manifest = new BackupManifest(
            BackupZip.CurrentFormatVersion,
            prepared.SchemaVersion!,
            exportedAt,
            _options.AppVersion,
            prepared.Sha256!,
            ApiKeyIncluded: false);

        await Task.Run(
            () =>
            {
                using Stream zipStream = _fs.Create(exportZip);
                using Stream databaseStream = _fs.OpenRead(exportDatabase);
                BackupZip.Write(zipStream, manifest, databaseStream);
            },
            ct).ConfigureAwait(false);

        long size = _fs.GetLength(exportZip);
        ct.ThrowIfCancellationRequested();

        // The user's file is touched only now that the package is complete and verified.
        await using (Stream destination = await openDestination(ct).ConfigureAwait(false))
        {
            await using (Stream source = _fs.OpenRead(exportZip))
            {
                await source.CopyToAsync(destination, ct).ConfigureAwait(false);
            }

            await destination.FlushAsync(ct).ConfigureAwait(false);
            if (destination.CanSeek)
            {
                destination.SetLength(destination.Position);
            }
        }

        return Result.Success(new BackupCreated(size, exportedAt, prepared.SchemaVersion!));
    }

    /// <summary>
    /// Copies the live database into <paramref name="target"/> with the online backup API. The API does
    /// not wait for a busy source (it fails at once with SQLite error 5 or 6), so a bounded retry with
    /// a fresh destination file follows <see cref="BackupOptions.SnapshotRetryDelays"/>.
    /// </summary>
    private async Task SnapshotAsync(string livePath, string target, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                await Task.Run(() => CopyDatabase(livePath, target), ct).ConfigureAwait(false);
                return;
            }
            catch (SqliteException ex) when (IsBusy(ex) && attempt < _options.SnapshotRetryDelays.Count)
            {
                _fs.Delete(target);
                await _options.Delay(_options.SnapshotRetryDelays[attempt], ct).ConfigureAwait(false);
            }
        }
    }

    private static void CopyDatabase(string livePath, string target)
    {
        using SqliteConnection source = OpenConnection(livePath, SqliteOpenMode.ReadOnly);
        using SqliteConnection destination = OpenConnection(target, SqliteOpenMode.ReadWriteCreate);
        source.BackupDatabase(destination);
    }

    /// <summary>
    /// Redacts the API key so that no key bytes remain in the file (<c>secure_delete</c> zeroes the
    /// deleted cell, <c>VACUUM</c> rebuilds the file so no free page keeps old bytes), normalizes the
    /// journal mode, verifies integrity before and after, and hashes the result.
    /// </summary>
    private Prepared PrepareExport(string exportDatabase)
    {
        string? schemaVersion;
        using (SqliteConnection connection = OpenConnection(exportDatabase, SqliteOpenMode.ReadWrite))
        {
            // Checked first because VACUUM rebuilds the file and could otherwise hide damage.
            if (!IntegrityIsOk(connection))
            {
                return Prepared.Fail(Corrupt());
            }

            Execute(connection, "PRAGMA journal_mode = DELETE;");
            if (Scalar(connection, "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'Settings';") != "0")
            {
                Execute(connection, "PRAGMA secure_delete = ON;");
                Execute(connection, "DELETE FROM Settings WHERE Key = $key;", ("$key", SettingKeys.AiApiKey));
                Execute(connection, "VACUUM;");
            }

            if (!IntegrityIsOk(connection))
            {
                return Prepared.Fail(Corrupt());
            }

            schemaVersion = Scalar(connection, "SELECT MigrationId FROM \"__EFMigrationsHistory\" ORDER BY MigrationId DESC LIMIT 1;");
        }

        if (string.IsNullOrEmpty(schemaVersion))
        {
            return Prepared.Fail(Corrupt());
        }

        foreach (string sidecar in new[] { "-journal", "-wal", "-shm" })
        {
            if (_fs.FileExists(exportDatabase + sidecar))
            {
                throw new IOException("The export copy left a journal file behind.");
            }
        }

        using Stream hashInput = _fs.OpenRead(exportDatabase);
        string sha256 = Convert.ToHexString(SHA256.HashData(hashInput)).ToLowerInvariant();
        return new Prepared(schemaVersion, sha256, null);
    }

    private static Result Corrupt() =>
        Result.Failure(BackupErrorCodes.Corrupt, "The database is damaged and cannot be backed up.");

    private void DeleteWorkDirectory(string workDirectory)
    {
        try
        {
            _fs.DeleteDirectory(workDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The backup work directory could not be removed");
        }
    }

    private readonly record struct Prepared(string? SchemaVersion, string? Sha256, Result? Failure)
    {
        public static Prepared Fail(Result failure) => new(null, null, failure);
    }
}
