using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Domain.Budgets;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ControlDeFinanzasIA.Infrastructure.Reports;

/// <summary>
/// Renders report DTOs to PDF with QuestPDF (F-018, D-6). Stateless; all text and number
/// formatting comes from <see cref="ReportLabels"/> and its culture, never from resources or
/// the ambient culture.
/// </summary>
public sealed class QuestPdfReportWriter : IReportWriter
{
    private const string _emDash = "—";
    private const string _renderFailedCode = "ReportRenderFailed";
    private const string _headerColor = "#F0F0F0";
    private const string _borderColor = "#BDBDBD";
    private const string _errorColor = "#B23A2E";

    /// <summary>Initializes a new instance of the <see cref="QuestPdfReportWriter"/> class.</summary>
    public QuestPdfReportWriter()
    {
        // Idempotent: lets callers that construct the writer directly skip AddReportExporters().
        QuestPdfConfiguration.Apply();
    }

    /// <inheritdoc />
    public ReportFormat Format => ReportFormat.Pdf;

    /// <inheritdoc />
    public async Task<Result> WriteMonthSummaryAsync(
        MonthSummaryReport report,
        ReportLabels labels,
        Stream destination,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(destination);

        try
        {
            await Task.Run(
                () =>
                {
                    ct.ThrowIfCancellationRequested();
                    BuildMonthSummary(report, labels).GeneratePdf(destination);
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
            return Result.Failure(_renderFailedCode, "The PDF report could not be rendered.");
        }
    }

    /// <inheritdoc />
    public Task<Result> WriteExpenseListAsync(
        ExpenseListReport report,
        ReportLabels labels,
        Stream destination,
        CancellationToken ct = default) =>
        // Implemented in slice S5.
        Task.FromResult(Result.Failure(_renderFailedCode, "The PDF expense-list report is not implemented yet."));

    internal static Document BuildMonthSummary(MonthSummaryReport report, ReportLabels labels)
    {
        CultureInfo culture = labels.Culture;

        return Document.Create(container => container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(labels.ReportMonthSummaryTitle).FontSize(18).Bold();
                    col.Item().Text(report.MonthAnchor.ToString("MMMM yyyy", culture)).FontSize(12);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(14);
                    col.Item().Element(c => ComposeSummary(c, report, labels));
                    col.Item().Element(c => ComposeCategories(c, report, labels));
                    col.Item().Element(c => ComposeBudgets(c, report, labels));
                });

                page.Footer().Column(col =>
                {
                    if (report.HasMissingRates)
                    {
                        col.Item().Text(labels.ReportMissingRatesNote).FontSize(8).FontColor(_errorColor);
                    }

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(
                            string.Format(culture, labels.ReportGeneratedOnFormat, report.GeneratedOn.ToString("d", culture)))
                            .FontSize(8);
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(8));
                            text.Span(labels.ReportPageLabel + " ");
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                    });
                });
            }));
    }

    internal static string FormatMoney(decimal value, CultureInfo culture) =>
        value.ToString("N2", culture) + " DOP";

    internal static string FormatPercent(decimal value, CultureInfo culture) =>
        value.ToString("0.0", culture) + "%";

    private static void ComposeSummary(IContainer container, MonthSummaryReport report, ReportLabels labels)
    {
        Application.UseCases.Dashboard.GetMonthSummary.MonthSummaryResult summary = report.Summary;

        container.Column(col =>
        {
            col.Item().PaddingBottom(4).Text(labels.ReportSummarySection).FontSize(13).Bold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(3);
                    c.RelativeColumn(3);
                });

                SummaryRow(table, labels.ReportExpensesLabel, summary.ExpensesDop, summary.ExpensesDeltaPercent, labels);
                SummaryRow(table, labels.ReportIncomesLabel, summary.IncomesDop, summary.IncomesDeltaPercent, labels);
                SummaryRow(table, labels.ReportNetBalanceLabel, summary.NetBalanceDop, null, labels);
            });
        });

        static void SummaryRow(TableDescriptor table, string label, decimal amount, decimal? delta, ReportLabels labels)
        {
            CultureInfo culture = labels.Culture;
            table.Cell().Element(BodyCell).Text(label).Bold();
            table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(amount, culture));
            table.Cell().Element(BodyCell).AlignRight().Text(
                delta is null
                    ? string.Empty
                    : labels.ReportDeltaLabel + ": " + (delta.Value > 0 ? "+" : string.Empty) + FormatPercent(delta.Value, culture));
        }

    }

    private static void ComposeCategories(IContainer container, MonthSummaryReport report, ReportLabels labels)
    {
        CultureInfo culture = labels.Culture;

        container.Column(col =>
        {
            col.Item().PaddingBottom(4).Text(labels.ReportCategoriesSection).FontSize(13).Bold();

            if (report.Categories.Count == 0)
            {
                col.Item().Text(labels.ReportNoData).Italic();
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(4);
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeaderCell).Text(labels.ReportCategoryColumn).Bold();
                    h.Cell().Element(HeaderCell).AlignRight().Text(labels.ReportAmountDopColumn).Bold();
                    h.Cell().Element(HeaderCell).AlignRight().Text(labels.ReportShareColumn).Bold();
                });

                foreach (CategorySlice slice in report.Categories)
                {
                    decimal share = report.CategoriesTotalDop == 0m ? 0m : slice.AmountDop / report.CategoriesTotalDop * 100m;
                    table.Cell().Element(BodyCell).Text(slice.Label);
                    table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(slice.AmountDop, culture));
                    table.Cell().Element(BodyCell).AlignRight().Text(FormatPercent(share, culture));
                }

                table.Cell().Element(BodyCell).Text(labels.ReportTotalLabel).Bold();
                table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(report.CategoriesTotalDop, culture)).Bold();
                table.Cell().Element(BodyCell).Text(string.Empty);
            });
        });
    }

    private static void ComposeBudgets(IContainer container, MonthSummaryReport report, ReportLabels labels)
    {
        CultureInfo culture = labels.Culture;
        BudgetVsActualResult budgets = report.Budgets;

        container.Column(col =>
        {
            col.Item().PaddingBottom(4).Text(labels.ReportBudgetsSection).FontSize(13).Bold();

            if (budgets.Lines.Count == 0)
            {
                col.Item().Text(labels.ReportNoData).Italic();
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(3);
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(3);
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeaderCell).Text(labels.ReportCategoryColumn).Bold();
                    h.Cell().Element(HeaderCell).AlignRight().Text(labels.ReportBudgetColumn).Bold();
                    h.Cell().Element(HeaderCell).AlignRight().Text(labels.ReportActualColumn).Bold();
                    h.Cell().Element(HeaderCell).AlignRight().Text(labels.ReportPercentColumn).Bold();
                    h.Cell().Element(HeaderCell).Text(labels.ReportStatusColumn).Bold();
                });

                foreach (BudgetVsActualLine line in budgets.Lines)
                {
                    table.Cell().Element(BodyCell).Text(line.CategoryName);
                    table.Cell().Element(BodyCell).AlignRight().Text(line.BudgetDop is { } b ? FormatMoney(b, culture) : _emDash);
                    table.Cell().Element(BodyCell).AlignRight().Text(line.ActualDop is { } a ? FormatMoney(a, culture) : _emDash);
                    table.Cell().Element(BodyCell).AlignRight().Text(line.PercentConsumed is { } p ? FormatPercent(p, culture) : _emDash);
                    table.Cell().Element(BodyCell).Text(StatusText(line.Status, labels));
                }

                table.Cell().Element(BodyCell).Text(labels.ReportTotalLabel).Bold();
                table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(budgets.TotalBudgetDop, culture)).Bold();
                table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(budgets.TotalActualDop, culture)).Bold();
                table.Cell().Element(BodyCell).Text(string.Empty);
                table.Cell().Element(BodyCell).Text(string.Empty);
            });
        });
    }

    private static string StatusText(BudgetStatus? status, ReportLabels labels) => status switch
    {
        BudgetStatus.Ok => labels.ReportStatusOk,
        BudgetStatus.Near => labels.ReportStatusNear,
        BudgetStatus.Exceeded => labels.ReportStatusExceeded,
        _ => labels.ReportStatusIndeterminate,
    };

    private static IContainer HeaderCell(IContainer c) =>
        c.Background(_headerColor).BorderBottom(1).BorderColor(_borderColor).Padding(4);

    private static IContainer BodyCell(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(_borderColor).Padding(4);
}
