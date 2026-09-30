using System.Globalization;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Import;

/// <summary>Tests for <see cref="DateParser"/> (F-020, R-13, S-11, C-6, C-7).</summary>
public sealed class DateParserTests
{
    private static RawCell Text(string s) => new(s, RawCellKind.Text);

    [Theory]
    [InlineData("2026-03-04")]
    [InlineData(" 2026-03-04 ")]
    [InlineData("2026-03-04T12:30:00")]
    [InlineData("2026-03-04T12:30:00Z")]
    [InlineData("2026-03-04T12:30:00+05:00")]
    [InlineData("2026-03-04T23:59:59.123-0400")]
    [InlineData("2026-03-04 12:30")]
    [InlineData("2026-03-04 12:30:15")]
    public void Parse_Iso_TakesTheDateAsWrittenForEveryFormat(string token)
    {
        foreach (ImportDateFormat format in Enum.GetValues<ImportDateFormat>())
        {
            DateParseResult result = DateParser.Parse(Text(token), format);

            Assert.Equal(new DateOnly(2026, 3, 4), result.Value);
            Assert.Null(result.Error);
        }
    }

    [Theory]
    [InlineData("2026-02-30")]
    [InlineData("2026-13-01")]
    [InlineData("2026-03-04X")]
    [InlineData("2026-03-04T25:00:00")]
    [InlineData("2026-03-04 junk")]
    public void Parse_IsoShapedButInvalid_IsInvalidDateWithNoFallThrough(string token)
    {
        DateParseResult result = DateParser.Parse(Text(token), ImportDateFormat.DayMonthYearDash);

        Assert.Null(result.Value);
        Assert.Equal(ImportReason.InvalidDate, result.Error);
    }

    [Theory]
    [InlineData("03/04/2026", ImportDateFormat.DayMonthYear, 2026, 4, 3)]
    [InlineData("03/04/2026", ImportDateFormat.MonthDayYear, 2026, 3, 4)]
    [InlineData("03-04-2026", ImportDateFormat.DayMonthYearDash, 2026, 4, 3)]
    [InlineData("3/4/2026", ImportDateFormat.DayMonthYear, 2026, 4, 3)]
    [InlineData("3/4/2026", ImportDateFormat.MonthDayYear, 2026, 3, 4)]
    [InlineData("3-4-2026", ImportDateFormat.DayMonthYearDash, 2026, 4, 3)]
    [InlineData("25/03/2026", ImportDateFormat.DayMonthYear, 2026, 3, 25)]
    [InlineData("03/25/2026", ImportDateFormat.MonthDayYear, 2026, 3, 25)]
    [InlineData("29/02/2028", ImportDateFormat.DayMonthYear, 2028, 2, 29)]
    public void Parse_UserFormat_ReadsDayAndMonthByTheSelectedOrder(
        string token, ImportDateFormat format, int y, int m, int d)
    {
        DateParseResult result = DateParser.Parse(Text(token), format);

        Assert.Equal(new DateOnly(y, m, d), result.Value);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("31/02/2026", ImportDateFormat.DayMonthYear)]
    [InlineData("13/13/2026", ImportDateFormat.DayMonthYear)]
    [InlineData("13/13/2026", ImportDateFormat.MonthDayYear)]
    [InlineData("29/02/2027", ImportDateFormat.DayMonthYear)]
    [InlineData("03/04/26", ImportDateFormat.DayMonthYear)]
    [InlineData("3/4/26", ImportDateFormat.MonthDayYear)]
    [InlineData("03/04/226", ImportDateFormat.DayMonthYear)]
    [InlineData("03-04-2026", ImportDateFormat.DayMonthYear)]
    [InlineData("03/04/2026", ImportDateFormat.DayMonthYearDash)]
    [InlineData("25/03/2026", ImportDateFormat.MonthDayYear)]
    [InlineData("March 4, 2026", ImportDateFormat.MonthDayYear)]
    [InlineData("hello", ImportDateFormat.DayMonthYear)]
    public void Parse_BadOrMismatchedText_IsInvalidDate(string token, ImportDateFormat format)
    {
        DateParseResult result = DateParser.Parse(Text(token), format);

        Assert.Null(result.Value);
        Assert.Equal(ImportReason.InvalidDate, result.Error);
    }

    [Fact]
    public void Parse_NumberCell_ExcelSerial_IsInvalidDate()
    {
        DateParseResult result = DateParser.Parse(new RawCell("46085", RawCellKind.Number), ImportDateFormat.DayMonthYear);

        Assert.Equal(ImportReason.InvalidDate, result.Error);
    }

