using ClosedXML.Excel;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>
/// Characterization of the pinned ClosedXML 0.105.1 behaviours the XLSX import reader (F-020, DD-10) relies on.
/// If a package upgrade changes one of these, the reader's conversion table must be revisited.
/// </summary>
public sealed class ClosedXmlBehaviorAssumptionsTests
{
    private static XLWorkbook RoundTrip(Action<IXLWorksheet> build)
    {
        using var source = new XLWorkbook();
        build(source.AddWorksheet("Sheet1"));
        var ms = new MemoryStream();
        source.SaveAs(ms);
        ms.Position = 0;
        return new XLWorkbook(ms);
    }

    [Theory]
    [InlineData("yyyy-mm-dd")]
    [InlineData("dd/mm/yyyy")]
    public void NumericCellWithDateFormat_ReportsDateTimeOnReload(string format)
    {
        using XLWorkbook wb = RoundTrip(ws =>
        {
            ws.Cell(1, 1).Value = 46000d; // a plain serial number, not assigned as a DateTime
            ws.Cell(1, 1).Style.DateFormat.Format = format;
        });

        IXLCell cell = wb.Worksheet(1).Cell(1, 1);

        Assert.Equal(XLDataType.DateTime, cell.DataType);
        Assert.Equal(new DateTime(2025, 12, 9), cell.GetDateTime());
    }

    [Fact]
    public void TrueDateTimeCell_ReportsDateTimeOnReload()
    {
        using XLWorkbook wb = RoundTrip(ws => ws.Cell(1, 1).Value = new DateTime(2026, 9, 15));

        Assert.Equal(XLDataType.DateTime, wb.Worksheet(1).Cell(1, 1).DataType);
    }

    [Fact]
    public void NumericCellWithMoneyFormat_ReportsNumber()
    {
        using XLWorkbook wb = RoundTrip(ws =>
        {
            ws.Cell(1, 1).Value = 12.5d;
            ws.Cell(1, 1).Style.NumberFormat.Format = "0.00";
        });

        Assert.Equal(XLDataType.Number, wb.Worksheet(1).Cell(1, 1).DataType);
    }

    [Fact]
    public void TextCellWithAtFormat_KeepsLeadingZeros()
    {
        using XLWorkbook wb = RoundTrip(ws =>
        {
            ws.Cell(1, 1).Value = "0123";
            ws.Cell(1, 1).Style.NumberFormat.Format = "@";
        });

        IXLCell cell = wb.Worksheet(1).Cell(1, 1);

        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.Equal("0123", cell.GetString());
    }

    [Fact]
    public void FormulaCell_ExposesCachedValueWithoutEvaluating()
    {
        // The file stores 41 while the formula 1+1 would evaluate to 2: reading 41 proves nothing is evaluated.
        byte[] bytes = XlsxHandBuilt.WithFormulaCell("1+1", "41");

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        IXLCell cell = wb.Worksheet(1).Cell(1, 1);

        Assert.True(cell.HasFormula);
        Assert.Equal(41d, cell.CachedValue.GetNumber());
    }

    [Fact]
    public void ErrorCell_ReportsErrorType()
    {
        byte[] bytes = XlsxHandBuilt.WithErrorCell("#N/A");

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        IXLCell cell = wb.Worksheet(1).Cell(1, 1);

        Assert.Equal(XLDataType.Error, cell.DataType);
        Assert.True(cell.Value.IsError);
        Assert.Equal(XLError.NoValueAvailable, cell.Value.GetError());
    }
}
