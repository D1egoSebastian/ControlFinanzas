using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>
/// Reads the picked file into memory with a hard cap (F-020, R-2, R-6, C-22). The size is never taken from
/// <see cref="Stream.Length"/>: picker streams can be non-seekable, so the copy itself is bounded.
/// </summary>
public static class ImportSourceLoader
{
    private const int _bufferSize = 81_920;

    /// <summary>Opens the target once, reads at most <see cref="ImportLimits.MaxFileBytes"/> + 1 bytes and disposes it.</summary>
    /// <param name="target">The picked file. The loader takes ownership and disposes it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with an <see cref="ImportSource"/> value, or an <c>UnreadableFile</c> / <c>TooLarge</c> failure.</returns>
    public static async Task<Result> LoadAsync(OpenTarget target, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        try
        {
            await using Stream stream = await target.OpenReadAsync(ct).ConfigureAwait(false);
            byte[] content = await ReadBoundedAsync(stream, ct).ConfigureAwait(false);

            if (content.Length > ImportLimits.MaxFileBytes)
            {
                return Result.Failure(
                    ImportErrorCodes.TooLarge,
                    "The file is larger than the import limit.",
                    ReasonDetails(ImportErrorCodes.ReasonFileTooLarge));
            }

            return Result.Success(new ImportSource(target.DisplayName, content));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure(
                ImportErrorCodes.UnreadableFile,
                "The file could not be read.",
                ReasonDetails(ex.GetType().Name));
        }
        finally
        {
            target.Dispose();
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken ct)
    {
        const int limit = ImportLimits.MaxFileBytes + 1;
        byte[] buffer = new byte[Math.Min(_bufferSize, limit)];
        using var collected = new MemoryStream();
        int total = 0;

        while (total < limit)
        {
            int wanted = Math.Min(buffer.Length, limit - total);
            int read = await stream.ReadAsync(buffer.AsMemory(0, wanted), ct).ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            collected.Write(buffer, 0, read);
            total += read;
        }

        return collected.ToArray();
    }

    private static Dictionary<string, string[]> ReasonDetails(string reason) =>
        new() { [ImportErrorCodes.DetailReason] = new[] { reason } };
}
