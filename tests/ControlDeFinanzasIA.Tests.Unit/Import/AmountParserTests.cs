using System.Globalization;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Import;

/// <summary>Tests for <see cref="AmountParser"/> (F-020, R-11, R-12, S-10, C-1..C-4).</summary>
public sealed class AmountParserTests
{
    private static RawCell Text(string s) => new(s, RawCellKind.Text);

    private static decimal Dec(string s) => decimal.Parse(s, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

    [Theory]
    // Point style
    [InlineData("1234.56", DecimalStyle.Point, "1234.56", null, false)]
    [InlineData("1,234.56", DecimalStyle.Point, "1234.56", null, false)]
    [InlineData("1.234,56", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("(1,234.50)", DecimalStyle.Point, "-1234.50", null, false)]
    [InlineData("-50", DecimalStyle.Point, "-50", null, false)]
    [InlineData("+50", DecimalStyle.Point, "50", null, false)]
    [InlineData("1,234", DecimalStyle.Point, "1234", null, true)]
    [InlineData("1.234", DecimalStyle.Point, "1.234", null, true)]
    [InlineData("12,5", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("1.234.567", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("1 234,56", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("12,34,567", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("1 234.56", DecimalStyle.Point, "1234.56", null, false)]
    [InlineData("1 234.56", DecimalStyle.Point, "1234.56", null, false)]
    [InlineData("1 234.56", DecimalStyle.Point, "1234.56", null, false)]
    [InlineData("12.34567", DecimalStyle.Point, null, ImportReason.TooManyDecimals, false)]
    [InlineData("0", DecimalStyle.Point, "0", null, false)]
    [InlineData("-0.00", DecimalStyle.Point, "0", null, false)]
    [InlineData("0,123", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("0.123", DecimalStyle.Point, "0.123", null, false)]
    [InlineData("RD$ 1,500.00", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("$50", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("50 DOP", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("1e5", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("--3", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("12,3456789", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("50-", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("1234.", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData(".5", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("1  234", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("-(5)", DecimalStyle.Point, null, ImportReason.InvalidAmount, false)]
    [InlineData("99999999999999.9999", DecimalStyle.Point, "99999999999999.9999", null, false)]
    [InlineData("100000000000000.0000", DecimalStyle.Point, null, ImportReason.AmountTooLarge, false)]
    [InlineData("99999999999999999999999999999999", DecimalStyle.Point, null, ImportReason.AmountTooLarge, false)]
    [InlineData("", DecimalStyle.Point, null, ImportReason.MissingAmount, false)]
    [InlineData("   ", DecimalStyle.Point, null, ImportReason.MissingAmount, false)]
    // Comma style
    [InlineData("1234.56", DecimalStyle.Comma, null, ImportReason.InvalidAmount, false)]
    [InlineData("1,234.56", DecimalStyle.Comma, null, ImportReason.InvalidAmount, false)]
    [InlineData("1.234,56", DecimalStyle.Comma, "1234.56", null, false)]
    [InlineData("(45,00)", DecimalStyle.Comma, "-45.00", null, false)]
    [InlineData("1,234", DecimalStyle.Comma, "1.234", null, true)]
    [InlineData("1.234", DecimalStyle.Comma, "1234", null, true)]
    [InlineData("12,5", DecimalStyle.Comma, "12.5", null, false)]
    [InlineData("1.234.567", DecimalStyle.Comma, "1234567", null, false)]
    [InlineData("1 234,56", DecimalStyle.Comma, "1234.56", null, false)]
    [InlineData("1 234,56", DecimalStyle.Comma, "1234.56", null, false)]
    [InlineData("1 234,56", DecimalStyle.Comma, "1234.56", null, false)]
    [InlineData("12,34,567", DecimalStyle.Comma, null, ImportReason.InvalidAmount, false)]
    [InlineData("12,34567", DecimalStyle.Comma, null, ImportReason.TooManyDecimals, false)]
    [InlineData("12,3456789", DecimalStyle.Comma, null, ImportReason.TooManyDecimals, false)]
    [InlineData("-0,00", DecimalStyle.Comma, "0", null, false)]
    [InlineData("1e5", DecimalStyle.Comma, null, ImportReason.InvalidAmount, false)]
    [InlineData("RD$ 1.500,00", DecimalStyle.Comma, null, ImportReason.InvalidAmount, false)]
    public void Parse_TextCell_FollowsTheWorkedTable(
        string token,
        DecimalStyle style,
        string? expectedValue,
        ImportReason? expectedError,
        bool expectedAmbiguous)
    {
        AmountParseResult result = AmountParser.Parse(Text(token), style);

        Assert.Equal(expectedValue is null ? null : Dec(expectedValue), result.Value);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(expectedAmbiguous, result.WasAmbiguous);
    }

    [Fact]
    public void Parse_AmbiguousShape_IsNotFlaggedWhenTheUserChoseTheStyle()
    {
        AmountParseResult result = AmountParser.Parse(Text("1,234"), DecimalStyle.Point, styleWasAutoResolved: false);

        Assert.Equal(1234m, result.Value);
        Assert.False(result.WasAmbiguous);
    }

    [Fact]
    public void Parse_NumberCell_BypassesTheDecimalStyle()
    {
        var cell = new RawCell("1234.5", RawCellKind.Number);

        Assert.Equal(1234.5m, AmountParser.Parse(cell, DecimalStyle.Comma).Value);
        Assert.Equal(1234.5m, AmountParser.Parse(cell, DecimalStyle.Point).Value);
        Assert.False(AmountParser.Parse(cell, DecimalStyle.Point).WasAmbiguous);
    }

    [Fact]
    public void Parse_NumberCellAboveMax_IsAmountTooLarge()
    {
        var cell = new RawCell("100000000000000", RawCellKind.Number);

        Assert.Equal(ImportReason.AmountTooLarge, AmountParser.Parse(cell, DecimalStyle.Point).Error);
    }

    [Fact]
    public void Parse_ErrorCell_IsCellError()
    {
        var cell = new RawCell("#N/A", RawCellKind.Error);

        Assert.Equal(ImportReason.CellError, AmountParser.Parse(cell, DecimalStyle.Point).Error);
    }

    [Fact]
    public void Parse_DateCell_IsInvalidAmount()
    {
        var cell = new RawCell("2026-03-04", RawCellKind.Date);

        Assert.Equal(ImportReason.InvalidAmount, AmountParser.Parse(cell, DecimalStyle.Point).Error);
    }

    [Fact]
    public void Parse_AutoStyle_IsAProgrammerError() =>
        Assert.Throws<ArgumentException>(() => AmountParser.Parse(Text("1"), DecimalStyle.Auto));

    [Fact]
    public void Parse_Contradicting_Rejected()
    {
        AmountParseResult result = AmountParser.Parse(Text("1.234,56"), DecimalStyle.Point);

        Assert.Null(result.Value);
        Assert.Equal(ImportReason.InvalidAmount, result.Error);
    }

    [Fact]
    public void Parse_ResultValueIsDecimal() =>
        Assert.Equal(typeof(decimal?), typeof(AmountParseResult).GetProperty(nameof(AmountParseResult.Value))!.PropertyType);

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    [InlineData("de-DE")]
    public void Parse_And_Detect_IgnoreTheCurrentCulture(string cultureName)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);

            Assert.Equal(1234.56m, AmountParser.Parse(Text("1,234.56"), DecimalStyle.Point).Value);
            Assert.Equal(1234.56m, AmountParser.Parse(Text("1.234,56"), DecimalStyle.Comma).Value);
            Assert.Equal(DecimalStyle.Comma, AmountParser.Detect(new[] { Text("1.234,56") }).Resolved);
            Assert.Equal(DecimalStyle.Point, AmountParser.Detect(new[] { Text("1,234") }).Resolved);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("1,234.56", 1, 0, 0)]
    [InlineData("1.234,56", 0, 1, 0)]
    [InlineData("1.234.567", 0, 1, 0)]
    [InlineData("1,234,567", 1, 0, 0)]
    [InlineData("12,34,567", 1, 0, 0)]
    [InlineData("12,5", 0, 1, 0)]
    [InlineData("12.50", 1, 0, 0)]
    [InlineData("1234,5678", 0, 1, 0)]
    [InlineData("1234.5678", 1, 0, 0)]
    [InlineData("1,234", 0, 0, 1)]
    [InlineData("1.234", 0, 0, 1)]
    [InlineData("0,123", 0, 1, 0)]
    [InlineData("0.123", 1, 0, 0)]
    [InlineData("1 234,56", 0, 1, 0)]
    [InlineData("(1,234.50)", 1, 0, 0)]
    [InlineData("-1.234,56", 0, 1, 0)]
    [InlineData("1234", 0, 0, 0)]
    [InlineData("1 234", 0, 0, 0)]
    [InlineData("RD$ 1,500.00", 0, 0, 0)]
    [InlineData("", 0, 0, 0)]
    public void Detect_CountsEvidencePerTokenShape(string token, int point, int comma, int ambiguous)
    {
        DecimalStyleDetection detection = AmountParser.Detect(new[] { Text(token) });

        Assert.Equal(point, detection.PointEvidence);
        Assert.Equal(comma, detection.CommaEvidence);
        Assert.Equal(ambiguous, detection.Ambiguous);
    }

    [Fact]
    public void Detect_NoEvidence_ResolvesToPointAndIsNotApplicable()
    {
        DecimalStyleDetection detection = AmountParser.Detect(new[] { Text("100"), Text("50") });

        Assert.Equal(DecimalStyle.Point, detection.Resolved);
        Assert.False(detection.IsApplicable);
        Assert.False(detection.IsConflict);
    }

    [Fact]
    public void Detect_OnlyAmbiguous_ResolvesToPointAndIsApplicable()
    {
        DecimalStyleDetection detection = AmountParser.Detect(new[] { Text("1,234") });

        Assert.Equal(DecimalStyle.Point, detection.Resolved);
        Assert.True(detection.IsApplicable);
        Assert.Equal(1, detection.Ambiguous);
    }

    [Fact]
    public void Detect_MixedStyles_IsConflictAndMajorityWins()
    {
        DecimalStyleDetection detection = AmountParser.Detect(
            new[] { Text("1.234,56"), Text("2.000,00"), Text("1,234.56") });

        Assert.True(detection.IsConflict);
        Assert.Equal(DecimalStyle.Comma, detection.Resolved);
        Assert.Equal(2, detection.CommaEvidence);
        Assert.Equal(1, detection.PointEvidence);
    }

    [Fact]
    public void Detect_TiedConflict_ResolvesToPoint()
    {
        DecimalStyleDetection detection = AmountParser.Detect(new[] { Text("1.234,56"), Text("1,234.56") });

        Assert.True(detection.IsConflict);
        Assert.Equal(DecimalStyle.Point, detection.Resolved);
    }

    [Fact]
    public void Detect_IgnoresNonTextCells()
    {
        DecimalStyleDetection detection = AmountParser.Detect(new[]
        {
            new RawCell("1234.5", RawCellKind.Number),
            new RawCell("#N/A", RawCellKind.Error),
            new RawCell("2026-01-01", RawCellKind.Date),
        });

        Assert.False(detection.IsApplicable);
        Assert.Equal(0, detection.PointEvidence);
    }
}
