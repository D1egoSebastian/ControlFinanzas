using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using ControlDeFinanzasIA.Application.Backup;

namespace ControlDeFinanzasIA.Infrastructure.Backup;

/// <summary>
/// Reads and writes the backup ZIP layout (F-019, R-2): exactly two entries, <c>manifest.json</c> and
/// <c>data.db</c>, with deterministic compression. Exercised through <see cref="SqliteBackupService"/>.
/// </summary>
internal static class BackupZip
{
    /// <summary>Name of the manifest entry.</summary>
    internal const string ManifestEntryName = "manifest.json";

    /// <summary>Name of the database entry.</summary>
    internal const string DatabaseEntryName = "data.db";

    /// <summary>Format version written by this application.</summary>
    internal const int CurrentFormatVersion = 1;

    /// <summary>Serializes a manifest as camelCase JSON; <c>exportedAt</c> uses the round-trip "O" format.</summary>
    internal static byte[] SerializeManifest(BackupManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", manifest.FormatVersion);
            writer.WriteString("schemaVersion", manifest.SchemaVersion);
            writer.WriteString("exportedAt", DateTime.SpecifyKind(manifest.ExportedAt, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("appVersion", manifest.AppVersion);
            writer.WriteString("dbSha256", manifest.DbSha256);
            writer.WriteBoolean("apiKeyIncluded", manifest.ApiKeyIncluded);
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    /// <summary>Writes the two-entry archive to <paramref name="destination"/>, which is left open.</summary>
    internal static void Write(Stream destination, BackupManifest manifest, Stream database)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(database);

        DateTimeOffset stamp = new(DateTime.SpecifyKind(manifest.ExportedAt, DateTimeKind.Utc));
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        ZipArchiveEntry manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        manifestEntry.LastWriteTime = stamp;
        using (Stream manifestStream = manifestEntry.Open())
        {
            manifestStream.Write(SerializeManifest(manifest));
        }

        ZipArchiveEntry databaseEntry = archive.CreateEntry(DatabaseEntryName, CompressionLevel.Optimal);
        databaseEntry.LastWriteTime = stamp;
        using Stream databaseStream = databaseEntry.Open();
        database.CopyTo(databaseStream);
    }
}
