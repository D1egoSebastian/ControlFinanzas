using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Import.CommitImport;

/// <summary>
/// Commits an import (F-020, DD-12, R-18..R-20). It never trusts the preview's numbers: it re-runs the shared
/// <see cref="ImportPipeline"/> over the retained bytes with the confirmed options, recomputes the plan and
/// refuses to write when its hash differs from the one the user saw (<c>PreviewStale</c>). Entities are built
/// by <see cref="ImportMaterializer"/> before the writer is called, so a row the domain refuses fails the whole
/// commit with nothing written. Cancellation is honored only until the write begins; from there the writer runs
/// with <see cref="CancellationToken.None"/> so the import is all or nothing. Awaits are strictly sequential.
/// </summary>
public sealed class CommitImportUseCase
{
    private readonly ImportPipeline _pipeline;
    private readonly IImportWriter _writer;
    private readonly IClock _clock;
    private readonly ILogger<CommitImportUseCase> _logger;

    /// <summary>Initializes a new instance of the <see cref="CommitImportUseCase"/> class.</summary>
    /// <param name="pipeline">The pipeline shared with the preview use case.</param>
    /// <param name="writer">The atomic writer.</param>
    /// <param name="clock">Supplies the instant stamped on created entities.</param>
    /// <param name="logger">Logger for unexpected failures.</param>
    public CommitImportUseCase(ImportPipeline pipeline, IImportWriter writer, IClock clock, ILogger<CommitImportUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _pipeline = pipeline;
        _writer = writer;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Commits the import the user previewed.</summary>
    /// <param name="source">The retained file bytes.</param>
    /// <param name="options">The exact options that were previewed.</param>
    /// <param name="expectedPlanHash">The plan hash shown by the preview.</param>
    /// <param name="ct">Cancellation token; honored only before the write begins.</param>
    /// <returns>
    /// Success with an <see cref="ImportCommitResult"/> (all zeros inserted when no row qualifies), or a failure:
    /// a file-level code, <c>MappingIncomplete</c>, <c>PreviewStale</c>, <c>LookupFailed</c> or <c>WriteFailed</c>.
    /// </returns>
    public async Task<Result> ExecuteAsync(ImportSource source, ImportOptions options, string expectedPlanHash, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(expectedPlanHash);

        ImportPlan plan;

        try
        {
            Result built = await _pipeline.BuildAsync(source, options, ct);

            if (!built.IsSuccess)
            {
                return built;
            }

            plan = (ImportPlan)built.Value!;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Preparing the import commit failed unexpectedly");
            return Result.Failure(ImportErrorCodes.LookupFailed, "The import could not be prepared. Nothing was written.");
        }

        if (!plan.Mapping.IsComplete)
        {
            return Result.Failure(ImportErrorCodes.MappingIncomplete, "The column mapping is incomplete.");
        }

        if (!string.Equals(plan.PlanHash, expectedPlanHash, StringComparison.Ordinal))
        {
            return Result.Failure(
                ImportErrorCodes.PreviewStale,
                "The file, the options or the database changed since the preview. Nothing was written.");
        }

        ImportBatch batch;

        try
        {
            batch = ImportMaterializer.Build(plan, _clock.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Building the import entities failed; nothing was written");
            return Result.Failure(ImportErrorCodes.WriteFailed, "A row could not be built. Nothing was written.");
        }

        int duplicates = plan.Rows.Count(row => row.Status == ImportRowStatus.Duplicate);
        int rejected = plan.Rows.Count(row => row.Status == ImportRowStatus.Rejected);

        if (batch.Expenses.Count + batch.Incomes.Count == 0)
        {
            return Result.Success(new ImportCommitResult(0, 0, 0, duplicates, rejected));
        }

        // The last chance to cancel: once the writer starts, the import completes or rolls back as a whole.
        ct.ThrowIfCancellationRequested();

        try
        {
            Result written = await _writer.WriteAsync(batch, CancellationToken.None);

            if (!written.IsSuccess)
            {
                return written;
            }

            return Result.Success(new ImportCommitResult(
                batch.Expenses.Count,
                batch.Incomes.Count,
                batch.NewCategories.Count,
                duplicates,
                rejected));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Writing the import failed unexpectedly");
            return Result.Failure(ImportErrorCodes.WriteFailed, "The import could not be saved. Nothing was written.");
        }
    }
}
