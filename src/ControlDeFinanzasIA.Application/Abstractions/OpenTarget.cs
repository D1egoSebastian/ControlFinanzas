namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Library-free handle to the file a user picked for reading (F-019, DD-1). Mirrors
/// <see cref="SaveTarget"/>: the stream is opened lazily and can be opened more than once.
/// </summary>
public sealed class OpenTarget : IDisposable
{
    private readonly Func<CancellationToken, Task<Stream>> _openRead;
    private readonly IDisposable? _owner;
    private bool _disposed;

    /// <summary>Creates a target.</summary>
    /// <param name="displayName">File name shown to the user.</param>
    /// <param name="openRead">Opens the source for reading.</param>
    /// <param name="owner">Optional resource disposed together with this target (e.g. the storage file).</param>
    public OpenTarget(
        string displayName,
        Func<CancellationToken, Task<Stream>> openRead,
        IDisposable? owner = null)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(openRead);
        DisplayName = displayName;
        _openRead = openRead;
        _owner = owner;
    }

    /// <summary>File name shown to the user.</summary>
    public string DisplayName { get; }

    /// <summary>Opens the source for reading.</summary>
    /// <exception cref="ObjectDisposedException">The target was already disposed.</exception>
    public Task<Stream> OpenReadAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _openRead(ct);
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
