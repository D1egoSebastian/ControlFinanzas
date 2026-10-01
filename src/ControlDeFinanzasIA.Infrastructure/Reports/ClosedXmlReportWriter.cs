using System.Globalization;
using ClosedXML.Excel;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Domain.Budgets;

namespace ControlDeFinanzasIA.Infrastructure.Reports;

/// <summary>
/// Renders report DTOs to a two-sheet XLSX workbook with ClosedXML (F-018, D-6): a styled
/// "Summary" sheet with localized labels and a flat "Data" sheet whose sheet name and headers are
/// invariant English (stable contract for Phase 20). Stateless; every text cell is assigned as a
/// string value, never as a formula.
/// </summary>
public sealed class ClosedXmlReportWriter : IReportWriter
{
    /// <summary>Invariant name of the summary sheet.</summary>
    internal const string SummarySheetName = "Summary";

    /// <summary>Invariant name of the data sheet.</summary>
    internal const string DataSheetName = "Data";

    private const string _renderFailedCode = "ReportRenderFailed";
    private const string _moneyFormat = "#,##0.00";
    private const string _percentFormat = "0.0%";
    private const string _dateFormat = "yyyy-mm-dd";
    private const int _maxCellText = 32767;

    private static readonly string[] _dataHeaders =
        ["Date", "Type", "Description", "Category", "Amount", "Currency", "AmountDOP"];

    private static readonly double[] _dataWidths = [12, 10, 40, 24, 16, 10, 16];

    /// <inheritdoc />
    public ReportFormat Format => ReportFormat.Xlsx;

    /// <inheritdoc />
    public Task<Result> WriteMonthSummaryAsync(
        MonthSummaryReport report,
        ReportLabels labels,
        Stream destination,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(destination);

        return RenderAsync(
            workbook =>
            {
                BuildMonthSummarySheet(workbook.AddWorksheet(SummarySheetName), report, labels);
                BuildDataSheet(workbook.AddWorksheet(DataSheetName), report.Transactions);
            },
            destination,
            ct);
    }

    /// <inheritdoc />
    public Task<Result> WriteExpenseListAsync(
        ExpenseListReport report,
        ReportLabels labels,
        Stream destination,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(destination);

        return RenderAsync(
            workbook =>
            {
                BuildExpenseListSummarySheet(workbook.AddWorksheet(SummarySheetName), report, labels);
                BuildDataSheet(workbook.AddWorksheet(DataSheetName), report.Lines);
            },
            destination,
            ct);
    }

    private static async Task<Result> RenderAsync(Action<XLWorkbook> build, Stream destination, CancellationToken ct)
    {
        try
        {
            await Task.Run(
                () =>
                {
                    ct.ThrowIfCancellationRequested();
                    using var workbook = new XLWorkbook();
                    build(workbook);
                    workbook.SaveAs(destination);
                },
                ct).ConfigureAwait(false);
            return Result.Success(destination.CanSeek ? destination.Length : 0L);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result.Failure(_renderFailedCode, "The XLSX report could not be rendered.");
        }
    }

