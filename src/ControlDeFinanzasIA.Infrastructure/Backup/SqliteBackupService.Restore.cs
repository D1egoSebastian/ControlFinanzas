using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Infrastructure.Backup;

/// <summary>Restore pipeline (F-019, DD-6): validation, key carry-over, safety backup and the swap.</summary>
public sealed partial class SqliteBackupService
{
    private const string _sidecarJournal = "-journal";
    private const string _sidecarWal = "-wal";
    private const string _sidecarShm = "-shm";

    /// <summary>
    /// Replaces the live database with the contents of a backup. Everything before the swap works on scratch
    /// files, so every failure up to that point leaves the live database byte-identical. The swap itself is the
    /// only statement that touches the live path and runs after the incoming file was validated and normalized
    /// and a safety backup of the current data was written and re-read.
    /// </summary>
    /// <remarks>
    /// The live database runs in WAL mode. Before the swap every pooled connection is released, the WAL is
    /// folded into the main file (so the retained original loses no committed data) and the <c>-wal</c>/<c>-shm</c>
    /// sidecars are removed; the swap is refused if they cannot be. A stale sidecar next to a swapped-in file would
    /// replay the old database's frames into the restored one. Cancellation is honored until the safety backup is
    /// published and ignored afterwards.
    /// </remarks>
    /// <param name="openRead">Opens the archive; called once. The stream need not be seekable.</param>
    /// <param name="ct">Cancellation token, honored only before the swap starts.</param>
    /// <returns>Success with a <see cref="RestoreOutcome"/>, or a coded failure.</returns>
    public async Task<Result> RestoreAsync(Func<CancellationToken, Task<Stream>> openRead, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(openRead);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        string workDirectory = NewWorkDirectory();
        var progress = new RestoreProgress();
        try
        {
            return await RestoreCoreAsync(workDirectory, openRead, progress, ct).ConfigureAwait(false);
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
            _logger.LogWarning(ex, "Restoring the backup failed with SQLite error {Code}", ex.SqliteErrorCode);
            return Result.Failure(BackupErrorCodes.IoError, "The backup could not be restored. Your data was not changed.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Restoring the backup failed with an I/O error");
            return Result.Failure(BackupErrorCodes.IoError, "The backup could not be restored. Your data was not changed.");
        }
        finally
        {
            DeleteWorkDirectory(workDirectory);
            if (progress.SafetyBackupPublished)
            {
                ApplyRetention();
            }

            _gate.Release();
        }
    }

