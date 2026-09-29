using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Exports.Common;

/// <summary>
/// Shared render-and-save plumbing of the export use cases (F-018, D-1, D-3). The report is
/// rendered into a <see cref="MemoryStream"/> first; the target file is opened and written only
/// after the render succeeded, so a failed render never creates or truncates it.
/// </summary>
internal static class ReportOutput
{
    /// <summary>Picks the writer registered for <paramref name="format"/>, or <c>null</c>.</summary>
    internal static IReportWriter? PickWriter(IEnumerable<IReportWriter> writers, ReportFormat format)
    {
        ArgumentNullException.ThrowIfNull(writers);
        return writers.FirstOrDefault(w => w.Format == format);
    }

    /// <summary>Failure returned when no writer supports the requested format.</summary>
    internal static Result WriterUnavailable(ReportFormat format) =>
        Result.Failure(ReportErrorCodes.WriterUnavailable, $"No report writer is registered for {format}.");

    /// <summary>
    /// Renders with <paramref name="render"/>, then copies the bytes to <paramref name="target"/>.
    /// </summary>
    /// <returns>Success carrying an <see cref="ExportReportResult"/>, or a coded failure.</returns>
    internal static async Task<Result> RenderAndSaveAsync(
        Func<Stream, Task<Result>> render,
        ReportFormat format,
        SaveTarget target,
        int rowCount,
        bool hasMissingRates,
        ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(logger);

        using var buffer = new MemoryStream();

        try
        {
            Result rendered = await render(buffer).ConfigureAwait(false);
            if (!rendered.IsSuccess)
            {
                logger.LogWarning(
                    "Report render failed for {Format}: {Code}",
                    format,
                    rendered.Error?.Code);
                return Result.Failure(
                    ReportErrorCodes.RenderFailed,
                    rendered.Error?.Message ?? "The report could not be rendered.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Report render threw for {Format}", format);
            return Result.Failure(ReportErrorCodes.RenderFailed, "The report could not be rendered.");
        }

        try
        {
            buffer.Position = 0;
            Stream destination = await target.OpenWriteAsync(ct).ConfigureAwait(false);
            await using (destination.ConfigureAwait(false))
            {
                await buffer.CopyToAsync(destination, ct).ConfigureAwait(false);
                if (destination.CanSeek)
                {
                    destination.SetLength(destination.Position);
                }

                await destination.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saving the {Format} report to the chosen target failed", format);
            return Result.Failure(ReportErrorCodes.SaveFailed, "The report could not be saved.");
        }

        return Result.Success(new ExportReportResult(format, target.DisplayName, rowCount, hasMissingRates));
    }
}
