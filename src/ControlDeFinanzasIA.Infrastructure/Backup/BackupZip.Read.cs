using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Infrastructure.Backup;

/// <summary>
/// Reading side of the backup ZIP layout (F-019, R-8): every input is untrusted, every read is capped
/// on actual bytes, and rejections are raised as <see cref="BackupRejectedException"/>.
/// </summary>
internal static partial class BackupZip
{
    /// <summary>Space allowed on top of the two entry caps for ZIP framing when staging the archive.</summary>
    internal const long ArchiveOverheadBytes = 1024 * 1024;

    /// <summary>
    /// Requires exactly the two expected entries, matched by ordinal name so no separator, parent
    /// reference, drive or absolute path can pass. Nothing is ever extracted under an entry name.
    /// </summary>
    internal static (ZipArchiveEntry Manifest, ZipArchiveEntry Database) RequireLayout(ZipArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        if (archive.Entries.Count == 2)
        {
            ZipArchiveEntry? manifest = archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, ManifestEntryName, StringComparison.Ordinal));
            ZipArchiveEntry? database = archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, DatabaseEntryName, StringComparison.Ordinal));
            if (manifest is not null && database is not null)
            {
                return (manifest, database);
            }
        }

        throw Reject(BackupErrorCodes.NotABackup, "The file is not a backup: it must contain exactly manifest.json and data.db.");
    }

    /// <summary>Copies an untrusted stream, failing as soon as more than <paramref name="cap"/> bytes arrive.</summary>
    internal static async Task CopyCappedAsync(Stream source, Stream destination, long cap, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > cap)
            {
                throw Reject(BackupErrorCodes.NotABackup, "The file is larger than any valid backup.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }

        await destination.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Copies an untrusted (possibly inflating) entry stream, hashing what is written and failing as soon
    /// as more than <paramref name="cap"/> bytes arrive. Declared entry lengths can lie; this cannot.
    /// </summary>
    internal static long CopyCapped(Stream source, Stream destination, long cap, IncrementalHash? hash, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            total += read;
            if (total > cap)
            {
                throw Reject(BackupErrorCodes.NotABackup, "The backup contains more data than its limits allow.");
            }

            hash?.AppendData(buffer, 0, read);
            destination.Write(buffer, 0, read);
        }

        destination.Flush();
        return total;
    }

    /// <summary>Parses and strictly validates the manifest; a newer <c>formatVersion</c> is reported before the shape is checked.</summary>
    /// <param name="json">Raw manifest bytes, already size-capped.</param>
    /// <param name="appSchema">Latest migration id of this application.</param>
    /// <param name="appVersion">Version of this application.</param>
    internal static BackupManifest ParseManifest(byte[] json, string appSchema, string appVersion)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw Reject(BackupErrorCodes.NotABackup, "The backup manifest is not valid JSON.");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("formatVersion", out JsonElement formatElement)
                || formatElement.ValueKind != JsonValueKind.Number
                || !formatElement.TryGetInt32(out int format)
                || format < 1)
            {
                throw Reject(BackupErrorCodes.NotABackup, "The backup manifest is missing a valid format version.");
            }

            if (format > CurrentFormatVersion)
            {
                throw new BackupRejectedException(NewerSchemaFailure(
                    OptionalString(root, "schemaVersion") ?? "unknown",
                    appSchema,
                    OptionalString(root, "appVersion") ?? "unknown",
                    appVersion));
            }

            string schemaVersion = RequiredString(root, "schemaVersion");
            string exportedAtText = RequiredString(root, "exportedAt");
            string manifestAppVersion = RequiredString(root, "appVersion");
            string dbSha256 = RequiredString(root, "dbSha256");
            if (!root.TryGetProperty("apiKeyIncluded", out JsonElement apiKey)
                || apiKey.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw Reject(BackupErrorCodes.NotABackup, "The backup manifest is missing 'apiKeyIncluded'.");
            }

            if (!DateTime.TryParse(exportedAtText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime exportedAt))
            {
                throw Reject(BackupErrorCodes.NotABackup, "The backup manifest has an invalid 'exportedAt'.");
            }

            return new BackupManifest(format, schemaVersion, DateTime.SpecifyKind(exportedAt, DateTimeKind.Utc), manifestAppVersion, dbSha256, apiKey.GetBoolean());
        }
    }

    /// <summary>The coded failure for a backup produced by a newer application, carrying both versions.</summary>
    internal static Result NewerSchemaFailure(string backupSchema, string appSchema, string backupAppVersion, string appVersion) =>
        Result.Failure(
            BackupErrorCodes.NewerSchema,
            $"This backup was created by a newer version of the application (version {backupAppVersion}, schema {backupSchema}). This application is version {appVersion}, schema {appSchema}. Update the application to restore it.",
            new Dictionary<string, string[]>
            {
                [BackupErrorCodes.DetailBackupSchema] = [backupSchema],
                [BackupErrorCodes.DetailAppSchema] = [appSchema],
                [BackupErrorCodes.DetailBackupAppVersion] = [backupAppVersion],
            });

    /// <summary>Builds the exception that carries a coded failure to the service boundary.</summary>
    internal static BackupRejectedException Reject(string code, string message) => new(Result.Failure(code, message));

    private static string RequiredString(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.String)
        {
            string? value = element.GetString();
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        throw Reject(BackupErrorCodes.NotABackup, $"The backup manifest is missing a valid '{name}'.");
    }

    private static string? OptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.String ? element.GetString() : null;
}