    /// <summary>
    /// Startup hook, called only after the migrations of the (possibly restored) database succeeded. Removes the
    /// original that the last restore retained at <c>data.db.pre-restore.bak</c> and sweeps the scratch
    /// directory. The <c>.bak</c> is deleted only when the live database opens, passes a quick check and carries
    /// every migration of this build; otherwise it is kept. Safety backups and every other file are never
    /// touched. It does nothing while another backup operation runs, is idempotent, and never throws.
    /// </summary>
    public void CompletePendingRestore()
    {
        bool entered = false;
        try
        {
            entered = _gate.Wait(0);
            if (!entered)
            {
                _logger.LogInformation("A backup operation is running; the restore clean-up was skipped");
                return;
            }

            RemoveRetainedOriginal();
            SweepWorkDirectory();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "Completing the pending restore failed; nothing was deleted beyond what was already logged");
        }
        finally
        {
            if (entered)
            {
                _gate.Release();
            }
        }
    }

    private void RemoveRetainedOriginal()
    {
        try
        {
            string live = _paths.DatabaseFilePath;
            string bak = live + ".pre-restore.bak";
            if (!_fs.FileExists(bak))
            {
                return;
            }

            if (!LiveIsProvenMigrated(live))
            {
                _logger.LogWarning("The database was not proven to be open and migrated, so the retained original {Bak} was kept", bak);
                return;
            }

            _fs.Delete(bak);
            _logger.LogInformation("The restore is complete; the retained original {Bak} was removed", bak);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "The retained original could not be removed and was kept");
        }
    }

    private void SweepWorkDirectory()
    {
        try
        {
            _fs.DeleteDirectory(Path.Combine(BackupsDirectory, ".work"));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "The backup scratch directory could not be swept");
        }
    }

    /// <summary>The live file opens, is healthy and lists every migration this build knows.</summary>
    private bool LiveIsProvenMigrated(string live)
    {
        if (!_fs.FileExists(live))
        {
            return false;
        }

        try
        {
            using SqliteConnection connection = OpenConnection(live, SqliteOpenMode.ReadOnly);
            if (!QuickCheckIsOk(connection)
                || Scalar(connection, "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory';") == "0")
            {
                return false;
            }

            var applied = new HashSet<string>(ReadAppliedMigrations(connection), StringComparer.Ordinal);
            return _catalog.KnownMigrationIds.All(applied.Contains);
        }
        catch (SqliteException ex)
        {
            _logger.LogWarning(ex, "The live database could not be checked after the restore");
            return false;
        }
    }

    private static bool QuickCheckIsOk(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() && string.Equals(reader.GetString(0), "ok", StringComparison.Ordinal) && !reader.Read();
    }

    [GeneratedRegex(@"^pre-restore-(?<stamp>\d{8}-\d{6})(?:-(?<n>\d+))?\.zip$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex SafetyZipNamePattern();

    private string BackupsDirectory => Path.Combine(_paths.DataDirectory, "backups");

    private async Task<Result> RestoreCoreAsync(string workDirectory, Func<CancellationToken, Task<Stream>> openRead, RestoreProgress progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string livePath = _paths.DatabaseFilePath;
        if (!_fs.FileExists(livePath))
        {
            return Result.Failure(BackupErrorCodes.IoError, "The database file was not found.");
        }

        // Nothing from a previous inspect is trusted: the archive is validated again, in full.
        Validated validated = await ValidateAsync(workDirectory, openRead, ct, declared => EnsureFreeSpace(livePath, declared)).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        // A read-only connection cannot read past a hot journal, so SQLite's own recovery runs before the
        // live file is read for the key carry-over and snapshotted for the safety backup.
        bool keyWillBeLost = await Task.Run(
            () =>
            {
                ResolveHotJournal(livePath);
                return !validated.BackupHasSettings && LiveHasApiKey();
            },
            ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        Normalized normalized = await Task.Run(() => NormalizeIncoming(validated), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        string safetyName = await WriteSafetyBackupAsync(workDirectory, ct).ConfigureAwait(false);
        progress.SafetyBackupPublished = true;

        // Point of no return for cancellation: a half-finished swap is worse than a slow one.
        string safetyPath = Path.Combine(BackupsDirectory, safetyName);
        Result? failure = await SwapAsync(validated.DatabasePath, normalized.Sha256, safetyPath).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        return Result.Success(new RestoreOutcome(safetyName, normalized.KeyCarriedOver, keyWillBeLost, validated.Info.IsOlderSchema));
    }

    /// <summary>Requires three times the larger of the live and incoming databases to be free (DD-5 step 4).</summary>
    private void EnsureFreeSpace(string livePath, long incomingLength)
    {
        long required = checked(3 * Math.Max(_fs.GetLength(livePath), incomingLength));
        if (_fs.GetAvailableFreeSpace(_paths.DataDirectory) < required)
        {
            throw new BackupRejectedException(Result.Failure(
                BackupErrorCodes.IoError,
                "There is not enough free disk space to restore this backup safely.",
                new Dictionary<string, string[]> { [BackupErrorCodes.DetailReason] = [BackupErrorCodes.ReasonInsufficientSpace] }));
        }
    }

    /// <summary>
    /// R1: works on the extracted scratch copy only. Normalizes it to the rollback-journal mode (so it carries
    /// no <c>-wal</c>), carries the local API key over when the backup has none, and hashes the final bytes.
    /// </summary>
    private Normalized NormalizeIncoming(Validated validated)
    {
        string path = validated.DatabasePath;
        bool carried = false;
        using (SqliteConnection connection = OpenConnection(path, SqliteOpenMode.ReadWrite))
        {
            Execute(connection, "PRAGMA journal_mode = DELETE;");
            if (validated.BackupHasSettings
                && string.IsNullOrEmpty(Scalar(connection, "SELECT Value FROM Settings WHERE Key = $key;", ("$key", SettingKeys.AiApiKey))))
            {
                (object Id, string Value)? liveKey = ReadLiveApiKey();
                if (liveKey is not null)
                {
                    Execute(
                        connection,
                        "INSERT INTO Settings (Id, Key, Value) VALUES ($id, $key, $value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;",
                        ("$id", liveKey.Value.Id),
                        ("$key", SettingKeys.AiApiKey),
                        ("$value", liveKey.Value.Value));
                    carried = true;
                }
            }

            if (!IntegrityIsOk(connection))
            {
                throw BackupZip.Reject(BackupErrorCodes.Corrupt, "The backup database failed its integrity check after preparation.");
            }
        }

        foreach (string sidecar in new[] { _sidecarJournal, _sidecarWal, _sidecarShm })
        {
            if (_fs.FileExists(path + sidecar))
            {
                throw new IOException("The prepared restore file left a journal file behind.");
            }
        }

        return new Normalized(carried, Sha256Hex(path));
    }

    /// <summary>Reads the live API key row read-only (raw <c>Id</c> preserved); any read failure aborts the restore.</summary>
    private (object Id, string Value)? ReadLiveApiKey()
    {
        using SqliteConnection connection = OpenConnection(_paths.DatabaseFilePath, SqliteOpenMode.ReadOnly);
        if (Scalar(connection, "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'Settings';") == "0")
        {
            return null;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Value FROM Settings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", SettingKeys.AiApiKey);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(1))
        {
            return null;
        }

        string value = reader.GetString(1);
        return value.Length == 0 ? null : (reader.GetValue(0), value);
    }

    /// <summary>
    /// R2: builds the safety backup with the create pipeline WITHOUT redaction (it never leaves the data
    /// directory), re-reads and hash-verifies it as a <c>.zip.tmp</c>, and only then publishes it.
    /// </summary>
    private async Task<string> WriteSafetyBackupAsync(string workDirectory, CancellationToken ct)
    {
        string directory = BackupsDirectory;
        _fs.CreateDirectory(directory);
        var utc = DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc);
        string stamp = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string stem = Path.Combine(directory, "pre-restore-" + stamp);
        string temp = stem + ".zip.tmp";
        string safetyWork = Path.Combine(workDirectory, "safety");
        bool published = false;
        try
        {
            Result created = await CreateCoreAsync(safetyWork, redactApiKey: false, _ => Task.FromResult<Stream>(_fs.Create(temp)), ct).ConfigureAwait(false);
            if (!created.IsSuccess)
            {
                _logger.LogWarning("The safety backup could not be created: {Code} {Message}", created.Error?.Code, created.Error?.Message);
                throw new BackupRejectedException(Result.Failure(
                    BackupErrorCodes.IoError,
                    "A safety backup of the current data could not be created, so nothing was restored."));
            }

            await Task.Run(() => VerifySafetyBackup(temp, safetyWork, ct), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            string final = NextSafetyZipPath(stem, stamp);
            _fs.Move(temp, final);
            published = true;
            return Path.GetFileName(final);
        }
        finally
        {
            if (!published)
            {
                DeleteQuietly(temp);
            }
        }
    }

    private void VerifySafetyBackup(string zipPath, string safetyWork, CancellationToken ct)
    {
        string appSchema = _catalog.KnownMigrationIds.Count > 0 ? _catalog.KnownMigrationIds[^1] : "unknown";
        string verifyPath = Path.Combine(safetyWork, "verify.db");
        try
        {
            using Stream zipStream = _fs.OpenRead(zipPath);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            (ZipArchiveEntry manifestEntry, ZipArchiveEntry databaseEntry) = BackupZip.RequireLayout(archive);
            BackupManifest manifest = BackupZip.ParseManifest(ReadManifest(manifestEntry, ct), appSchema, _options.AppVersion);
            string actual = ExtractDatabase(databaseEntry, verifyPath, ct);
            if (!manifest.ApiKeyIncluded
                || !string.Equals(actual, manifest.DbSha256, StringComparison.OrdinalIgnoreCase)
                || !HasSqliteHeader(verifyPath))
            {
                throw new IOException("The safety backup does not match what was written.");
            }
        }
        catch (Exception ex) when (ex is BackupRejectedException or InvalidDataException)
        {
            throw new IOException("The safety backup failed verification.", ex);
        }
        finally
        {
            DeleteQuietly(verifyPath);
        }
    }

    /// <summary>
    /// R3 to R7: releases every handle, gates on sidecars, performs the single <c>Replace</c> of the live path,
    /// verifies the installed file and rolls back on doubt. A failed attempt is repaired (the name invariant:
    /// the original is back at the live path) and repeated on <see cref="BackupOptions.SwapRetryDelays"/>.
    /// Cancellation is deliberately not accepted here. Returns a failure, or <see langword="null"/> on success.
    /// </summary>
    private async Task<Result?> SwapAsync(string restoredPath, string expectedHash, string safetyBackupPath)
    {
        string live = _paths.DatabaseFilePath;
        string bak = live + ".pre-restore.bak";
        IReadOnlyList<TimeSpan> delays = _options.SwapRetryDelays;

        for (int attempt = 0; ; attempt++)
        {
            SwapAttempt outcome = await Task.Run(() => AttemptSwap(restoredPath, live, bak, safetyBackupPath), CancellationToken.None).ConfigureAwait(false);
            if (outcome.Failure is null)
            {
                string? problem = await Task.Run(() => VerifyInstalled(live, expectedHash), CancellationToken.None).ConfigureAwait(false);
                return problem is null
                    ? null
                    : await RollBackAsync(live, bak, outcome.OriginalHash, safetyBackupPath, problem).ConfigureAwait(false);
            }

            if (attempt >= delays.Count)
            {
                return outcome.Failure;
            }

            await _options.Delay(delays[attempt], CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// One pass of R3 to R5. Any I/O or SQLite failure becomes a failed attempt after the name invariant was
    /// repaired; nothing here throws for a problem that a later attempt could clear.
    /// </summary>
    private SwapAttempt AttemptSwap(string restoredPath, string live, string bak, string safetyBackupPath)
    {
        string originalHash = string.Empty;
        bool replaceStarted = false;
        try
        {
            SqliteConnection.ClearAllPools();

            // A previous attempt may have left the original under the .bak name (Win32 1177 style).
            RepairNameInvariant(live, bak, safetyBackupPath);
            if (!_fs.FileExists(live))
            {
                return SwapAttempt.Failed(_swapNotDone);
            }

            ReleaseLiveSidecars(live);
            originalHash = Sha256Hex(live);

            if (_fs.FileExists(bak))
            {
                _fs.Move(bak, UniquePath(live + ".pre-restore-" + SwapStamp(), ".bak"));
            }

            replaceStarted = true;
            _fs.Replace(restoredPath, live, bak);
            return new SwapAttempt(originalHash, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or BackupRejectedException)
        {
            _logger.LogWarning(ex, "The database file could not be replaced; the original is kept");
            RepairNameInvariant(live, bak, safetyBackupPath);

            // Replace can report a failure after it already did its job: the staged file is gone and the
            // original sits under the .bak name. Verification decides; a second Replace would find nothing.
            if (replaceStarted && !_fs.FileExists(restoredPath) && _fs.FileExists(live) && _fs.FileExists(bak))
            {
                return new SwapAttempt(originalHash, null);
            }

            return SwapAttempt.Failed(ex is BackupRejectedException rejected ? rejected.Failure : _swapNotDone);
        }
    }

    private static readonly Result _swapNotDone = Result.Failure(
        BackupErrorCodes.IoError,
        "The current data could not be replaced (a file may be in use). Nothing was changed.");

    /// <summary>
    /// Makes the live path free of any state that belongs to a different database file: resolves a hot
    /// rollback journal with a writer connection, folds the WAL into the main file, and requires the
    /// <c>-wal</c>/<c>-shm</c> sidecars to be gone. Anything that cannot be resolved aborts the restore.
    /// </summary>
    private void ReleaseLiveSidecars(string live)
    {
        string wal = live + _sidecarWal;
        string shm = live + _sidecarShm;

        ResolveHotJournal(live);

        if (_fs.FileExists(wal) || _fs.FileExists(shm))
        {
            using (SqliteConnection connection = OpenConnection(live, SqliteOpenMode.ReadWrite))
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                using SqliteDataReader reader = command.ExecuteReader();
                if (!reader.Read() || reader.GetInt32(0) != 0)
                {
                    throw BackupZip.Reject(BackupErrorCodes.IoError, "The current database is in use and its pending changes could not be saved. Close other programs that use it and try again.");
                }
            }

            // Closing the last connection deletes both files; anything still here is not ours to guess about.
            if (_fs.FileExists(wal) && _fs.GetLength(wal) > 0)
            {
                throw BackupZip.Reject(BackupErrorCodes.IoError, "The current database still has pending changes in a write-ahead log. Close other programs that use it and try again.");
            }

            RemoveSidecar(wal);
            RemoveSidecar(shm);
        }
    }

    /// <summary>
    /// A hot rollback journal is resolved by letting SQLite recover the live file with a writer connection
    /// (which rolls it back and deletes it). A journal that is still there afterwards aborts the restore.
    /// </summary>
    private void ResolveHotJournal(string live)
    {
        string journal = live + _sidecarJournal;
        if (!_fs.FileExists(journal))
        {
            return;
        }

        using (SqliteConnection connection = OpenConnection(live, SqliteOpenMode.ReadWrite))
        {
            Scalar(connection, "SELECT count(*) FROM sqlite_master;");
        }

        if (_fs.FileExists(journal))
        {
            throw BackupZip.Reject(BackupErrorCodes.IoError, "The current database has an unresolved journal file. Close other programs that use it and try again.");
        }
    }

    private void RemoveSidecar(string path)
    {
        if (!_fs.FileExists(path))
        {
            return;
        }

        _fs.Delete(path);
        if (_fs.FileExists(path))
        {
            throw new IOException("A database sidecar file could not be removed.");
        }
    }

    /// <summary>Win32 <c>ReplaceFile</c> can fail after moving the original to the backup name; puts it back.</summary>
    private void RepairNameInvariant(string live, string bak, string safetyBackupPath)
    {
        try
        {
            if (!_fs.FileExists(live) && _fs.FileExists(bak))
            {
                _fs.Move(bak, live);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogCritical(ex, "The original database could not be moved back to {Live}. It is kept at {Bak}; a full copy is in {Safety}", live, bak, safetyBackupPath);
        }
    }

    /// <summary>Returns <see langword="null"/> when the installed file is exactly the validated one, else the reason.</summary>
    private string? VerifyInstalled(string live, string expectedHash)
    {
        try
        {
            // Stale sidecars of the old database must go before anything opens the new file.
            RemoveSidecar(live + _sidecarWal);
            RemoveSidecar(live + _sidecarShm);
            RemoveSidecar(live + _sidecarJournal);

            if (!string.Equals(Sha256Hex(live), expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                return "The installed database does not match the validated backup.";
            }

            using SqliteConnection connection = OpenConnection(live, SqliteOpenMode.ReadOnly);
            return QuickCheckIsOk(connection) ? null : "The installed database failed its quick check.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            _logger.LogWarning(ex, "Verifying the installed database failed");
            return "The installed database could not be verified.";
        }
    }

    /// <summary>
    /// Puts the original (kept at <paramref name="bak"/>) back and proves it is byte-identical, repeating on
    /// the swap schedule. A failure here is the worst case of the pipeline: it is logged as Critical with the
    /// paths the user needs for a manual recovery.
    /// </summary>
    private async Task<Result> RollBackAsync(string live, string bak, string originalHash, string safetyBackupPath, string reason)
    {
        _logger.LogError("Restore verification failed ({Reason}); rolling back to the original database", reason);
        string rejected = UniquePath(live + ".rollback-" + SwapStamp(), string.Empty);
        IReadOnlyList<TimeSpan> delays = _options.SwapRetryDelays;

        for (int attempt = 0; ; attempt++)
        {
            bool restored = await Task.Run(() => TryRollBack(live, bak, rejected, originalHash, safetyBackupPath), CancellationToken.None).ConfigureAwait(false);
            if (restored)
            {
                DeleteQuietly(rejected);
                return Result.Failure(BackupErrorCodes.IoError, "The restored data failed verification, so your previous data was put back unchanged.");
            }

            if (attempt >= delays.Count)
            {
                break;
            }

            await _options.Delay(delays[attempt], CancellationToken.None).ConfigureAwait(false);
        }

        _logger.LogCritical(
            "The rollback failed. The original database is kept at {Bak}; the rejected file is at {Rejected}; a full copy is in {Safety}",
            bak,
            rejected,
            safetyBackupPath);
        return Result.Failure(BackupErrorCodes.IoError, "The restore failed and the previous data could not be put back automatically. The safety backup in the backups folder holds a full copy.");
    }

    /// <summary>One rollback attempt; true only when the live path holds bytes identical to the original.</summary>
    private bool TryRollBack(string live, string bak, string rejected, string originalHash, string safetyBackupPath)
    {
        try
        {
            SqliteConnection.ClearAllPools();
            if (_fs.FileExists(bak))
            {
                _fs.Replace(bak, live, rejected);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "A rollback attempt failed");
            RepairNameInvariant(live, bak, safetyBackupPath);
        }

        try
        {
            // Whatever sits next to the live path now belongs to the rejected file, not to the original.
            RemoveSidecar(live + _sidecarWal);
            RemoveSidecar(live + _sidecarShm);
            RemoveSidecar(live + _sidecarJournal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Sidecar files of the rejected database could not be removed after the rollback");
        }

        try
        {
            return _fs.FileExists(live) && string.Equals(Sha256Hex(live), originalHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The database file could not be compared with the original after a rollback attempt");
            return false;
        }
    }

    /// <summary>R8: keeps only the newest <see cref="BackupOptions.SafetyBackupsToKeep"/> safety ZIPs; other files are never touched.</summary>
    private void ApplyRetention()
    {
        try
        {
            var candidates = new List<(string Path, string Stamp, int Sequence)>();
            foreach (string file in _fs.EnumerateFiles(BackupsDirectory, "pre-restore-*.zip"))
            {
                Match match = SafetyZipNamePattern().Match(Path.GetFileName(file));
                if (!match.Success)
                {
                    continue;
                }

                int sequence = 1;
                if (match.Groups["n"].Success && !int.TryParse(match.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out sequence))
                {
                    continue;
                }

                candidates.Add((file, match.Groups["stamp"].Value, sequence));
            }

            IEnumerable<(string Path, string Stamp, int Sequence)> stale = candidates
                .OrderByDescending(c => c.Stamp, StringComparer.Ordinal)
                .ThenByDescending(c => c.Sequence)
                .Skip(Math.Max(1, _options.SafetyBackupsToKeep));
            foreach ((string path, _, _) in stale)
            {
                DeleteQuietly(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Old safety backups could not be pruned");
        }
    }

    private string SwapStamp() =>
        DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc).ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

    /// <summary>
    /// The safety ZIP path for this second: the plain name, or <c>-N</c> with N one above the highest sequence
    /// already present. It never reuses a number that retention freed, so the newest file always sorts last.
    /// </summary>
    private string NextSafetyZipPath(string stem, string stamp)
    {
        int highest = 0;
        foreach (string file in _fs.EnumerateFiles(BackupsDirectory, "pre-restore-*.zip"))
        {
            Match match = SafetyZipNamePattern().Match(Path.GetFileName(file));
            if (match.Success && match.Groups["stamp"].Value == stamp)
            {
                int sequence = 1;
                if (!match.Groups["n"].Success || int.TryParse(match.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out sequence))
                {
                    highest = Math.Max(highest, sequence);
                }
            }
        }

        return highest == 0 ? UniquePath(stem, ".zip") : UniquePath($"{stem}-{highest + 1}", ".zip");
    }

    /// <summary>The first free path of the form <c>stem+ext</c>, <c>stem-2+ext</c>, <c>stem-3+ext</c>, ...</summary>
    private string UniquePath(string stem, string extension)
    {
        string candidate = stem + extension;
        for (int n = 2; _fs.FileExists(candidate); n++)
        {
            candidate = $"{stem}-{n}{extension}";
        }

        return candidate;
    }

    private string Sha256Hex(string path)
    {
        using Stream input = _fs.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    private void DeleteQuietly(string path)
    {
        try
        {
            _fs.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "A temporary file could not be removed");
        }
    }

    private sealed class RestoreProgress
    {
        public bool SafetyBackupPublished { get; set; }
    }

    private readonly record struct Normalized(bool KeyCarriedOver, string Sha256);

    private readonly record struct SwapAttempt(string OriginalHash, Result? Failure)
    {
        public static SwapAttempt Failed(Result failure) => new(string.Empty, failure);
    }
}
