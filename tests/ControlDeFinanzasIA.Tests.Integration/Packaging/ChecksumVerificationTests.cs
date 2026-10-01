using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ControlDeFinanzasIA.UI.Diagnostics;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Packaging;

/// <summary>
/// F-021 S9 (R-22, S-44, T13): the REAL archives and <c>SHA256SUMS.txt</c> in <c>artifacts/</c> produced by
/// <c>scripts/publish.ps1</c>: names, version, checksums and the Unix modes recorded inside the archives.
/// </summary>
[Trait("Category", "Packaging")]
public sealed class ChecksumVerificationTests
{
    private static string[] ExpectedArchiveNames(string version) =>
    [
        $"ControlDeFinanzasIA-{version}-win-x64.zip",
        $"ControlDeFinanzasIA-{version}-osx-arm64.tar.gz",
        $"ControlDeFinanzasIA-{version}-osx-x64.tar.gz",
        $"ControlDeFinanzasIA-{version}-linux-x64.tar.gz",
        $"ControlDeFinanzasIA-{version}-osx-arm64.app.zip",
        $"ControlDeFinanzasIA-{version}-osx-x64.app.zip",
    ];

    private static bool Ready(string path) => PackagingSupport.RequireArtifacts(path);

    [Fact]
    public void ArchiveNamesCarryTheAppVersionForEveryRid()
    {
        if (!Ready(PackagingSupport.Artifacts))
        {
            return;
        }

        Assert.Equal(PackagingSupport.Version(), AppVersionInfo.Current);
        foreach (string name in ExpectedArchiveNames(AppVersionInfo.Current))
        {
            Assert.True(File.Exists(Path.Combine(PackagingSupport.Artifacts, name)), "Missing archive: " + name);
        }

        string[] zipsAndTars = Directory.GetFiles(PackagingSupport.Artifacts)
            .Select(Path.GetFileName)
            .Where(n => n!.EndsWith(".zip", StringComparison.Ordinal) || n.EndsWith(".tar.gz", StringComparison.Ordinal))
            .ToArray()!;
        Assert.All(zipsAndTars, n => Assert.Matches(@"^ControlDeFinanzasIA-" + Regex.Escape(AppVersionInfo.Current) + @"-(win-x64|osx-arm64|osx-x64|linux-x64)(\.app)?\.(zip|tar\.gz)$", n));
    }

    [Fact]
    public void EveryLineOfSha256SumsVerifiesAgainstTheFileOnDisk()
    {
        string sums = Path.Combine(PackagingSupport.Artifacts, "SHA256SUMS.txt");
        if (!Ready(sums))
        {
            return;
        }

        string text = File.ReadAllText(sums);
        Assert.DoesNotContain('\r', text);
        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(ExpectedArchiveNames(AppVersionInfo.Current).Length, lines.Length);

        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            Match m = Regex.Match(line, @"^([0-9a-f]{64})  (\S+)$");
            Assert.True(m.Success, "Not in sha256sum format: " + line);

            string file = Path.Combine(PackagingSupport.Artifacts, m.Groups[2].Value);
            Assert.True(File.Exists(file), "Listed but missing: " + m.Groups[2].Value);
            string actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
            Assert.Equal(m.Groups[1].Value, actual);
            listed.Add(m.Groups[2].Value);
        }

        Assert.Equal(
            ExpectedArchiveNames(AppVersionInfo.Current).OrderBy(n => n, StringComparer.Ordinal),
            listed.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("osx-arm64")]
    [InlineData("osx-x64")]
    [InlineData("linux-x64")]
    public void TarArchivesKeepTheExecutableBitOnlyOnTheExecutables(string rid)
    {
        string path = Path.Combine(PackagingSupport.Artifacts, $"ControlDeFinanzasIA-{AppVersionInfo.Current}-{rid}.tar.gz");
        if (!Ready(path))
        {
            return;
        }

        Dictionary<string, int> modes = ReadTarModes(path);
        string root = $"ControlDeFinanzasIA-{AppVersionInfo.Current}-{rid}/";

        Assert.Equal(0b111_101_101, modes[root + "ControlDeFinanzasIA.UI"]);
        Assert.Equal(0b111_101_101, modes[root + "createdump"]);
        Assert.Equal(0b110_100_100, modes[root + "ControlDeFinanzasIA.UI.dll"]);
        Assert.Equal(0b111_101_101, modes[root]);
        Assert.All(
            modes.Where(kv => !kv.Key.EndsWith('/') && !kv.Key.EndsWith("/ControlDeFinanzasIA.UI", StringComparison.Ordinal) && !kv.Key.EndsWith("/createdump", StringComparison.Ordinal)),
            kv => Assert.Equal(0, kv.Value & 0b001_001_001));
    }

