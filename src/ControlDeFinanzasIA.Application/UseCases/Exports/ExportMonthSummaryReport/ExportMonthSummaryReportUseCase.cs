using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Chat;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Exports.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Exports.ExportMonthSummaryReport;

/// <summary>
/// Builds the fully computed month-summary report from the existing Dashboard queries and
/// writes it through the writer registered for the requested format (F-018, D-3).
/// Failure codes: <c>ReportWriterUnavailable</c>, <c>ReportDataFailed</c>,
/// <c>ReportRenderFailed</c>, <c>ReportSaveFailed</c>. Never throws except for cancellation.
/// </summary>
public sealed class ExportMonthSummaryReportUseCase
{
    private readonly GetMonthSummaryQuery _summary;
    private readonly GetCategoryBreakdownQuery _breakdown;
    private readonly GetBudgetVsActualQuery _budgets;
    private readonly IExpenseRepository _expenses;
    private readonly IIncomeRepository _incomes;
    private readonly ICategoryRepository _categories;
    private readonly ICurrencyConverter _converter;
    private readonly IReadOnlyList<IReportWriter> _writers;
    private readonly IClock _clock;
    private readonly ILogger<ExportMonthSummaryReportUseCase> _logger;

    /// <summary>Creates the use case.</summary>
    public ExportMonthSummaryReportUseCase(
        GetMonthSummaryQuery summary,
        GetCategoryBreakdownQuery breakdown,
        GetBudgetVsActualQuery budgets,
        IExpenseRepository expenses,
        IIncomeRepository incomes,
        ICategoryRepository categories,
        ICurrencyConverter converter,
        IEnumerable<IReportWriter> writers,
        IClock clock,
        ILogger<ExportMonthSummaryReportUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(breakdown);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _summary = summary;
        _breakdown = breakdown;
        _budgets = budgets;
        _expenses = expenses;
        _incomes = incomes;
        _categories = categories;
        _converter = converter;
        _writers = writers.ToList();
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Exports the month described by <paramref name="command"/> to <paramref name="target"/>.</summary>
    /// <returns>Success with an <see cref="ExportReportResult"/>, or a coded failure.</returns>
    public async Task<Result> ExecuteAsync(
        ExportMonthSummaryReportCommand command,
        SaveTarget target,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(target);

        IReportWriter? writer = ReportOutput.PickWriter(_writers, command.Format);
        if (writer is null)
        {
            return ReportOutput.WriterUnavailable(command.Format);
        }

        MonthSummaryReport report;
        try
        {
            report = await BuildReportAsync(command.MonthAnchor, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Building the month-summary report failed");
            return Result.Failure(ReportErrorCodes.DataFailed, "The report data could not be read.");
        }

        return await ReportOutput.RenderAndSaveAsync(
            stream => writer.WriteMonthSummaryAsync(report, command.Labels, stream, ct),
            command.Format,
            target,
            report.Transactions.Count,
            report.HasMissingRates,
            _logger,
            ct).ConfigureAwait(false);
    }

    private static T Unwrap<T>(Result result)
        where T : class
    {
        if (!result.IsSuccess || result.Value is not T value)
        {
            throw new InvalidOperationException(
                $"Query failed while building the report: {result.Error?.Code ?? "unexpected value"}.");
        }

        return value;
    }

    private async Task<MonthSummaryReport> BuildReportAsync(DateOnly monthAnchor, CancellationToken ct)
    {
        // Sequential awaits: the queries share one scoped DbContext.
        MonthSummaryResult summary = Unwrap<MonthSummaryResult>(await _summary.ExecuteAsync(monthAnchor, ct).ConfigureAwait(false));
        CategoryBreakdownResult breakdown = Unwrap<CategoryBreakdownResult>(
            await _breakdown.ExecuteAsync(monthAnchor, ChatContextLimits.UnfoldedTopCount, ct).ConfigureAwait(false));
        BudgetVsActualResult budgets = Unwrap<BudgetVsActualResult>(await _budgets.ExecuteAsync(monthAnchor, ct).ConfigureAwait(false));

        DateRange range = MonthPeriod.Range(monthAnchor);
        IReadOnlyList<Expense> expenses = await ReportPaging.ReadAllAsync(
            (page, token) => _expenses.GetByFilterAsync(
                new ExpenseFilter
                {
                    Page = page,
                    PageSize = ReportPaging.PageSize,
                    DateFrom = range.Start,
                    DateTo = range.End,
                },
                token),
            ct).ConfigureAwait(false);
        IReadOnlyList<Income> incomes = await ReportPaging.ReadAllAsync(
            (page, token) => _incomes.GetByFilterAsync(
                new IncomeFilter
                {
                    Page = page,
                    PageSize = ReportPaging.PageSize,
                    DateFrom = range.Start,
                    DateTo = range.End,
                },
                token),
            ct).ConfigureAwait(false);

        IReadOnlyList<Category> categories = await _categories.GetAllAsync(ct).ConfigureAwait(false);
        var names = categories.ToDictionary(c => c.Id, c => c.Name);

        var builder = new ReportLineBuilder(_converter, names);
        var lines = new List<ReportTransactionLine>(expenses.Count + incomes.Count);
        foreach (Expense expense in expenses)
        {
            lines.Add(await builder.BuildAsync(expense, ct).ConfigureAwait(false));
        }

        foreach (Income income in incomes)
        {
            lines.Add(await builder.BuildAsync(income, ct).ConfigureAwait(false));
        }

        // OrderBy is stable and Expense < Income, so ties on date keep expenses first.
        var ordered = lines
            .OrderBy(l => l.Date)
            .ThenBy(l => l.Kind)
            .ToList();

        bool hasMissingRates = summary.HasMissingRates
            || breakdown.HasMissingRates
            || budgets.HasMissingRates
            || ordered.Any(l => l.AmountDop is null);

        return new MonthSummaryReport(
            MonthPeriod.Anchor(monthAnchor),
            _clock.Today,
            summary,
            breakdown.Slices,
            breakdown.TotalDop,
            budgets,
            ordered,
            hasMissingRates);
    }
}
