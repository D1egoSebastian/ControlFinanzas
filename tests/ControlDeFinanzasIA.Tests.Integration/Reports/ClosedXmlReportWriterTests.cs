using ClosedXML.Excel;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Infrastructure.Reports;

namespace ControlDeFinanzasIA.Tests.Integration.Reports;

/// <summary>
/// Round-trip tests for <see cref="ClosedXmlReportWriter"/> (F-018, slice S3): the XLSX is read
/// back with ClosedXML and the "Data" contract (Phase 20) is asserted cell by cell.
/// </summary>
public sealed class ClosedXmlReportWriterTests
{
    private static readonly string[] _dataHeaders = ["Date", "Type", "Description", "Category", "Amount", "Currency", "AmountDOP"];

    private static async Task<XLWorkbook> RenderAsync(MonthSummaryReport report, ReportLabels labels)
    {
        var writer = new ClosedXmlReportWriter();
        var ms = new MemoryStream();
        Result result = await writer.WriteMonthSummaryAsync(report, labels, ms);
        Assert.True(result.IsSuccess, result.Error?.Message);
        ms.Position = 0;
        return new XLWorkbook(ms);
    }

    private static async Task<XLWorkbook> RenderListAsync(ExpenseListReport report, ReportLabels labels)
    {
        var writer = new ClosedXmlReportWriter();
        var ms = new MemoryStream();
        Result result = await writer.WriteExpenseListAsync(report, labels, ms);
        Assert.True(result.IsSuccess, result.Error?.Message);
        ms.Position = 0;
        return new XLWorkbook(ms);
    }

    private static string[] Headers(IXLWorksheet ws) =>
        _dataHeaders.Select((_, i) => ws.Cell(1, i + 1).GetString()).ToArray();

    private static IXLCell? Find(IXLWorksheet ws, string text, int occurrence = 1) =>
        ws.CellsUsed().Where(c => c.DataType == XLDataType.Text && c.GetString() == text).Skip(occurrence - 1).FirstOrDefault();

