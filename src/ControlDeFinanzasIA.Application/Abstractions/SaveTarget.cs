namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Library-free handle to the destination a user picked (F-018, D-1). The stream is opened
/// lazily so a failed render never creates or truncates the file.
/// </summary>
public sealed class SaveTarget : IDisposable
{
    private readonly Func<CancellationToken, Task<Stream>> _openWrite;
    private readonly IDisposable? _owner;
    private bool _disposed;

    /// <summary>Creates a target.</summary>
    /// <param name="displayName">File name shown to the user.</param>
    /// <param name="openWrite">Opens the destination for writing.</param>
    /// <param name="owner">Optional resource disposed together with this target (e.g. the storage file).</param>
    public SaveTarget(
        string displayName,
        Func<CancellationToken, Task<Stream>> openWrite,
        IDisposable? owner = null)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(openWrite);
        DisplayName = displayName;
        _openWrite = openWrite;
        _owner = owner;
    }

    /// <summary>File name shown to the user.</summary>
    public string DisplayName { get; }

    /// <summary>Opens the destination for writing.</summary>
    /// <exception cref="ObjectDisposedException">The target was already disposed.</exception>
    public Task<Stream> OpenWriteAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _openWrite(ct);
    }

    /// <summary>Disposes the owner exactly once.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _owner?.Dispose();
    }
}
