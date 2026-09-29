using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Infrastructure.Backup;

/// <summary>Inspect pipeline (F-019, DD-5): validates a backup without touching the live database.</summary>
public sealed partial class SqliteBackupService
{
    private static readonly byte[] _sqliteHeader = Encoding.ASCII.GetBytes("SQLite format 3\0");

    /// <summary>
    /// Validates a backup archive end to end (structure, caps, manifest, hash, integrity, foreign keys and
    /// schema compatibility) inside a private work directory and returns what the confirmation panel needs.
    /// The live database is only ever read, and every rejection leaves the inputs untouched.
    /// </summary>
    /// <param name="openRead">Opens the archive; called once. The stream need not be seekable.</param>
    /// <param name="ct">Cancellation token; cancellation is rethrown.</param>
    /// <returns>Success with a <see cref="BackupInfo"/>, or a coded failure.</returns>
    public async Task<Result> InspectAsync(Func<CancellationToken, Task<Stream>> openRead, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(openRead);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        string workDirectory = NewWorkDirectory();
        try
        {
            Validated validated = await ValidateAsync(workDirectory, openRead, ct).ConfigureAwait(false);
            return Result.Success(validated.Info);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BackupRejectedException ex)
        {
            return ex.Failure;
        }
        catch (SqliteException ex)
        {
            _logger.LogWarning(ex, "Inspecting the backup failed with SQLite error {Code}", ex.SqliteErrorCode);
            return Result.Failure(BackupErrorCodes.IoError, "The backup could not be read.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Inspecting the backup failed with an I/O error");
            return Result.Failure(BackupErrorCodes.IoError, "The backup could not be read.");
        }
        finally
        {
            DeleteWorkDirectory(workDirectory);
            _gate.Release();
        }
    }