    private static void BuildMonthSummarySheet(IXLWorksheet ws, MonthSummaryReport report, ReportLabels labels)
    {
        CultureInfo culture = labels.Culture;
        MonthSummaryResult summary = report.Summary;
        int row = 1;

        row = WriteTitle(ws, row, labels.ReportMonthSummaryTitle, report.MonthAnchor.ToString("MMMM yyyy", culture), report.GeneratedOn, labels);

        row = WriteSection(ws, row, labels.ReportSummarySection);
        row = WriteSummaryLine(ws, row, labels.ReportExpensesLabel, summary.ExpensesDop, summary.ExpensesDeltaPercent, labels);
        row = WriteSummaryLine(ws, row, labels.ReportIncomesLabel, summary.IncomesDop, summary.IncomesDeltaPercent, labels);
        row = WriteSummaryLine(ws, row, labels.ReportNetBalanceLabel, summary.NetBalanceDop, null, labels);
        row++;

        row = WriteSection(ws, row, labels.ReportCategoriesSection);
        if (report.Categories.Count == 0)
        {
            SetText(ws.Cell(row++, 1), labels.ReportNoData).Style.Font.Italic = true;
        }
        else
        {
            WriteHeaderRow(ws, row++, labels.ReportCategoryColumn, labels.ReportAmountDopColumn, labels.ReportShareColumn);
            foreach (CategorySlice slice in report.Categories)
            {
                SetText(ws.Cell(row, 1), slice.Label);
                SetMoney(ws.Cell(row, 2), slice.AmountDop);
                if (report.CategoriesTotalDop != 0m)
                {
                    SetPercent(ws.Cell(row, 3), slice.AmountDop / report.CategoriesTotalDop);
                }

                row++;
            }

            SetText(ws.Cell(row, 1), labels.ReportTotalLabel).Style.Font.Bold = true;
            SetMoney(ws.Cell(row, 2), report.CategoriesTotalDop).Style.Font.Bold = true;
            row++;
        }

        row++;
        row = WriteSection(ws, row, labels.ReportBudgetsSection);
        BudgetVsActualResult budgets = report.Budgets;
        if (budgets.Lines.Count == 0)
        {
            SetText(ws.Cell(row++, 1), labels.ReportNoData).Style.Font.Italic = true;
        }
        else
        {
            WriteHeaderRow(
                ws,
                row++,
                labels.ReportCategoryColumn,
                labels.ReportBudgetColumn,
                labels.ReportActualColumn,
                labels.ReportPercentColumn,
                labels.ReportStatusColumn);
            foreach (BudgetVsActualLine line in budgets.Lines)
            {
                SetText(ws.Cell(row, 1), line.CategoryName);
                if (line.BudgetDop is { } budget)
                {
                    SetMoney(ws.Cell(row, 2), budget);
                }

                if (line.ActualDop is { } actual)
                {
                    SetMoney(ws.Cell(row, 3), actual);
                }

                if (line.PercentConsumed is { } percent)
                {
                    SetPercent(ws.Cell(row, 4), percent / 100m);
                }

                SetText(ws.Cell(row, 5), StatusText(line.Status, labels));
                row++;
            }

            SetText(ws.Cell(row, 1), labels.ReportTotalLabel).Style.Font.Bold = true;
            SetMoney(ws.Cell(row, 2), budgets.TotalBudgetDop).Style.Font.Bold = true;
            SetMoney(ws.Cell(row, 3), budgets.TotalActualDop).Style.Font.Bold = true;
            row++;
        }

        if (report.HasMissingRates)
        {
            row++;
            SetText(ws.Cell(row, 1), labels.ReportMissingRatesNote).Style.Font.FontColor = XLColor.FromHtml("#B23A2E");
        }

        SetSummaryWidths(ws);
    }

    private static void BuildExpenseListSummarySheet(IXLWorksheet ws, ExpenseListReport report, ReportLabels labels)
    {
        CultureInfo culture = labels.Culture;
        ReportFilterSummary filter = report.Filter;
        int row = 1;

        row = WriteTitle(ws, row, labels.ReportExpenseListTitle, null, report.GeneratedOn, labels);

        row = WriteSection(ws, row, labels.ReportFiltersLabel);
        bool anyFilter = false;
        if (filter.DateFrom is not null || filter.DateTo is not null)
        {
            anyFilter = true;
            string from = filter.DateFrom?.ToString("d", culture) ?? string.Empty;
            string to = filter.DateTo?.ToString("d", culture) ?? string.Empty;
            SetText(ws.Cell(row, 1), string.Format(culture, labels.ReportFilterPeriodFormat, from, to));
            row++;
        }

        if (!string.IsNullOrWhiteSpace(filter.CategoryName))
        {
            anyFilter = true;
            SetText(ws.Cell(row, 1), labels.ReportFilterCategoryLabel);
            SetText(ws.Cell(row, 2), filter.CategoryName);
            row++;
        }

        if (filter.TagCount > 0)
        {
            anyFilter = true;
            SetText(ws.Cell(row, 1), string.Format(culture, labels.ReportFilterTagsFormat, filter.TagCount));
            row++;
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            anyFilter = true;
            SetText(ws.Cell(row, 1), string.Format(culture, labels.ReportFilterTextFormat, filter.SearchText));
            row++;
        }

        if (!anyFilter)
        {
            SetText(ws.Cell(row++, 1), labels.ReportFilterAll);
        }

        row++;
        SetText(ws.Cell(row, 1), labels.ReportCountLabel).Style.Font.Bold = true;
        ws.Cell(row, 2).Value = report.Lines.Count;
        row++;
        SetText(ws.Cell(row, 1), labels.ReportTotalLabel).Style.Font.Bold = true;
        SetMoney(ws.Cell(row, 2), report.TotalDop).Style.Font.Bold = true;
        row++;

        if (report.HasMissingRates)
        {
            row++;
            SetText(ws.Cell(row, 1), labels.ReportMissingRatesNote).Style.Font.FontColor = XLColor.FromHtml("#B23A2E");
        }

        SetSummaryWidths(ws);
    }

