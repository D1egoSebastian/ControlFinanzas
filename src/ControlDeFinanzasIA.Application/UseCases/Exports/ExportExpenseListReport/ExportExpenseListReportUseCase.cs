using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Application.UseCases.Exports.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Transactions;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Exports.ExportExpenseListReport;

/// <summary>
/// Builds the fully computed expense-list report for a filter (all pages) and writes it through
/// the writer registered for the requested format (F-018, D-4).
/// Failure codes: <c>InvalidFilter</c>, <c>ReportWriterUnavailable</c>, <c>ReportDataFailed</c>,
/// <c>ReportRenderFailed</c>, <c>ReportSaveFailed</c>. Never throws except for cancellation.
/// </summary>
public sealed class ExportExpenseListReportUseCase
{
    private readonly IExpenseRepository _expenses;
    private readonly ICategoryRepository _categories;
    private readonly ICurrencyConverter _converter;
    private readonly IReadOnlyList<IReportWriter> _writers;
    private readonly IClock _clock;
    private readonly ILogger<ExportExpenseListReportUseCase> _logger;

    /// <summary>Creates the use case.</summary>
    public ExportExpenseListReportUseCase(
        IExpenseRepository expenses,
        ICategoryRepository categories,
        ICurrencyConverter converter,
        IEnumerable<IReportWriter> writers,
        IClock clock,
        ILogger<ExportExpenseListReportUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _expenses = expenses;
        _categories = categories;
        _converter = converter;
        _writers = writers.ToList();
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Exports the expenses matching the command's filter to <paramref name="target"/>.</summary>
    /// <returns>Success with an <see cref="ExportReportResult"/>, or a coded failure.</returns>
    public async Task<Result> ExecuteAsync(
        ExportExpenseListReportCommand command,
        SaveTarget target,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(command.Filter);

        IReportWriter? writer = ReportOutput.PickWriter(_writers, command.Format);
        if (writer is null)
        {
            return ReportOutput.WriterUnavailable(command.Format);
        }

        Result? invalid = Validate(command.Filter);
        if (invalid is not null)
        {
            return invalid;
        }

        ExpenseListReport report;
        try
        {
            report = await BuildReportAsync(command.Filter, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Building the expense-list report failed");
            return Result.Failure(ReportErrorCodes.DataFailed, "The report data could not be read.");
        }

        return await ReportOutput.RenderAndSaveAsync(
            stream => writer.WriteExpenseListAsync(report, command.Labels, stream, ct),
            command.Format,
            target,
            report.Lines.Count,
            report.HasMissingRates,
            _logger,
            ct).ConfigureAwait(false);
    }

    private static Result? Validate(ExpenseFilter filter)
    {
        if (filter.DateFrom.HasValue && filter.DateTo.HasValue && filter.DateFrom > filter.DateTo)
        {
            return Result.Failure("InvalidFilter", "DateFrom cannot be later than DateTo.");
        }

        if (filter.TagIds is not null && filter.TagIds.Any(id => id == Guid.Empty))
        {
            return Result.Failure("InvalidFilter", "TagIds cannot contain empty identifiers.");
        }

        return null;
    }

    private async Task<ExpenseListReport> BuildReportAsync(ExpenseFilter filter, CancellationToken ct)
    {
        // Sequential awaits: the repositories share one scoped DbContext.
        IReadOnlyList<Category> categories = await _categories.GetAllAsync(ct).ConfigureAwait(false);
        var names = categories.ToDictionary(c => c.Id, c => c.Name);

        IReadOnlyList<Expense> expenses = await ReportPaging.ReadAllAsync(
            (page, token) => _expenses.GetByFilterAsync(
                filter with { Page = page, PageSize = ReportPaging.PageSize },
                token),
            ct).ConfigureAwait(false);

        var builder = new ReportLineBuilder(_converter, names);
        var lines = new List<ReportTransactionLine>(expenses.Count);
        foreach (Expense expense in expenses)
        {
            lines.Add(await builder.BuildAsync(expense, ct).ConfigureAwait(false));
        }

        string? categoryName = filter.CategoryId is { } id && names.TryGetValue(id, out string? name) ? name : null;
        var summary = new ReportFilterSummary(
            filter.DateFrom,
            filter.DateTo,
            categoryName,
            filter.TagIds?.Count ?? 0);

        decimal totalDop = lines.Sum(l => l.AmountDop ?? 0m);
        bool hasMissingRates = lines.Any(l => l.AmountDop is null);

        return new ExpenseListReport(_clock.Today, summary, lines, totalDop, hasMissingRates);
    }
}
