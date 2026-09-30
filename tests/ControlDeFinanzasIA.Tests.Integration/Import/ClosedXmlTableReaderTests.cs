using System.Diagnostics;
using System.Globalization;
using ClosedXML.Excel;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using ControlDeFinanzasIA.Infrastructure.Import;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>Tests of the REAL <see cref="ClosedXmlTableReader"/> over real workbook bytes (F-020, S3).</summary>
public sealed class ClosedXmlTableReaderTests
{
    private static readonly TabularReadOptions _options = new((char?)null);

    private static Task<Result> ReadAsync(byte[] bytes) => new ClosedXmlTableReader().ReadAsync(bytes, _options);

    private static async Task<RawTable> ReadTableAsync(byte[] bytes)
    {
        Result result = await ReadAsync(bytes);
        Assert.True(result.IsSuccess, result.Error?.Code + " " + result.Error?.Message);
        return (RawTable)result.Value!;
    }

    private static byte[] Workbook(Action<XLWorkbook> build)
    {
        using var wb = new XLWorkbook();
        build(wb);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Rows(IXLWorksheet ws, params string[][] rows)
    {
        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c < rows[r].Length; c++)
            {
                ws.Cell(r + 1, c + 1).Value = rows[r][c];
            }
        }
    }

    private static void AssertFailure(Result result, string code, string? reason)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Error!.Code);
        if (reason is not null)
        {
            Assert.Equal(reason, result.Error.Details![ImportErrorCodes.DetailReason][0]);
        }
    }

    [Fact]
    public async Task ExpenseListReport_RoundTripsHeadersRowsAndTypedCells()
    {
        RawTable table = await ReadTableAsync(await XlsxFixtures.ExpenseListAsync());

        Assert.Equal(XlsxFixtures.DataHeaders, table.Headers);
        Assert.Equal(ImportFileKind.Xlsx, table.Source.Kind);
        Assert.Equal("Data", table.Source.SheetName);
        Assert.Equal(3, table.Rows.Count);

        RawRow first = table.Rows[0];
        Assert.Equal(2, first.SourceRowNumber);
        Assert.Equal(new RawCell("2026-09-01", RawCellKind.Date), first.Cells[0]);
        Assert.Equal(new RawCell("Expense", RawCellKind.Text), first.Cells[1]);
        Assert.Equal(new RawCell("Supermercado", RawCellKind.Text), first.Cells[2]);
        Assert.Equal(new RawCell("Comida", RawCellKind.Text), first.Cells[3]);
        Assert.Equal(new RawCell("1234.5", RawCellKind.Number), first.Cells[4]);
        Assert.Equal(new RawCell("DOP", RawCellKind.Text), first.Cells[5]);
        Assert.Equal(new RawCell("1234.5", RawCellKind.Number), first.Cells[6]);

        RawRow emptyCategory = table.Rows[1];
        Assert.Equal(new RawCell(string.Empty, RawCellKind.Text), emptyCategory.Cells[3]);
        Assert.Equal(new RawCell("Cafe, con leche", RawCellKind.Text), emptyCategory.Cells[2]);
        Assert.Equal(new RawCell("USD", RawCellKind.Text), emptyCategory.Cells[5]);

        RawRow blankDop = table.Rows[2];
        Assert.Equal(new RawCell("12.3456", RawCellKind.Number), blankDop.Cells[4]);
        Assert.Equal(new RawCell(string.Empty, RawCellKind.Text), blankDop.Cells[6]);
        Assert.All(table.Rows, r => Assert.Equal(XlsxFixtures.DataHeaders.Length, r.Cells.Count));
    }

    [Fact]
    public async Task MonthSummaryReport_RoundTripsExpenseAndIncomeRows()
    {
        RawTable table = await ReadTableAsync(await XlsxFixtures.MonthSummaryAsync());

        Assert.Equal(XlsxFixtures.DataHeaders, table.Headers);
        Assert.Equal("Data", table.Source.SheetName);
        Assert.Equal(4, table.Rows.Count);
        Assert.Equal(new[] { "Expense", "Expense", "Income", "Expense" }, table.Rows.Select(r => r.Cells[1].Text).ToArray());
        Assert.All(table.Rows, r => Assert.Equal(RawCellKind.Date, r.Cells[0].Kind));
        Assert.All(table.Rows, r => Assert.Equal(RawCellKind.Number, r.Cells[4].Kind));
        Assert.Equal(new[] { "2026-09-01", "2026-09-02", "2026-09-03", "2026-09-04" }, table.Rows.Select(r => r.Cells[0].Text).ToArray());
        Assert.Equal(new[] { "1234.5", "4.5", "50000", "12.3456" }, table.Rows.Select(r => r.Cells[4].Text).ToArray());

        RawRow income = table.Rows[2];
        Assert.Equal(new RawCell("Salario", RawCellKind.Text), income.Cells[2]);
        Assert.Equal(new RawCell(string.Empty, RawCellKind.Text), income.Cells[3]);
        Assert.Equal(new RawCell(string.Empty, RawCellKind.Text), table.Rows[3].Cells[6]);
        Assert.Equal(RawCellKind.Number, table.Rows[0].Cells[6].Kind);
    }

    [Fact]
    public async Task ReportHeaders_AutoMapCompletelyWithAmountDopIgnored()
    {
        RawTable table = await ReadTableAsync(await XlsxFixtures.ExpenseListAsync());

        HeaderMapping mapping = HeaderMapper.AutoMap(table.Headers);

        Assert.Empty(mapping.Missing);
        Assert.Empty(mapping.Conflicting);
        Assert.Equal(new[] { 6 }, mapping.Ignored.ToArray());
        Assert.True(mapping.Mapping.IsComplete);
        Assert.Equal(4, mapping.Mapping.Amount);
        Assert.Equal(0, mapping.Mapping.Date);
        Assert.Equal(5, mapping.Mapping.Currency);
    }

    [Fact]
    public async Task Result_IsIndependentOfCurrentCulture()
    {
        byte[] bytes = await XlsxFixtures.MonthSummaryAsync();
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-DO");
            RawTable es = await ReadTableAsync(bytes);
            CultureInfo.CurrentCulture = new CultureInfo("en");
            RawTable en = await ReadTableAsync(bytes);

            Assert.Equal(es.Rows.Select(r => r.Cells.Select(c => c.Text).ToArray()), en.Rows.Select(r => r.Cells.Select(c => c.Text).ToArray()));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task SheetSelection_PrefersDataOverOthers()
    {
        byte[] bytes = Workbook(wb =>
        {
            Rows(wb.AddWorksheet("Other"), ["a", "b"], ["1", "2"]);
            Rows(wb.AddWorksheet("Data"), ["Date", "Amount"], ["x", "y"]);
        });

        RawTable table = await ReadTableAsync(bytes);

        Assert.Equal("Data", table.Source.SheetName);
        Assert.Equal(new[] { "Date", "Amount" }, table.Headers);
    }

    [Fact]
    public async Task SheetSelection_WithoutDataReadsFirstSheet()
    {
        byte[] bytes = Workbook(wb =>
        {
            Rows(wb.AddWorksheet("A"), ["first", "sheet"], ["1", "2"]);
            Rows(wb.AddWorksheet("B"), ["second", "sheet"], ["3", "4"]);
        });

        RawTable table = await ReadTableAsync(bytes);

        Assert.Equal("A", table.Source.SheetName);
        Assert.Equal(new[] { "first", "sheet" }, table.Headers);
    }

    [Fact]
    public async Task SheetSelection_IsCaseInsensitive()
    {
        byte[] bytes = Workbook(wb =>
        {
            Rows(wb.AddWorksheet("Other"), ["a", "b"], ["1", "2"]);
            Rows(wb.AddWorksheet("data"), ["c", "d"], ["3", "4"]);
        });

        RawTable table = await ReadTableAsync(bytes);

        Assert.Equal("data", table.Source.SheetName);
    }

    [Fact]
    public async Task TextCellWithLeadingZeros_StaysText()
    {
        byte[] bytes = Workbook(wb =>
        {
            IXLWorksheet ws = wb.AddWorksheet("Sheet1");
            ws.Cell(1, 1).Value = "Code";
            ws.Cell(2, 1).Value = "0123";
            ws.Cell(2, 1).Style.NumberFormat.Format = "@";
        });

        RawTable table = await ReadTableAsync(bytes);

        Assert.Equal(new RawCell("0123", RawCellKind.Text), table.Rows[0].Cells[0]);
    }

    [Fact]
    public async Task UserWorkbook_NumericCellWithDateFormat_IsDateKindAndPlainNumberIsNumber()
    {
        byte[] bytes = Workbook(wb =>
        {
            IXLWorksheet ws = wb.AddWorksheet("Sheet1");
            Rows(ws, ["When", "Trued", "Plain"]);
            ws.Cell(2, 1).Value = 46000d;
            ws.Cell(2, 1).Style.DateFormat.Format = "dd/mm/yyyy";
            ws.Cell(2, 2).Value = new DateTime(2026, 9, 15, 13, 45, 0);
            ws.Cell(2, 3).Value = 46000d;
        });

        RawTable table = await ReadTableAsync(bytes);

        Assert.Equal(new RawCell("2025-12-09", RawCellKind.Date), table.Rows[0].Cells[0]);
        Assert.Equal(new RawCell("2026-09-15", RawCellKind.Date), table.Rows[0].Cells[1]);
        Assert.Equal(new RawCell("46000", RawCellKind.Number), table.Rows[0].Cells[2]);
    }

    [Fact]
    public async Task FormulaCell_UsesCachedValueNeverEvaluates()
    {
        byte[] bytes = XlsxHandBuilt.Build(
            "Sheet1",
            "<row r=\"1\"><c r=\"A1\" t=\"str\"><v>Amount</v></c></row><row r=\"2\"><c r=\"A2\"><f>1+1</f><v>41</v></c></row>");

        RawTable table = await ReadTableAsync(bytes);

        Assert.Equal(new RawCell("41", RawCellKind.Number), table.Rows[0].Cells[0]);
    }

    [Theory]
    [InlineData("#N/A")]
    [InlineData("#DIV/0!")]
    [InlineData("#REF!")]
    public async Task ErrorCell_BecomesErrorKind(string error)
    {
        byte[] bytes = XlsxHandBuilt.Build(
            "Sheet1",
            "<row r=\"1\"><c r=\"A1\" t=\"str\"><v>Amount</v></c></row><row r=\"2\"><c r=\"A2\" t=\"e\"><v>" + error + "</v></c></row>");

        RawTable table = await ReadTableAsync(bytes);

        Assert.Equal(new RawCell(error, RawCellKind.Error), table.Rows[0].Cells[0]);
    }

    [Fact]
    public async Task HugeDouble_BecomesErrorCellInsteadOfThrowing()
    {
        byte[] bytes = XlsxHandBuilt.Build(
            "Sheet1",
            "<row r=\"1\"><c r=\"A1\" t=\"str\"><v>Amount</v></c></row><row r=\"2\"><c r=\"A2\"><v>1E308</v></c></row>");

        RawTable table = await ReadTableAsync(bytes);

        Assert.Equal(RawCellKind.Error, table.Rows[0].Cells[0].Kind);
    }

    [Fact]
    public async Task NumberIsRoundedToFourPlacesAwayFromZero()
    {
        byte[] bytes = Workbook(wb =>
        {
            IXLWorksheet ws = wb.AddWorksheet("Sheet1");
            ws.Cell(1, 1).Value = "Amount";
            ws.Cell(2, 1).Value = 1.23455d;
            ws.Cell(3, 1).Value = -0.00005d;
        });

        RawTable table = await ReadTableAsync(bytes);

        Assert.Equal("1.2346", table.Rows[0].Cells[0].Text);
        Assert.Equal("-0.0001", table.Rows[1].Cells[0].Text);
    }

    [Fact]
    public async Task HeaderRow_IsFirstNonBlankRowOfUsedRange()
    {
        byte[] bytes = Workbook(wb =>
        {
            IXLWorksheet ws = wb.AddWorksheet("Sheet1");
            ws.Cell(3, 2).Value = "Date";
            ws.Cell(3, 3).Value = "Amount";
            ws.Cell(4, 2).Value = "x";
            ws.Cell(4, 3).Value = "5";
            ws.Cell(6, 2).Value = "y";
            ws.Cell(6, 3).Value = "6";
        });

        RawTable table = await ReadTableAsync(bytes);

        Assert.Equal(new[] { "Date", "Amount" }, table.Headers);
        Assert.Equal(new[] { 4, 6 }, table.Rows.Select(r => r.SourceRowNumber).ToArray());
    }

    [Fact]
    public async Task EmptySheetAndHeaderOnly_AreNoRows()
    {
        byte[] empty = Workbook(wb => wb.AddWorksheet("Sheet1"));
        byte[] headerOnly = Workbook(wb => Rows(wb.AddWorksheet("Sheet1"), ["Date", "Amount"]));

        AssertFailure(await ReadAsync(empty), ImportErrorCodes.NoRows, null);
        AssertFailure(await ReadAsync(headerOnly), ImportErrorCodes.NoRows, null);
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public async Task ColumnLimit(int columns, bool ok)
    {
        byte[] bytes = Workbook(wb =>
        {
            IXLWorksheet ws = wb.AddWorksheet("Sheet1");
            for (int c = 1; c <= columns; c++)
            {
                ws.Cell(1, c).Value = "h" + c;
                ws.Cell(2, c).Value = "v" + c;
            }
        });

        Result result = await ReadAsync(bytes);

        if (ok)
        {
            Assert.True(result.IsSuccess);
        }
        else
        {
            AssertFailure(result, ImportErrorCodes.TooLarge, ImportErrorCodes.ReasonTooManyColumns);
        }
    }

    [Theory]
    [InlineData(50_000, true)]
    [InlineData(50_001, false)]
    public async Task RowLimit(int dataRows, bool ok)
    {
        byte[] bytes = Workbook(wb =>
        {
            IXLWorksheet ws = wb.AddWorksheet("Sheet1");
            ws.Cell(1, 1).Value = "Amount";
            for (int r = 0; r < dataRows; r++)
            {
                ws.Cell(r + 2, 1).Value = r + 1;
            }
        });

        Result result = await ReadAsync(bytes);

        if (ok)
        {
            Assert.True(result.IsSuccess);
            Assert.Equal(dataRows, ((RawTable)result.Value!).Rows.Count);
        }
        else
        {
            AssertFailure(result, ImportErrorCodes.TooLarge, ImportErrorCodes.ReasonTooManyRows);
        }
    }

    [Fact]
    public async Task LongestExcelCell_32767Chars_IsRead()
    {
        string value = new('a', 32_767);
        byte[] bytes = XlsxHandBuilt.Build(
            "Sheet1",
            "<row r=\"1\"><c r=\"A1\" t=\"str\"><v>Note</v></c></row><row r=\"2\"><c r=\"A2\" t=\"str\"><v>" + value + "</v></c></row>");

        RawTable table = await ReadTableAsync(bytes);

        Assert.Equal(32_767, table.Rows[0].Cells[0].Text.Length);
    }

    [Fact]
    public async Task OversizedCell_BeyondExcelLimit_IsRejectedNeverThrows()
    {
        // ClosedXML itself refuses text above the Excel maximum (32,767), so such a file is an unreadable workbook;
        // the reader's own 32,768 field check is a backstop for the other cell kinds.
        string value = new('a', 32_769);
        byte[] bytes = XlsxHandBuilt.Build(
            "Sheet1",
            "<row r=\"1\"><c r=\"A1\" t=\"str\"><v>Note</v></c></row><row r=\"2\"><c r=\"A2\" t=\"str\"><v>" + value + "</v></c></row>");

        Result result = await ReadAsync(bytes);

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.UnreadableFile, result.Error!.Code);
    }

    [Fact]
    public async Task HugeSparseUsedRange_DownTheRows_IsCheapAndReadsOnlyRowsWithContent()
    {
        byte[] bytes = XlsxHandBuilt.Build(
            "Sheet1",
            "<row r=\"1\"><c r=\"A1\" t=\"str\"><v>Amount</v></c></row><row r=\"1048576\"><c r=\"A1048576\" t=\"str\"><v>last</v></c></row>");
        var clock = Stopwatch.StartNew();

        RawTable table = await ReadTableAsync(bytes);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), "Sparse range took " + clock.Elapsed);
        Assert.Single(table.Rows);
        Assert.Equal(1_048_576, table.Rows[0].SourceRowNumber);
    }

    [Fact]
    public async Task HugeSparseUsedRange_AcrossColumns_IsTooManyColumnsBeforeMaterializing()
    {
        byte[] bytes = XlsxHandBuilt.Build(
            "Sheet1",
            "<row r=\"1\"><c r=\"A1\" t=\"str\"><v>Amount</v></c><c r=\"XFD1\" t=\"str\"><v>far</v></c></row>");
        var clock = Stopwatch.StartNew();

        Result result = await ReadAsync(bytes);

        AssertFailure(result, ImportErrorCodes.TooLarge, ImportErrorCodes.ReasonTooManyColumns);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), "Sparse range took " + clock.Elapsed);
    }

    [Fact]
    public async Task Cancelled_Token_Cancels()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ClosedXmlTableReader().ReadAsync(new byte[] { 1 }, _options, cts.Token));
    }

    [Fact]
    public async Task Content_LargerThanFileLimit_IsFileTooLarge()
    {
        byte[] bytes = new byte[ImportLimits.MaxFileBytes + 1];

        AssertFailure(await ReadAsync(bytes), ImportErrorCodes.TooLarge, ImportErrorCodes.ReasonFileTooLarge);
    }
}
