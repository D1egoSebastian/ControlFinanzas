# Archive helpers for the packaging scripts (F-021 S9, DD-12).
#
# Why this module hand-writes the archives instead of using Compress-Archive or tar:
#  - Compress-Archive drops Unix modes and writes backslash entry names on Windows PowerShell 5.1.
#  - System.Formats.Tar needs .NET 7 (PowerShell 7.3+), while this machine only has Windows PowerShell 5.1.
# The small C# writer below runs on both Windows PowerShell 5.1 and PowerShell 7, produces
#  - zip files whose entries carry Unix permissions (external attributes + "made by Unix" host byte), and
#  - ustar .tar.gz files with explicit modes (0755 for the executables, 0644 for the rest).
# Only entries named in $ExecutableEntries (relative to the packaged folder) are marked executable.

Set-StrictMode -Version Latest

if (-not ('ControlDeFinanzasIA.Packaging.ArchiveTools' -as [type])) {
    $source = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ControlDeFinanzasIA.Packaging
{
    public static class ArchiveTools
    {
        private const int ExecutableMode = 493;   // 0755
        private const int RegularMode = 420;      // 0644
        private const int DirectoryMode = 493;    // 0755

        private static List<string> RelativeFiles(string sourceDir)
        {
            string root = Path.GetFullPath(sourceDir).TrimEnd('\\', '/');
            List<string> files = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                files.Add(file.Substring(root.Length + 1).Replace('\\', '/'));
            }
            files.Sort(StringComparer.Ordinal);
            return files;
        }

        private static string Join(string prefix, string relative)
        {
            if (string.IsNullOrEmpty(prefix)) { return relative; }
            return prefix.TrimEnd('/') + "/" + relative;
        }

        public static void WriteZip(string sourceDir, string zipPath, string entryPrefix, string[] executableEntries)
        {
            string root = Path.GetFullPath(sourceDir).TrimEnd('\\', '/');
            HashSet<string> executables = new HashSet<string>(executableEntries ?? new string[0], StringComparer.Ordinal);
            if (File.Exists(zipPath)) { File.Delete(zipPath); }

            using (FileStream stream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.ReadWrite))
            using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (string relative in RelativeFiles(sourceDir))
                {
                    ZipArchiveEntry entry = zip.CreateEntry(Join(entryPrefix, relative), CompressionLevel.Optimal);
                    int mode = executables.Contains(relative) ? ExecutableMode : RegularMode;
                    // 0x8000 = regular file; the Unix mode lives in the high 16 bits.
                    entry.ExternalAttributes = (0x8000 | mode) << 16;
                    using (Stream target = entry.Open())
                    using (FileStream input = File.OpenRead(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))))
                    {
                        input.CopyTo(target);
                    }
                }
            }

            MarkEntriesAsMadeOnUnix(zipPath);
        }

        // ZipArchive writes the "version made by" host byte of the machine it runs on (0 = DOS/Windows). Unzip
        // tools only honour the Unix mode in the external attributes when that byte says 3 (Unix), so patch the
        // central directory records in place.
        private static void MarkEntriesAsMadeOnUnix(string zipPath)
        {
            byte[] bytes = File.ReadAllBytes(zipPath);
            int eocd = -1;
            for (int i = bytes.Length - 22; i >= 0; i--)
            {
                if (bytes[i] == 0x50 && bytes[i + 1] == 0x4B && bytes[i + 2] == 0x05 && bytes[i + 3] == 0x06)
                {
                    eocd = i;
                    break;
                }
            }
            if (eocd < 0) { throw new InvalidDataException("End of central directory record not found."); }

            int count = BitConverter.ToUInt16(bytes, eocd + 10);
            int offset = (int)BitConverter.ToUInt32(bytes, eocd + 16);
            for (int n = 0; n < count; n++)
            {
                if (bytes[offset] != 0x50 || bytes[offset + 1] != 0x4B || bytes[offset + 2] != 0x01 || bytes[offset + 3] != 0x02)
                {
                    throw new InvalidDataException("Unexpected central directory signature at entry " + n);
                }
                bytes[offset + 5] = 3;
                int nameLength = BitConverter.ToUInt16(bytes, offset + 28);
                int extraLength = BitConverter.ToUInt16(bytes, offset + 30);
                int commentLength = BitConverter.ToUInt16(bytes, offset + 32);
                offset += 46 + nameLength + extraLength + commentLength;
            }

            File.WriteAllBytes(zipPath, bytes);
        }

        public static void WriteTarGz(string sourceDir, string tarGzPath, string entryPrefix, string[] executableEntries)
        {
            string root = Path.GetFullPath(sourceDir).TrimEnd('\\', '/');
            HashSet<string> executables = new HashSet<string>(executableEntries ?? new string[0], StringComparer.Ordinal);
            if (File.Exists(tarGzPath)) { File.Delete(tarGzPath); }

            List<string> files = RelativeFiles(sourceDir);
            SortedSet<string> directories = new SortedSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(entryPrefix)) { directories.Add(entryPrefix.TrimEnd('/')); }
            foreach (string relative in files)
            {
                int slash = relative.IndexOf('/');
                while (slash >= 0)
                {
                    directories.Add(Join(entryPrefix, relative.Substring(0, slash)));
                    slash = relative.IndexOf('/', slash + 1);
                }
            }

            using (FileStream stream = new FileStream(tarGzPath, FileMode.CreateNew, FileAccess.Write))
            using (GZipStream gzip = new GZipStream(stream, CompressionLevel.Optimal))
            {
                foreach (string directory in directories)
                {
                    WriteHeader(gzip, directory + "/", DirectoryMode, 0, DateTime.UtcNow, '5');
                }

                foreach (string relative in files)
                {
                    string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                    FileInfo info = new FileInfo(path);
                    int mode = executables.Contains(relative) ? ExecutableMode : RegularMode;
                    WriteHeader(gzip, Join(entryPrefix, relative), mode, info.Length, info.LastWriteTimeUtc, '0');
                    using (FileStream input = File.OpenRead(path))
                    {
                        input.CopyTo(gzip);
                    }
                    int padding = (int)((512 - (info.Length % 512)) % 512);
                    if (padding > 0) { gzip.Write(new byte[padding], 0, padding); }
                }

                gzip.Write(new byte[1024], 0, 1024);
            }
        }

        private static void WriteHeader(Stream output, string name, int mode, long size, DateTime modified, char type)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            string prefix = string.Empty;
            string shortName = name;

            if (nameBytes.Length > 100)
            {
                // ustar: split at a '/' so that name <= 100 and prefix <= 155 bytes; else GNU long-name entry.
                int split = -1;
                for (int i = name.Length - 1; i > 0; i--)
                {
                    if (name[i] != '/') { continue; }
                    string head = name.Substring(0, i);
                    string tail = name.Substring(i + 1);
                    if (Encoding.UTF8.GetByteCount(head) <= 155 && Encoding.UTF8.GetByteCount(tail) <= 100 && tail.Length > 0)
                    {
                        split = i;
                        break;
                    }
                }

                if (split > 0)
                {
                    prefix = name.Substring(0, split);
                    shortName = name.Substring(split + 1);
                }
                else
                {
                    byte[] longName = new byte[nameBytes.Length + 1];
                    Array.Copy(nameBytes, longName, nameBytes.Length);
                    WriteHeaderBlock(output, "././@LongLink", string.Empty, 0, longName.Length, DateTime.UtcNow, 'L');
                    output.Write(longName, 0, longName.Length);
                    int pad = (512 - (longName.Length % 512)) % 512;
                    if (pad > 0) { output.Write(new byte[pad], 0, pad); }
                    shortName = Encoding.UTF8.GetString(nameBytes, 0, 100);
                }
            }

            WriteHeaderBlock(output, shortName, prefix, mode, size, modified, type);
        }

        private static void WriteHeaderBlock(Stream output, string name, string prefix, int mode, long size, DateTime modified, char type)
        {
            byte[] block = new byte[512];
            PutString(block, 0, 100, name);
            PutOctal(block, 100, 8, mode);
            PutOctal(block, 108, 8, 0);
            PutOctal(block, 116, 8, 0);
            PutOctal(block, 124, 12, size);
            long seconds = (long)(modified.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            PutOctal(block, 136, 12, seconds < 0 ? 0 : seconds);
            for (int i = 148; i < 156; i++) { block[i] = 0x20; }
            block[156] = (byte)type;
            PutString(block, 257, 6, "ustar");
            block[262] = 0;
            block[263] = (byte)'0';
            block[264] = (byte)'0';
            PutString(block, 345, 155, prefix);

            long sum = 0;
            for (int i = 0; i < 512; i++) { sum += block[i]; }
            string checksum = Convert.ToString(sum, 8).PadLeft(6, '0');
            for (int i = 0; i < 6; i++) { block[148 + i] = (byte)checksum[i]; }
            block[154] = 0;
            block[155] = 0x20;

            output.Write(block, 0, 512);
        }

        private static void PutString(byte[] block, int offset, int length, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            Array.Copy(bytes, 0, block, offset, Math.Min(bytes.Length, length));
        }

        private static void PutOctal(byte[] block, int offset, int length, long value)
        {
            string text = Convert.ToString(value, 8).PadLeft(length - 1, '0');
            for (int i = 0; i < length - 1; i++) { block[offset + i] = (byte)text[i]; }
            block[offset + length - 1] = 0;
        }
    }
}
'@
    if ($PSEdition -eq 'Desktop') {
        # Windows PowerShell 5.1 (.NET Framework) needs the references spelled out; PowerShell 7 already has them.
        Add-Type -TypeDefinition $source -ReferencedAssemblies 'System.IO.Compression', 'System.IO.Compression.FileSystem' -Language CSharp
    }
    else {
        Add-Type -TypeDefinition $source -Language CSharp
    }
}

