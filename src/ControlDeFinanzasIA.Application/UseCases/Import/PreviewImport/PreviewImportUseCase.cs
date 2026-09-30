using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Import.PreviewImport;

/// <summary>
/// Dry run of an import (F-020, DD-12, R-18, R-20). Runs the shared <see cref="ImportPipeline"/> and summarises
/// the plan; it never writes. Row-level problems are part of a successful <see cref="ImportPreview"/>. Only
/// file-level problems (<c>UnreadableFile</c>, <c>TooLarge</c>, <c>NoRows</c>, <c>MappingIncomplete</c>) and
/// system failures (<c>LookupFailed</c>) are <see cref="Result"/> failures; cancellation is rethrown.
/// </summary>
public sealed class PreviewImportUseCase
{
    private readonly ImportPipeline _pipeline;
    private readonly ILogger<PreviewImportUseCase> _logger;

    /// <summary>Initializes a new instance of the <see cref="PreviewImportUseCase"/> class.</summary>
    /// <param name="pipeline">The pipeline shared with the commit use case.</param>
    /// <param name="logger">Logger for unexpected failures.</param>
    public PreviewImportUseCase(ImportPipeline pipeline, ILogger<PreviewImportUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(logger);

        _pipeline = pipeline;
        _logger = logger;
    }

    /// <summary>Previews an import.</summary>
    /// <param name="source">The retained file bytes.</param>
    /// <param name="options">The options the user has chosen; the same instance must be given to Commit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with an <see cref="ImportPreview"/> value, or a file-level failure.</returns>
    public async Task<Result> ExecuteAsync(ImportSource source, ImportOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            Result built = await _pipeline.BuildAsync(source, options, ct);

            if (!built.IsSuccess)
            {
                return built;
            }

            return Result.Success(Summarize((ImportPlan)built.Value!));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Previewing the import failed unexpectedly");
            return Result.Failure(ImportErrorCodes.LookupFailed, "The import preview could not be prepared.");
        }
    }

    private static ImportPreview Summarize(ImportPlan plan)
    {
        int toImport = 0;
        int duplicates = 0;
        int rejected = 0;
        int warnings = 0;
        var sample = new List<ImportRowResult>(ImportLimits.PreviewRows);
        var problems = new List<ImportRowResult>(ImportLimits.PreviewRows);
        var reasonCounts = new SortedDictionary<int, int>();

        foreach (PlannedRow row in plan.Rows)
        {
            switch (row.Status)
            {
                case ImportRowStatus.Valid:
                    toImport++;

                    if (row.Reasons.Any(reason => !ImportReasons.IsRejection(reason)))
                    {
                        warnings++;
                    }

                    break;
                case ImportRowStatus.Duplicate:
                    duplicates++;
                    break;
                default:
                    rejected++;
                    break;
            }

            foreach (ImportReason reason in row.Reasons)
            {
                reasonCounts[(int)reason] = reasonCounts.GetValueOrDefault((int)reason) + 1;
            }

            bool needsSample = sample.Count < ImportLimits.PreviewRows;
            bool needsProblem = problems.Count < ImportLimits.PreviewRows && (row.Status != ImportRowStatus.Valid || row.Reasons.Count > 0);

            if (needsSample || needsProblem)
            {
                ImportRowResult result = ToResult(row);

                if (needsSample)
                {
                    sample.Add(result);
                }

                if (needsProblem)
                {
                    problems.Add(result);
                }
            }
        }

        return new ImportPreview(
            plan.Table.Source,
            plan.Table.Headers,
            plan.Mapping,
            plan.MissingFields,
            plan.ConflictingFields,
            plan.Decimal,
            plan.Dates,
            plan.TotalRows,
            toImport,
            duplicates,
            rejected,
            warnings,
            sample,
            problems,
            reasonCounts.Select(pair => new ImportReasonCount((ImportReason)pair.Key, pair.Value)).ToList(),
            plan.NewCategories.Select(category => category.Name).ToList(),
            plan.PlanHash);
    }

    private static ImportRowResult ToResult(PlannedRow row) =>
        new(
            row.Parsed.RowNumber,
            row.Status,
            row.Parsed.Date,
            row.Parsed.Type,
            row.Parsed.Amount,
            row.Parsed.Currency,
            row.Parsed.Description,
            row.CategoryName,
            row.IsNewCategory,
            row.Reasons);
}
