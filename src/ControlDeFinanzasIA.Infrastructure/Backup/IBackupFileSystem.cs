namespace ControlDeFinanzasIA.Infrastructure.Backup;

/// <summary>
/// Thin file-system seam used by <see cref="SqliteBackupService"/> so failure injection is possible
/// in tests (F-019, DD-3). Public because Infrastructure has no <c>InternalsVisibleTo</c>.
/// </summary>
public interface IBackupFileSystem
{
    /// <summary>Whether a file exists at <paramref name="path"/>.</summary>
    bool FileExists(string path);

    /// <summary>Length in bytes of the file at <paramref name="path"/>.</summary>
    long GetLength(string path);

    /// <summary>Free bytes on the volume that holds <paramref name="directory"/>.</summary>
    long GetAvailableFreeSpace(string directory);

    /// <summary>Opens an existing file for sequential reading.</summary>
    Stream OpenRead(string path);

    /// <summary>Creates or truncates a file for exclusive writing.</summary>
    Stream Create(string path);

    /// <summary>Atomically replaces <paramref name="destination"/> with <paramref name="source"/>, keeping the old file at <paramref name="backup"/>.</summary>
    void Replace(string source, string destination, string backup);

    /// <summary>Moves a file without overwriting.</summary>
    void Move(string source, string destination);

    /// <summary>Deletes a file; a missing file is not an error.</summary>
    void Delete(string path);

    /// <summary>Creates a directory and any missing parents.</summary>
    void CreateDirectory(string path);

    /// <summary>Deletes a directory recursively; a missing directory is not an error.</summary>
    void DeleteDirectory(string path);

    /// <summary>Enumerates the files of <paramref name="directory"/> that match <paramref name="pattern"/>.</summary>
    IEnumerable<string> EnumerateFiles(string directory, string pattern);
}