    [Theory]
    [InlineData("osx-arm64")]
    [InlineData("osx-x64")]
    public void AppZipRecordsUnixHostAndExecutableModeForTheBundleExecutable(string rid)
    {
        string path = Path.Combine(PackagingSupport.Artifacts, $"ControlDeFinanzasIA-{AppVersionInfo.Current}-{rid}.app.zip");
        if (!Ready(path))
        {
            return;
        }

        Dictionary<string, (byte Host, int Mode)> entries = ReadZipUnixModes(path);
        const string macos = "ControlDeFinanzasIA.app/Contents/MacOS/";

        Assert.True(entries.ContainsKey("ControlDeFinanzasIA.app/Contents/Info.plist"));
        Assert.True(entries.ContainsKey("ControlDeFinanzasIA.app/Contents/Resources/AppIcon.icns"));
        Assert.All(entries.Values, e => Assert.Equal(3, e.Host));
        Assert.Equal(0b111_101_101, entries[macos + "ControlDeFinanzasIA.UI"].Mode);
        Assert.Equal(0b111_101_101, entries[macos + "createdump"].Mode);
        Assert.Equal(0b110_100_100, entries[macos + "ControlDeFinanzasIA.UI.dll"].Mode);
        Assert.DoesNotContain(entries.Keys, k => k.Contains('\\'));
    }

    [Fact]
    public void WindowsZipUsesForwardSlashEntriesUnderTheVersionedFolder()
    {
        string path = Path.Combine(PackagingSupport.Artifacts, $"ControlDeFinanzasIA-{AppVersionInfo.Current}-win-x64.zip");
        if (!Ready(path))
        {
            return;
        }

        using ZipArchive zip = ZipFile.OpenRead(path);
        string root = $"ControlDeFinanzasIA-{AppVersionInfo.Current}-win-x64/";

        Assert.NotEmpty(zip.Entries);
        Assert.All(zip.Entries, e => Assert.StartsWith(root, e.FullName, StringComparison.Ordinal));
        Assert.Contains(zip.Entries, e => e.FullName == root + "ControlDeFinanzasIA.UI.exe");
        Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains('\\'));
    }

    private static Dictionary<string, (byte Host, int Mode)> ReadZipUnixModes(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        int eocd = -1;
        for (int i = bytes.Length - 22; i >= 0; i--)
        {
            if (bytes[i] == 0x50 && bytes[i + 1] == 0x4B && bytes[i + 2] == 0x05 && bytes[i + 3] == 0x06)
            {
                eocd = i;
                break;
            }
        }

        Assert.True(eocd >= 0, "End of central directory not found.");
        int count = BitConverter.ToUInt16(bytes, eocd + 10);
        int offset = (int)BitConverter.ToUInt32(bytes, eocd + 16);
        var result = new Dictionary<string, (byte, int)>();
        for (int n = 0; n < count; n++)
        {
            Assert.Equal(0x02014B50u, BitConverter.ToUInt32(bytes, offset));
            byte host = bytes[offset + 5];
            uint external = BitConverter.ToUInt32(bytes, offset + 38);
            int nameLength = BitConverter.ToUInt16(bytes, offset + 28);
            int extraLength = BitConverter.ToUInt16(bytes, offset + 30);
            int commentLength = BitConverter.ToUInt16(bytes, offset + 32);
            string name = Encoding.UTF8.GetString(bytes, offset + 46, nameLength);
            result[name] = (host, (int)((external >> 16) & 0x1FF));
            offset += 46 + nameLength + extraLength + commentLength;
        }

        return result;
    }

    private static Dictionary<string, int> ReadTarModes(string path)
    {
        using FileStream file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var buffer = new MemoryStream();
        gzip.CopyTo(buffer);
        byte[] tar = buffer.ToArray();

        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        int pos = 0;
        string? longName = null;
        while (pos + 512 <= tar.Length)
        {
            if (tar.Skip(pos).Take(512).All(b => b == 0))
            {
                break;
            }

            string name = ReadAscii(tar, pos, 100);
            string prefix = ReadAscii(tar, pos + 345, 155);
            int mode = Convert.ToInt32(ReadAscii(tar, pos + 100, 8).Trim(), 8);
            long size = Convert.ToInt64(ReadAscii(tar, pos + 124, 12).Trim(), 8);
            char type = (char)tar[pos + 156];

            // Header checksum: sum of all bytes with the checksum field read as spaces.
            long expected = Convert.ToInt64(ReadAscii(tar, pos + 148, 8).Trim(), 8);
            long sum = 0;
            for (int i = 0; i < 512; i++)
            {
                sum += i is >= 148 and < 156 ? 0x20 : tar[pos + i];
            }

            Assert.Equal(expected, sum);

            int dataBlocks = (int)((size + 511) / 512);
            if (type == 'L')
            {
                longName = ReadAscii(tar, pos + 512, (int)size);
            }
            else
            {
                string full = longName ?? (prefix.Length > 0 ? prefix + "/" + name : name);
                result[full] = mode;
                longName = null;
            }

            pos += 512 + (dataBlocks * 512);
        }

        return result;
    }

    private static string ReadAscii(byte[] data, int offset, int length)
    {
        int end = offset;
        while (end < offset + length && data[end] != 0)
        {
            end++;
        }

        return Encoding.UTF8.GetString(data, offset, end - offset);
    }
}