function Get-RepoRoot {
    $dir = Split-Path -Parent $PSScriptRoot
    return (Split-Path -Parent $dir)
}

function Get-AppVersion {
    <# Reads the one application version from Directory.Build.props (never typed in scripts). #>
    param([string]$RepoRoot = (Get-RepoRoot))
    [xml]$props = Get-Content -LiteralPath (Join-Path $RepoRoot 'Directory.Build.props') -Raw
    $node = $props.SelectSingleNode('//Version')
    if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
        throw 'No <Version> found in Directory.Build.props.'
    }
    return $node.InnerText.Trim()
}

function New-ZipArchive {
    param(
        [Parameter(Mandatory)][string]$SourceDir,
        [Parameter(Mandatory)][string]$DestinationPath,
        [string]$EntryPrefix = '',
        [string[]]$ExecutableEntries = @()
    )
    [ControlDeFinanzasIA.Packaging.ArchiveTools]::WriteZip($SourceDir, $DestinationPath, $EntryPrefix, $ExecutableEntries)
}

function New-TarGzArchive {
    param(
        [Parameter(Mandatory)][string]$SourceDir,
        [Parameter(Mandatory)][string]$DestinationPath,
        [string]$EntryPrefix = '',
        [string[]]$ExecutableEntries = @()
    )
    [ControlDeFinanzasIA.Packaging.ArchiveTools]::WriteTarGz($SourceDir, $DestinationPath, $EntryPrefix, $ExecutableEntries)
}

function Write-Sha256Sums {
    <# Writes SHA256SUMS.txt in `sha256sum` format ("<hex>  <name>", LF, no BOM) for the given files. #>
    param(
        [Parameter(Mandatory)][string[]]$Files,
        [Parameter(Mandatory)][string]$DestinationPath
    )
    $sb = New-Object System.Text.StringBuilder
    foreach ($file in ($Files | Sort-Object { [IO.Path]::GetFileName($_) })) {
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLowerInvariant()
        [void]$sb.Append($hash).Append('  ').Append([IO.Path]::GetFileName($file)).Append("`n")
    }
    [IO.File]::WriteAllText($DestinationPath, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
}

Export-ModuleMember -Function Get-RepoRoot, Get-AppVersion, New-ZipArchive, New-TarGzArchive, Write-Sha256Sums
