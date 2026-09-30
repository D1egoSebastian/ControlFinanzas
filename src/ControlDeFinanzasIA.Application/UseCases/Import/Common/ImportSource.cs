namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>
/// The retained bytes of the picked file (F-020, D4). Preview and commit both run over these exact bytes.
/// </summary>
public sealed class ImportSource
{
    private readonly byte[] _content;

    /// <summary>Creates a source. Nothing is copied: the caller hands ownership of the array.</summary>
    /// <param name="displayName">File name shown to the user.</param>
    /// <param name="content">The file bytes.</param>
    public ImportSource(string displayName, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(content);
        DisplayName = displayName;
        _content = content;
    }

    /// <summary>Gets the file name shown to the user.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the file bytes.</summary>
    public ReadOnlyMemory<byte> Content => _content;
}
