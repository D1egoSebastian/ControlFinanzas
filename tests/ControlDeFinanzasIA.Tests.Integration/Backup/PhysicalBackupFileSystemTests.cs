using ControlDeFinanzasIA.Infrastructure.Backup;

namespace ControlDeFinanzasIA.Tests.Integration.Backup;

/// <summary>The physical file system wrapper does what <see cref="IBackupFileSystem"/> promises, on a temp directory.</summary>
public sealed class PhysicalBackupFileSystemTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"backup-fs-{Guid.NewGuid():N}");
    private readonly PhysicalBackupFileSystem _fs = new();

    public PhysicalBackupFileSystemTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void CreateWriteReadAndLength_RoundTripBytes()
    {
        string path = Path.Combine(_dir, "a.bin");

        using (Stream stream = _fs.Create(path))
        {
            stream.Write([1, 2, 3, 4]);
        }

        Assert.True(_fs.FileExists(path));
        Assert.Equal(4, _fs.GetLength(path));
        using Stream read = _fs.OpenRead(path);
        byte[] buffer = new byte[4];
        Assert.Equal(4, read.Read(buffer));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer);
    }

    [Fact]
    public void Create_TruncatesAnExistingFile()
    {
        string path = Path.Combine(_dir, "a.bin");
        File.WriteAllBytes(path, new byte[100]);

        using (_fs.Create(path))
        {
        }

        Assert.Equal(0, _fs.GetLength(path));
    }

    [Fact]
    public void Replace_SwapsTheContentAndKeepsTheOldFileAsBackup()
    {
        string source = Path.Combine(_dir, "new.bin");
        string destination = Path.Combine(_dir, "live.bin");
        string backup = Path.Combine(_dir, "live.bak");
        File.WriteAllText(source, "new");
        File.WriteAllText(destination, "old");

        _fs.Replace(source, destination, backup);

        Assert.Equal("new", File.ReadAllText(destination));
        Assert.Equal("old", File.ReadAllText(backup));
        Assert.False(File.Exists(source));
    }

    [Fact]
    public void Move_RenamesAndDoesNotOverwrite()
    {
        string source = Path.Combine(_dir, "s.bin");
        string target = Path.Combine(_dir, "t.bin");
        File.WriteAllText(source, "s");
        File.WriteAllText(Path.Combine(_dir, "existing.bin"), "e");

        _fs.Move(source, target);

        Assert.True(File.Exists(target));
        File.WriteAllText(source, "again");
        Assert.Throws<IOException>(() => _fs.Move(source, Path.Combine(_dir, "existing.bin")));
    }

    [Fact]
    public void Delete_RemovesAFileAndToleratesAMissingOne()
    {
        string path = Path.Combine(_dir, "d.bin");
        File.WriteAllText(path, "x");

        _fs.Delete(path);
        _fs.Delete(path);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void CreateDirectoryAndDeleteDirectory_HandleNestedAndMissingDirectories()
    {
        string nested = Path.Combine(_dir, "x", "y", "z");

        _fs.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "f.txt"), "f");
        _fs.DeleteDirectory(Path.Combine(_dir, "x"));
        _fs.DeleteDirectory(Path.Combine(_dir, "x"));

        Assert.False(Directory.Exists(Path.Combine(_dir, "x")));
    }

    [Fact]
    public void EnumerateFiles_FiltersByPattern()
    {
        File.WriteAllText(Path.Combine(_dir, "pre-restore-1.zip"), "1");
        File.WriteAllText(Path.Combine(_dir, "pre-restore-2.zip"), "2");
        File.WriteAllText(Path.Combine(_dir, "notes.txt"), "n");

        string[] names = _fs.EnumerateFiles(_dir, "pre-restore-*.zip").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;

        Assert.Equal(new[] { "pre-restore-1.zip", "pre-restore-2.zip" }, names);
    }

    [Fact]
    public void GetAvailableFreeSpace_ReturnsAPositiveNumberForTheTempVolume() => Assert.True(_fs.GetAvailableFreeSpace(_dir) > 0);
}
