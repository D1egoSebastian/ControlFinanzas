namespace ControlDeFinanzasIA.Infrastructure.Backup;

/// <summary>One-to-one wrapper over <see cref="System.IO"/> and <see cref="DriveInfo"/> (F-019, DD-3).</summary>
public sealed class PhysicalBackupFileSystem : IBackupFileSystem
{
    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    public long GetLength(string path) => new FileInfo(path).Length;

    /// <inheritdoc />
    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

    /// <inheritdoc />
    public long GetAvailableFreeSpace(string directory)
    {
        string root = Path.GetPathRoot(Path.GetFullPath(directory))
            ?? throw new ArgumentException("The directory has no volume root.", nameof(directory));
        return new DriveInfo(root).AvailableFreeSpace;
    }

    /// <inheritdoc />
    public Stream OpenRead(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);

    /// <inheritdoc />
    public Stream Create(string path) =>
        new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

    /// <inheritdoc />
    public void Replace(string source, string destination, string backup) =>
        File.Replace(source, destination, backup, ignoreMetadataErrors: true);

    /// <inheritdoc />
    public void Move(string source, string destination) => File.Move(source, destination);

    /// <inheritdoc />
    public void Delete(string path) => File.Delete(path);

    /// <inheritdoc />
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    /// <inheritdoc />
    public void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFiles(string directory, string pattern) =>
        Directory.EnumerateFiles(directory, pattern);
}