    private static IXLCell RightOf(IXLCell cell, int offset = 1) => cell.Worksheet.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber + offset);

    private static decimal Money(IXLCell cell) => Math.Round((decimal)cell.GetDouble(), 2);

    [Fact]
    public void Format_IsXlsx() => Assert.Equal(ReportFormat.Xlsx, new ClosedXmlReportWriter().Format);

    [Fact]
    public async Task MonthSummary_HasExactlyTwoInvariantSheetNames()
    {
        using XLWorkbook wb = await RenderAsync(ReportTestData.Report(), ReportTestData.EsLabels());

        Assert.Equal(new[] { "Summary", "Data" }, wb.Worksheets.Select(w => w.Name).ToArray());
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public async Task MonthSummary_DataHeaders_AreInvariantInBothLocales(string locale)
    {
        ReportLabels labels = locale == "es" ? ReportTestData.EsLabels() : ReportTestData.EnLabels();
        using XLWorkbook wb = await RenderAsync(ReportTestData.Report(), labels);

        Assert.Equal(_dataHeaders, Headers(wb.Worksheet("Data")));
    }

    [Fact]
    public async Task MonthSummary_DataRows_CarryExpensesAndIncomesWithTypedCells()
    {
        using XLWorkbook wb = await RenderAsync(ReportTestData.Report(transactionCount: 3), ReportTestData.EsLabels());
        IXLWorksheet ws = wb.Worksheet("Data");

        Assert.Equal(4, ws.LastRowUsed()!.RowNumber());
        Assert.Equal(XLDataType.DateTime, ws.Cell(2, 1).DataType);
        Assert.Equal(new DateTime(2026, 9, 1), ws.Cell(2, 1).GetDateTime());
        Assert.Equal("Expense", ws.Cell(2, 2).GetString());
        Assert.Equal("Income", ws.Cell(3, 2).GetString());
        Assert.Equal("Expense", ws.Cell(4, 2).GetString());
        Assert.Equal("Comida", ws.Cell(2, 4).GetString());
        Assert.Equal(XLDataType.Number, ws.Cell(2, 5).DataType);
        Assert.Equal(100m, Money(ws.Cell(2, 5)));
        Assert.Equal("USD", ws.Cell(2, 6).GetString());
        Assert.Equal("DOP", ws.Cell(3, 6).GetString());
        Assert.Equal(XLDataType.Number, ws.Cell(2, 7).DataType);
        Assert.Equal(6000m, Money(ws.Cell(2, 7)));
    }

    [Fact]
    public async Task MonthSummary_MissingRate_LeavesAmountDopBlank()
    {
        using XLWorkbook wb = await RenderAsync(ReportTestData.Report(hasMissingRates: true), ReportTestData.EsLabels());
        IXLWorksheet ws = wb.Worksheet("Data");

        Assert.True(ws.Cell(2, 7).IsEmpty());
        Assert.Equal(XLDataType.Number, ws.Cell(3, 7).DataType);
        Assert.Equal(100m, Money(ws.Cell(2, 5)));
    }

    [Fact]
    public async Task MonthSummary_SummarySheet_HoldsTotalsCategoriesAndBudgets()
    {
        using XLWorkbook wb = await RenderAsync(ReportTestData.Report(indeterminateBudget: true), ReportTestData.EsLabels());
        IXLWorksheet ws = wb.Worksheet("Summary");

        Assert.NotNull(Find(ws, "ES Month summary"));
        IXLCell expenses = Find(ws, "ES Expenses")!;
        Assert.Equal(XLDataType.Number, RightOf(expenses).DataType);
        Assert.Equal(17600m, Money(RightOf(expenses)));
        Assert.Equal(50000m, Money(RightOf(Find(ws, "ES Incomes")!)));
        Assert.Equal(32400m, Money(RightOf(Find(ws, "ES Net balance")!)));

        Assert.NotNull(Find(ws, "ES Categories"));
        Assert.Equal(12000m, Money(RightOf(Find(ws, "Comida")!)));
        Assert.Equal(5600m, Money(RightOf(Find(ws, "Transporte")!)));

        Assert.NotNull(Find(ws, "ES Budgets"));
        IXLCell viajes = Find(ws, "Viajes")!;
        Assert.True(RightOf(viajes).IsEmpty());
        Assert.Equal("ES Not available", RightOf(viajes, 4).GetString());
        Assert.Equal("ES Exceeded", RightOf(Find(ws, "Transporte", 2)!, 4).GetString());
    }

    [Fact]
    public async Task MonthSummary_MissingRatesNote_OnlyWhenFlagged()
    {
        using XLWorkbook flagged = await RenderAsync(ReportTestData.Report(hasMissingRates: true), ReportTestData.EsLabels());
        using XLWorkbook clean = await RenderAsync(ReportTestData.Report(), ReportTestData.EsLabels());
        const string note = "ES Some amounts have no exchange rate and are excluded.";

        Assert.NotNull(Find(flagged.Worksheet("Summary"), note));
        Assert.Null(Find(clean.Worksheet("Summary"), note));
    }

    [Fact]
    public async Task MonthSummary_Labels_ChangeSummaryText_ButNotDataHeaders()
    {
        using XLWorkbook es = await RenderAsync(ReportTestData.Report(), ReportTestData.EsLabels());
        using XLWorkbook en = await RenderAsync(ReportTestData.Report(), ReportTestData.EnLabels());

        Assert.NotNull(Find(es.Worksheet("Summary"), "ES Categories"));
        Assert.Null(Find(es.Worksheet("Summary"), "EN Categories"));
        Assert.NotNull(Find(en.Worksheet("Summary"), "EN Categories"));
        Assert.Equal(Headers(es.Worksheet("Data")), Headers(en.Worksheet("Data")));
    }

    [Fact]
    public async Task MonthSummary_Empty_StillHasTwoSheetsAndHeadersOnlyData()
    {
        using XLWorkbook wb = await RenderAsync(ReportTestData.Report(empty: true), ReportTestData.EnLabels());

        Assert.Equal(new[] { "Summary", "Data" }, wb.Worksheets.Select(w => w.Name).ToArray());
        IXLWorksheet ws = wb.Worksheet("Data");
        Assert.Equal(1, ws.LastRowUsed()!.RowNumber());
        Assert.Equal(_dataHeaders, Headers(ws));
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("=HYPERLINK(\"http://evil\",\"x\")")]
    [InlineData("+SUM(A1)")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    public async Task MonthSummary_DangerousDescription_IsStoredAsPlainText(string description)
    {
        using XLWorkbook wb = await RenderAsync(ReportTestData.Report(description: description), ReportTestData.EsLabels());
        IXLCell cell = wb.Worksheet("Data").Cell(2, 3);

        Assert.False(cell.HasFormula);
        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.Equal(description, cell.GetString());
    }

    [Fact]
    public async Task MonthSummary_DangerousCategoryName_IsStoredAsPlainText()
    {
        MonthSummaryReport report = ReportTestData.Report();
        report = report with { Transactions = [report.Transactions[0] with { CategoryName = "=1+1" }] };
        using XLWorkbook wb = await RenderAsync(report, ReportTestData.EsLabels());

        Assert.False(wb.Worksheet("Data").Cell(2, 4).HasFormula);
        Assert.Equal("=1+1", wb.Worksheet("Data").Cell(2, 4).GetString());
    }

    [Fact]
    public async Task MonthSummary_EmojiAndLongDescription_RoundTrips()
    {
        string description = "Cena ñ á € 🍕 " + new string('x', 5000);
        using XLWorkbook wb = await RenderAsync(ReportTestData.Report(description: description), ReportTestData.EsLabels());

        Assert.Equal(description, wb.Worksheet("Data").Cell(2, 3).GetString());
    }

    [Fact]
    public async Task MonthSummary_DescriptionOverExcelCellLimit_IsTruncatedNotFailed()
    {
        string description = new('y', 40000);
        using XLWorkbook wb = await RenderAsync(ReportTestData.Report(description: description), ReportTestData.EsLabels());

        Assert.Equal(32767, wb.Worksheet("Data").Cell(2, 3).GetString().Length);
    }

    [Fact]
    public async Task MonthSummary_NullReport_Throws()
    {
        var writer = new ClosedXmlReportWriter();
        await Assert.ThrowsAsync<ArgumentNullException>(() => writer.WriteMonthSummaryAsync(null!, ReportTestData.EsLabels(), new MemoryStream()));
    }

    [Fact]
    public async Task MonthSummary_UnwritableStream_ReturnsFailureNotException()
    {
        var writer = new ClosedXmlReportWriter();
        using var ms = new MemoryStream(Array.Empty<byte>(), writable: false);

        Result result = await writer.WriteMonthSummaryAsync(ReportTestData.Report(), ReportTestData.EsLabels(), ms);

        Assert.False(result.IsSuccess);
        Assert.Equal("ReportRenderFailed", result.Error!.Code);
    }

    private static ExpenseListReport ListReport(int count = 2, bool missingRate = false, string? searchText = null)
    {
        var lines = new List<ReportTransactionLine>();
        for (int i = 0; i < count; i++)
        {
            lines.Add(new ReportTransactionLine(
                new DateOnly(2026, 9, 1 + i),
                ReportTransactionKind.Expense,
                "Uber",
                "Transporte",
                50m + i,
                Currency.USD,
                missingRate && i == 0 ? null : 3000m + i));
        }

        return new ExpenseListReport(
            new DateOnly(2026, 9, 29),
            new ReportFilterSummary(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), "Transporte", 2, searchText),
            lines,
            lines.Sum(l => l.AmountDop ?? 0m),
            missingRate);
    }

    [Fact]
    public async Task ExpenseList_HasTwoSheets_AndOnlyExpensesInData()
    {
        using XLWorkbook wb = await RenderListAsync(ListReport(3), ReportTestData.EsLabels());
        IXLWorksheet ws = wb.Worksheet("Data");

        Assert.Equal(new[] { "Summary", "Data" }, wb.Worksheets.Select(w => w.Name).ToArray());
        Assert.Equal(_dataHeaders, Headers(ws));
        Assert.Equal(4, ws.LastRowUsed()!.RowNumber());
        Assert.All(Enumerable.Range(2, 3), r => Assert.Equal("Expense", ws.Cell(r, 2).GetString()));
        Assert.Equal(XLDataType.DateTime, ws.Cell(2, 1).DataType);
        Assert.Equal(XLDataType.Number, ws.Cell(2, 5).DataType);
    }

    [Fact]
    public async Task ExpenseList_SummarySheet_HasCountTotalFilterAndLocalizedLabels()
    {
        using XLWorkbook wb = await RenderListAsync(ListReport(2, missingRate: true), ReportTestData.EnLabels());
        IXLWorksheet ws = wb.Worksheet("Summary");

        Assert.NotNull(Find(ws, "EN Expense list"));
        Assert.Equal(2d, RightOf(Find(ws, "EN Count")!).GetDouble());
        Assert.Equal(3001m, Money(RightOf(Find(ws, "EN Total")!)));
        Assert.NotNull(Find(ws, "EN Filters"));
        Assert.NotNull(Find(ws, "Transporte"));
        Assert.NotNull(Find(ws, "EN Some amounts have no exchange rate and are excluded."));
        Assert.True(wb.Worksheet("Data").Cell(2, 7).IsEmpty());
    }

    [Fact]
    public async Task ExpenseList_SummarySheet_EchoesTheSearchText_OnlyWhenOneWasUsed()
    {
        using XLWorkbook withText = await RenderListAsync(ListReport(2, searchText: "pizza"), ReportTestData.EnLabels());
        using XLWorkbook without = await RenderListAsync(ListReport(2), ReportTestData.EnLabels());

        Assert.NotNull(Find(withText.Worksheet("Summary"), "EN text: pizza"));
        Assert.Null(Find(without.Worksheet("Summary"), "EN text: pizza"));
    }

    [Fact]
    public async Task ExpenseList_Empty_StillValid()
    {
        using XLWorkbook wb = await RenderListAsync(ListReport(0), ReportTestData.EsLabels());

        Assert.Equal(2, wb.Worksheets.Count);
        Assert.Equal(1, wb.Worksheet("Data").LastRowUsed()!.RowNumber());
    }
}