    private string NewWorkDirectory() =>
        Path.Combine(_paths.DataDirectory, "backups", ".work", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Shared validation core. Stages the archive and extracts <c>data.db</c> to <c>restore.db</c> inside
    /// <paramref name="workDirectory"/> (fixed names, never entry names), then verifies everything.
    /// Throws <see cref="BackupRejectedException"/> for any rejection; the caller owns the work directory.
    /// </summary>
    private async Task<Validated> ValidateAsync(string workDirectory, Func<CancellationToken, Task<Stream>> openRead, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _fs.CreateDirectory(workDirectory);
        string incoming = Path.Combine(workDirectory, "incoming.zip");
        string extracted = Path.Combine(workDirectory, "restore.db");

        // ZipArchive needs a seekable stream and picker streams may not be; the copy is capped on real bytes.
        await using (Stream source = await openRead(ct).ConfigureAwait(false))
        await using (Stream staged = _fs.Create(incoming))
        {
            long stagingCap = _options.MaxDatabaseBytes + _options.MaxManifestBytes + BackupZip.ArchiveOverheadBytes;
            await BackupZip.CopyCappedAsync(source, staged, stagingCap, ct).ConfigureAwait(false);
        }

        return await Task.Run(() => ValidateStaged(incoming, extracted, ct), ct).ConfigureAwait(false);
    }

    private Validated ValidateStaged(string incoming, string extracted, CancellationToken ct)
    {
        string appVersion = _options.AppVersion;
        string appSchema = _catalog.KnownMigrationIds.Count > 0 ? _catalog.KnownMigrationIds[^1] : "unknown";

        BackupManifest manifest;
        string actualHash;
        using (Stream zipStream = _fs.OpenRead(incoming))
        {
            ZipArchive archive;
            try
            {
                archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            }
            catch (InvalidDataException)
            {
                throw BackupZip.Reject(BackupErrorCodes.NotABackup, "The file is not a valid backup archive.");
            }

            using (archive)
            {
                (ZipArchiveEntry manifestEntry, ZipArchiveEntry databaseEntry) = BackupZip.RequireLayout(archive);
                if (manifestEntry.Length > _options.MaxManifestBytes || databaseEntry.Length > _options.MaxDatabaseBytes)
                {
                    throw BackupZip.Reject(BackupErrorCodes.NotABackup, "The backup declares more data than the allowed limits.");
                }

                manifest = BackupZip.ParseManifest(ReadManifest(manifestEntry, ct), appSchema, appVersion);
                actualHash = ExtractDatabase(databaseEntry, extracted, ct);
            }
        }

        if (!string.Equals(actualHash, manifest.DbSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw BackupZip.Reject(BackupErrorCodes.Corrupt, "The backup data does not match its recorded checksum.");
        }

        return VerifyDatabase(extracted, manifest, appSchema, ct);
    }

    private byte[] ReadManifest(ZipArchiveEntry entry, CancellationToken ct)
    {
        try
        {
            using Stream input = entry.Open();
            using var buffer = new MemoryStream();
            BackupZip.CopyCapped(input, buffer, _options.MaxManifestBytes, null, ct);
            return buffer.ToArray();
        }
        catch (InvalidDataException)
        {
            throw BackupZip.Reject(BackupErrorCodes.NotABackup, "The backup manifest could not be read.");
        }
    }

    private string ExtractDatabase(ZipArchiveEntry entry, string destinationPath, CancellationToken ct)
    {
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using (Stream input = entry.Open())
            using (Stream output = _fs.Create(destinationPath))
            {
                BackupZip.CopyCapped(input, output, _options.MaxDatabaseBytes, hash, ct);
            }

            return Convert.ToHexString(hash.GetHashAndReset());
        }
        catch (InvalidDataException)
        {
            throw BackupZip.Reject(BackupErrorCodes.Corrupt, "The backup data could not be decompressed.");
        }
    }

    private Validated VerifyDatabase(string databasePath, BackupManifest manifest, string appSchema, CancellationToken ct)
    {
        if (!HasSqliteHeader(databasePath))
        {
            throw BackupZip.Reject(BackupErrorCodes.Corrupt, "The backup data is not a SQLite database.");
        }

        ct.ThrowIfCancellationRequested();
        List<string> applied;
        bool hasSettings;
        try
        {
            using SqliteConnection connection = OpenConnection(databasePath, SqliteOpenMode.ReadOnly);
            if (!IntegrityIsOk(connection))
            {
                throw BackupZip.Reject(BackupErrorCodes.Corrupt, "The backup database failed its integrity check.");
            }

            if (HasForeignKeyViolations(connection))
            {
                throw BackupZip.Reject(BackupErrorCodes.Corrupt, "The backup database has broken relationships between records.");
            }

            if (Scalar(connection, "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory';") == "0")
            {
                throw BackupZip.Reject(BackupErrorCodes.NotABackup, "The backup database has no migration history.");
            }

            applied = ReadAppliedMigrations(connection);
            hasSettings = Scalar(connection, "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'Settings';") != "0";
        }
        catch (SqliteException ex) when (IsCorruption(ex))
        {
            throw BackupZip.Reject(BackupErrorCodes.Corrupt, "The backup database is damaged.");
        }

        if (applied.Count == 0)
        {
            throw BackupZip.Reject(BackupErrorCodes.NotABackup, "The backup database has no applied migrations.");
        }

        IReadOnlyList<string> known = _catalog.KnownMigrationIds;
        var knownSet = new HashSet<string>(known, StringComparer.Ordinal);
        var appliedSet = new HashSet<string>(applied, StringComparer.Ordinal);
        string backupSchema = applied.Max(StringComparer.Ordinal)!;
        if (applied.Any(id => !knownSet.Contains(id)))
        {
            throw new BackupRejectedException(BackupZip.NewerSchemaFailure(backupSchema, appSchema, manifest.AppVersion, _options.AppVersion));
        }

        bool isOlder = known.Any(id => !appliedSet.Contains(id));
        bool keyWillBeLost = !hasSettings && LiveHasApiKey();
        long length = _fs.GetLength(databasePath);
        var info = new BackupInfo(manifest.ExportedAt, manifest.AppVersion, backupSchema, isOlder, keyWillBeLost, length);
        return new Validated(databasePath, manifest, info, hasSettings);
    }

    private bool HasSqliteHeader(string path)
    {
        using Stream stream = _fs.OpenRead(path);
        byte[] header = new byte[_sqliteHeader.Length];
        return stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) == header.Length
            && header.AsSpan().SequenceEqual(_sqliteHeader);
    }

    private static bool HasForeignKeyViolations(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read();
    }

    private static List<string> ReadAppliedMigrations(SqliteConnection connection)
    {
        var ids = new List<string>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM \"__EFMigrationsHistory\";";
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    /// <summary>Whether the live database currently stores a non-empty API key (read-only; false when unknown).</summary>
    private bool LiveHasApiKey()
    {
        string livePath = _paths.DatabaseFilePath;
        if (!_fs.FileExists(livePath))
        {
            return false;
        }

        try
        {
            using SqliteConnection connection = OpenConnection(livePath, SqliteOpenMode.ReadOnly);
            if (Scalar(connection, "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'Settings';") == "0")
            {
                return false;
            }

            return !string.IsNullOrEmpty(Scalar(connection, "SELECT Value FROM Settings WHERE Key = $key;", ("$key", SettingKeys.AiApiKey)));
        }
        catch (SqliteException ex)
        {
            _logger.LogWarning(ex, "The live API key could not be checked; assuming there is none");
            return false;
        }
    }

    /// <summary>Facts produced by a fully validated backup, reused by the restore pipeline.</summary>
    private sealed record Validated(string DatabasePath, BackupManifest Manifest, BackupInfo Info, bool BackupHasSettings);
}
