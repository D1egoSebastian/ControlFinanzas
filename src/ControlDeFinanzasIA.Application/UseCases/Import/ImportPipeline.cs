using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;

namespace ControlDeFinanzasIA.Application.UseCases.Import;

/// <summary>
/// The one pure pipeline Preview and Commit share (F-020, DD-11): read, map, parse every row, look up the
/// database once, plan. Both use cases pass the same retained bytes and the same <see cref="ImportOptions"/>,
/// so the same file, options and database always produce the same <see cref="ImportPlan"/> and plan hash.
/// Awaits are strictly sequential; nothing here writes.
/// </summary>
public sealed class ImportPipeline
{
    private const int _cancellationCheckInterval = 1024;

    private static readonly ImportField[] _required = [ImportField.Date, ImportField.Description, ImportField.Amount];

    private readonly ITabularReader _reader;
    private readonly IImportLookup _lookup;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="ImportPipeline"/> class.</summary>
    /// <param name="reader">Reads CSV and XLSX bytes into a typed table.</param>
    /// <param name="lookup">Loads the catalog and the stored transactions of the file's date span.</param>
    /// <param name="clock">Supplies today's date for the future-date warning.</param>
    public ImportPipeline(ITabularReader reader, IImportLookup lookup, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(clock);

        _reader = reader;
        _lookup = lookup;
        _clock = clock;
    }

    /// <summary>Builds the plan for a file.</summary>
    /// <param name="source">The retained file bytes.</param>
    /// <param name="options">The options; Commit must pass the instance the user previewed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with an <see cref="ImportPlan"/> value, or the file-level failure (<c>UnreadableFile</c>,
    /// <c>TooLarge</c>, <c>NoRows</c>, <c>MappingIncomplete</c>, <c>LookupFailed</c>).
    /// </returns>
    public async Task<Result> BuildAsync(ImportSource source, ImportOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        ct.ThrowIfCancellationRequested();

        Result read = await _reader.ReadAsync(source.Content, new TabularReadOptions(options.Delimiter), ct);

        if (!read.IsSuccess)
        {
            return read;
        }

        var table = (RawTable)read.Value!;
        ct.ThrowIfCancellationRequested();

        int headerCount = table.Headers.Count;
        HeaderMapping autoMapping = HeaderMapper.AutoMap(table.Headers);
        ColumnMapping mapping = options.Mapping ?? autoMapping.Mapping;

        if (options.Mapping is not null && !IsValidUserMapping(mapping, headerCount))
        {
            return Result.Failure(
                ImportErrorCodes.MappingIncomplete,
                "The column mapping points outside the file's columns or uses one column for two fields.");
        }

        // Blank rows (including Excel's all-empty ",,,," lines) are neither valid nor rejected: they do not exist.
        var rows = table.Rows.Where(row => !ImportRowValidator.IsBlank(row)).ToList();

        if (rows.Count == 0)
        {
            return Result.Failure(ImportErrorCodes.NoRows, "The file has no data rows.");
        }

        IReadOnlyList<ImportField> missing = _required.Where(field => !IsMapped(mapping, field)).ToList();
        IReadOnlyList<ImportField> conflicting = options.Mapping is null ? autoMapping.Conflicting : Array.Empty<ImportField>();

        // Evidence comes only from rows with the expected shape: a shifted row's cells are not what the mapping says.
        var shaped = rows.Where(row => ImportRowValidator.HasExpectedShape(row.Cells, headerCount)).ToList();
        DecimalStyleDetection detection = mapping.Amount is int amountIndex
            ? AmountParser.Detect(shaped.Select(row => row.Cells[amountIndex]))
            : new DecimalStyleDetection(DecimalStyle.Point, 0, 0, 0);
        DecimalStyle resolvedStyle = options.DecimalStyle == DecimalStyle.Auto ? detection.Resolved : options.DecimalStyle;
        detection = detection with { Resolved = resolvedStyle };
        DateEvidence dates = mapping.Date is int dateIndex
            ? DateParser.Detect(shaped.Select(row => row.Cells[dateIndex]), options.DateFormat)
            : DateEvidence.NotApplicable;

        if (!mapping.IsComplete)
        {
            return Result.Success(new ImportPlan(
                table,
                mapping,
                autoMapping,
                missing,
                conflicting,
                detection,
                dates,
                rows.Count,
                Array.Empty<PlannedRow>(),
                Array.Empty<NewCategoryPlan>(),
                ImportPlanner.EmptyPlanHash()));
        }

        DateOnly today = _clock.Today;
        var parsed = new List<ParsedRow>(rows.Count);

        for (int i = 0; i < rows.Count; i++)
        {
            if (i % _cancellationCheckInterval == 0)
            {
                ct.ThrowIfCancellationRequested();
            }

            parsed.Add(ImportRowValidator.Parse(rows[i], mapping, headerCount, options, resolvedStyle, today));
        }

        // Rejected rows never reach the duplicate check, so their dates must not widen the stored-row query.
        DateOnly[] dateSpan = parsed.Where(row => !row.IsRejected && row.Date is not null).Select(row => row.Date!.Value).ToArray();
        DateOnly? minDate = dateSpan.Length == 0 ? null : dateSpan.Min();
        DateOnly? maxDate = dateSpan.Length == 0 ? null : dateSpan.Max();

        Result lookup = await _lookup.LoadAsync(minDate, maxDate, ct);

        if (!lookup.IsSuccess)
        {
            return lookup;
        }

        var snapshot = (ImportLookupSnapshot)lookup.Value!;
        ct.ThrowIfCancellationRequested();

        PlannedRows planned = ImportPlanner.Plan(parsed, snapshot, options);

        return Result.Success(new ImportPlan(
            table,
            mapping,
            autoMapping,
            missing,
            conflicting,
            detection,
            dates,
            rows.Count,
            planned.Rows,
            planned.NewCategories,
            planned.PlanHash));
    }

    private static bool IsMapped(ColumnMapping mapping, ImportField field) => field switch
    {
        ImportField.Date => mapping.Date is not null,
        ImportField.Type => mapping.Type is not null,
        ImportField.Description => mapping.Description is not null,
        ImportField.Category => mapping.Category is not null,
        ImportField.Amount => mapping.Amount is not null,
        ImportField.Currency => mapping.Currency is not null,
        ImportField.PaymentMethod => mapping.PaymentMethod is not null,
        _ => false,
    };

    private static bool IsValidUserMapping(ColumnMapping mapping, int headerCount)
    {
        int[] indexes = new[] { mapping.Date, mapping.Type, mapping.Description, mapping.Category, mapping.Amount, mapping.Currency, mapping.PaymentMethod }
            .Where(index => index is not null)
            .Select(index => index!.Value)
            .ToArray();

        return indexes.All(index => index >= 0 && index < headerCount) && indexes.Distinct().Count() == indexes.Length;
    }
}
