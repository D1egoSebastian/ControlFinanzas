using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>
/// Builds crafted backup archives for the malformed and adversarial cases (F-019, task 3.3): arbitrary
/// entry names, bytes and manifests, plus a central-directory patch to fake a declared length.
/// </summary>
internal static class BackupZipBuilder
{
    public static readonly DateTime ExportedAt = new(2026, 9, 1, 10, 20, 30, DateTimeKind.Utc);

    /// <summary>Zip with the given entries, in order.</summary>
    public static byte[] Build(params (string Name, byte[] Data)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, byte[] data) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using Stream stream = entry.Open();
                stream.Write(data);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>The manifest members of a valid backup of <paramref name="database"/>; callers mutate them.</summary>
    public static Dictionary<string, object?> ManifestFor(byte[] database, string schemaVersion = "20260926064030_AddNotifications") => new()
    {
        ["formatVersion"] = 1,
        ["schemaVersion"] = schemaVersion,
        ["exportedAt"] = ExportedAt.ToString("O"),
        ["appVersion"] = "1.2.3",
        ["dbSha256"] = Sha256(database),
        ["apiKeyIncluded"] = false,
    };

    public static byte[] Json(Dictionary<string, object?> members) => JsonSerializer.SerializeToUtf8Bytes(members);

    /// <summary>A well-formed backup: manifest computed from <paramref name="database"/>, optionally altered.</summary>
    public static byte[] Valid(byte[] database, Action<Dictionary<string, object?>>? alterManifest = null, string databaseEntryName = "data.db")
    {
        Dictionary<string, object?> manifest = ManifestFor(database);
        alterManifest?.Invoke(manifest);
        return Build(("manifest.json", Json(manifest)), (databaseEntryName, database));
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>Rewrites the uncompressed size that the central directory declares for an entry.</summary>
    public static byte[] PatchDeclaredLength(byte[] zip, string entryName, uint declared)
    {
        byte[] copy = (byte[])zip.Clone();
        byte[] name = Encoding.ASCII.GetBytes(entryName);
        for (int i = 0; i + 46 + name.Length <= copy.Length; i++)
        {
            // Central directory file header signature 0x02014b50, name at offset 46.
            if (BinaryPrimitives.ReadUInt32LittleEndian(copy.AsSpan(i)) == 0x02014b50
                && copy.AsSpan(i + 46, name.Length).SequenceEqual(name)
                && BinaryPrimitives.ReadUInt16LittleEndian(copy.AsSpan(i + 28)) == name.Length)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(i + 24), declared);
                return copy;
            }
        }

        throw new InvalidOperationException($"Central directory entry '{entryName}' not found.");
    }
}
