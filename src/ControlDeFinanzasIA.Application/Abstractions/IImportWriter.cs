using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Persists an import batch atomically (F-020, DD-1): everything is stored or nothing is.
/// Implemented in Infrastructure.
/// </summary>
public interface IImportWriter
{
    /// <summary>Writes the batch. Not cancellable once started (D25).</summary>
    /// <param name="batch">Already-built domain entities.</param>
    /// <param name="ct">Cancellation token (advisory).</param>
    /// <returns>Success with the number of rows written as an <c>int</c> value, or a <c>WriteFailed</c> failure.</returns>
    Task<Result> WriteAsync(ImportBatch batch, CancellationToken ct = default);
}