    private static void BuildDataSheet(IXLWorksheet ws, IReadOnlyList<ReportTransactionLine> lines)
    {
        for (int c = 0; c < _dataHeaders.Length; c++)
        {
            IXLCell header = ws.Cell(1, c + 1);
            header.Value = _dataHeaders[c];
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F0F0");
            ws.Column(c + 1).Width = _dataWidths[c];
        }

        int row = 2;
        foreach (ReportTransactionLine line in lines)
        {
            IXLCell date = ws.Cell(row, 1);
            date.Value = line.Date.ToDateTime(TimeOnly.MinValue);
            date.Style.DateFormat.Format = _dateFormat;

            SetText(ws.Cell(row, 2), line.Kind == ReportTransactionKind.Income ? "Income" : "Expense");
            SetText(ws.Cell(row, 3), line.Description);
            SetText(ws.Cell(row, 4), line.CategoryName);
            SetMoney(ws.Cell(row, 5), line.Amount);
            SetText(ws.Cell(row, 6), line.Currency.ToString());
            if (line.AmountDop is { } dop)
            {
                SetMoney(ws.Cell(row, 7), dop);
            }

            row++;
        }

        ws.SheetView.FreezeRows(1);
    }

    private static int WriteTitle(IXLWorksheet ws, int row, string title, string? subtitle, DateOnly generatedOn, ReportLabels labels)
    {
        IXLCell titleCell = SetText(ws.Cell(row++, 1), title);
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 16;
        if (subtitle is not null)
        {
            SetText(ws.Cell(row++, 1), subtitle);
        }

        SetText(ws.Cell(row++, 1), string.Format(labels.Culture, labels.ReportGeneratedOnFormat, generatedOn.ToString("d", labels.Culture))).Style.Font.FontSize = 9;
        return row + 1;
    }

    private static int WriteSection(IXLWorksheet ws, int row, string title)
    {
        IXLCell cell = SetText(ws.Cell(row, 1), title);
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontSize = 13;
        return row + 1;
    }

    private static int WriteSummaryLine(IXLWorksheet ws, int row, string label, decimal amount, decimal? delta, ReportLabels labels)
    {
        SetText(ws.Cell(row, 1), label).Style.Font.Bold = true;
        SetMoney(ws.Cell(row, 2), amount);
        if (delta is { } d)
        {
            SetPercent(ws.Cell(row, 3), d / 100m);
            ws.Cell(row, 3).Style.NumberFormat.Format = "+0.0%;-0.0%;0.0%";
            SetText(ws.Cell(row, 4), labels.ReportDeltaLabel).Style.Font.Italic = true;
        }

        return row + 1;
    }

    private static void WriteHeaderRow(IXLWorksheet ws, int row, params string[] headers)
    {
        for (int c = 0; c < headers.Length; c++)
        {
            IXLCell cell = SetText(ws.Cell(row, c + 1), headers[c]);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F0F0");
        }
    }

    private static void SetSummaryWidths(IXLWorksheet ws)
    {
        ws.Column(1).Width = 34;
        ws.Column(2).Width = 18;
        ws.Column(3).Width = 18;
        ws.Column(4).Width = 18;
        ws.Column(5).Width = 18;
    }

    private static IXLCell SetText(IXLCell cell, string? value)
    {
        string text = value ?? string.Empty;
        if (text.Length > _maxCellText)
        {
            text = text[.._maxCellText];
        }

        // Assigning a string to Value stores a text cell; formulas are only created via FormulaA1.
        cell.Value = text;
        cell.Style.NumberFormat.Format = "@";
        return cell;
    }

    private static IXLCell SetMoney(IXLCell cell, decimal value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = _moneyFormat;
        return cell;
    }

    private static IXLCell SetPercent(IXLCell cell, decimal fraction)
    {
        cell.Value = fraction;
        cell.Style.NumberFormat.Format = _percentFormat;
        return cell;
    }

    private static string StatusText(BudgetStatus? status, ReportLabels labels) => status switch
    {
        BudgetStatus.Ok => labels.ReportStatusOk,
        BudgetStatus.Near => labels.ReportStatusNear,
        BudgetStatus.Exceeded => labels.ReportStatusExceeded,
        _ => labels.ReportStatusIndeterminate,
    };
}
