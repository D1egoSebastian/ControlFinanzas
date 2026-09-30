using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Turns the bytes of a CSV or Excel file into a typed table (F-020, DD-1). Implemented in Infrastructure.
/// </summary>
public interface ITabularReader
{
    /// <summary>Reads the whole content.</summary>
    /// <param name="content">The retained file bytes.</param>
    /// <param name="options">Reader options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with a <c>RawTable</c> value, or a file-level failure
    /// (<c>UnreadableFile</c>, <c>TooLarge</c>, <c>NoRows</c>) carrying <c>Details["reason"]</c> and <c>Details["line"]</c>.
    /// </returns>
    Task<Result> ReadAsync(ReadOnlyMemory<byte> content, TabularReadOptions options, CancellationToken ct = default);
}