    [Fact]
    public void Parse_DateCell_ParsesExactly()
    {
        DateParseResult result = DateParser.Parse(new RawCell("2026-03-04", RawCellKind.Date), ImportDateFormat.MonthDayYear);

        Assert.Equal(new DateOnly(2026, 3, 4), result.Value);
    }

    [Fact]
    public void Parse_MalformedDateCell_IsInvalidDate()
    {
        DateParseResult result = DateParser.Parse(new RawCell("03/04/2026", RawCellKind.Date), ImportDateFormat.MonthDayYear);

        Assert.Equal(ImportReason.InvalidDate, result.Error);
    }

    [Fact]
    public void Parse_ErrorCell_IsCellError()
    {
        DateParseResult result = DateParser.Parse(new RawCell("#REF!", RawCellKind.Error), ImportDateFormat.DayMonthYear);

        Assert.Equal(ImportReason.CellError, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Empty_IsMissingDate(string token)
    {
        DateParseResult result = DateParser.Parse(Text(token), ImportDateFormat.DayMonthYear);

        Assert.Equal(ImportReason.MissingDate, result.Error);
    }

    [Fact]
    public void Parse_YearBefore1900_StillParses_TheValidatorRejectsIt()
    {
        DateParseResult result = DateParser.Parse(Text("1899-12-31"), ImportDateFormat.DayMonthYear);

        Assert.Equal(new DateOnly(1899, 12, 31), result.Value);
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    [InlineData("ar-SA")]
    public void Parse_And_Detect_IgnoreTheCurrentCulture(string cultureName)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);

            Assert.Equal(new DateOnly(2026, 4, 3), DateParser.Parse(Text("03/04/2026"), ImportDateFormat.DayMonthYear).Value);
            Assert.Equal(new DateOnly(2026, 3, 4), DateParser.Parse(Text("2026-03-04"), ImportDateFormat.DayMonthYear).Value);
            Assert.Equal(
                DateEvidence.Contradicts,
                DateParser.Detect(new[] { Text("25/03/2026") }, ImportDateFormat.MonthDayYear));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Detect_DayFirstTokenUnderMonthFirstFormat_Contradicts()
    {
        Assert.Equal(
            DateEvidence.Contradicts,
            DateParser.Detect(new[] { Text("25/03/2026") }, ImportDateFormat.MonthDayYear));
    }

    [Fact]
    public void Detect_MonthFirstTokenUnderDayFirstFormat_Contradicts()
    {
        Assert.Equal(
            DateEvidence.Contradicts,
            DateParser.Detect(new[] { Text("03/25/2026") }, ImportDateFormat.DayMonthYear));
        Assert.Equal(
            DateEvidence.Contradicts,
            DateParser.Detect(new[] { Text("03-25-2026") }, ImportDateFormat.DayMonthYearDash));
    }

    [Fact]
    public void Detect_OneContradictionAmongConsistentTokens_StillContradicts()
    {
        Assert.Equal(
            DateEvidence.Contradicts,
            DateParser.Detect(new[] { Text("25/03/2026"), Text("03/25/2026") }, ImportDateFormat.DayMonthYear));
    }

    [Fact]
    public void Detect_OnlyBothAtMost12Tokens_IsAmbiguousOnly()
    {
        Assert.Equal(
            DateEvidence.AmbiguousOnly,
            DateParser.Detect(new[] { Text("03/04/2026"), Text("05/06/2026") }, ImportDateFormat.DayMonthYear));
    }

    [Fact]
    public void Detect_ConsistentDecisiveToken_IsConsistent()
    {
        Assert.Equal(
            DateEvidence.Consistent,
            DateParser.Detect(new[] { Text("25/03/2026"), Text("03/04/2026") }, ImportDateFormat.DayMonthYear));
        Assert.Equal(
            DateEvidence.Consistent,
            DateParser.Detect(new[] { Text("03/25/2026") }, ImportDateFormat.MonthDayYear));
    }

    [Fact]
    public void Detect_NoTextDates_IsNotApplicable()
    {
        Assert.Equal(
            DateEvidence.NotApplicable,
            DateParser.Detect(
                new[]
                {
                    Text("2026-03-04"),
                    Text(string.Empty),
                    new RawCell("2026-03-04", RawCellKind.Date),
                    new RawCell("46085", RawCellKind.Number),
                    Text("hello"),
                },
                ImportDateFormat.DayMonthYear));
    }
}
