using System.Globalization;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using ControlDeFinanzasIA.Domain.Enums;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Import;

/// <summary>Tests for <see cref="ImportRowValidator"/> (F-020, R-10, R-11, S-12..S-14, C-9, C-12, C-13, C-15).</summary>
public sealed class ImportRowValidatorTests
{
    private static readonly DateOnly _today = new(2026, 9, 30);
    private static readonly ColumnMapping _full = new(0, 1, 2, 3, 4, 5, 6);

    private static ImportOptions Options(
        SignMode sign = SignMode.Unsigned,
        TransactionType defaultType = TransactionType.Expense,
        Currency defaultCurrency = Currency.DOP,
        DecimalStyle decimalStyle = DecimalStyle.Point,
        ImportDateFormat dateFormat = ImportDateFormat.DayMonthYear) =>
        new(null, null, decimalStyle, dateFormat, defaultType, defaultCurrency, sign);

    private static RawRow Row(params string[] cells) =>
        new(2, cells.Select(c => new RawCell(c, RawCellKind.Text)).ToList());

    private static RawRow RowWith(params RawCell[] cells) => new(2, cells);

    /// <summary>A fully valid row: Date, Type, Description, Category, Amount, Currency, PaymentMethod.</summary>
    private static string[] Good() => new[] { "2026-03-04", "Gasto", "Coffee", "Comida", "50.00", "DOP", "Efectivo" };

    private static ParsedRow Parse(
        RawRow row,
        ColumnMapping? map = null,
        ImportOptions? options = null,
        int headerCount = 7,
        DecimalStyle style = DecimalStyle.Point) =>
        ImportRowValidator.Parse(row, map ?? _full, headerCount, options ?? Options(), style, _today);

    private static string[] With(int index, string value)
    {
        string[] cells = Good();
        cells[index] = value;
        return cells;
    }

    [Fact]
    public void Parse_ValidRow_ProducesEveryFieldAndNoReasons()
    {
        ParsedRow result = Parse(Row(Good()));

        Assert.False(result.IsRejected);
        Assert.Empty(result.Reasons);
        Assert.Equal(2, result.RowNumber);
        Assert.Equal(new DateOnly(2026, 3, 4), result.Date);
        Assert.Equal(TransactionType.Expense, result.Type);
        Assert.Equal(50.00m, result.Amount);
        Assert.Equal(Currency.DOP, result.Currency);
        Assert.Equal("Coffee", result.Description);
        Assert.Equal("Comida", result.CategoryText);
        Assert.Equal("Efectivo", result.PaymentMethodText);
        Assert.False(result.AmountWasAmbiguous);
    }

    // (a) one test per rejection reason code proving it fires and the row IsRejected
    [Theory]
    [InlineData(ImportReason.MissingDate, 0, "")]
    [InlineData(ImportReason.InvalidDate, 0, "31/02/2026")]
    [InlineData(ImportReason.DateBefore1900, 0, "1899-12-31")]
    [InlineData(ImportReason.MissingAmount, 4, "")]
    [InlineData(ImportReason.InvalidAmount, 4, "$50")]
    [InlineData(ImportReason.ZeroAmount, 4, "0.00")]
    [InlineData(ImportReason.NegativeAmount, 4, "-50")]
    [InlineData(ImportReason.TooManyDecimals, 4, "1.23456")]
    [InlineData(ImportReason.AmountTooLarge, 4, "100000000000000.0000")]
    [InlineData(ImportReason.MissingDescription, 2, "   ")]
    [InlineData(ImportReason.InvalidType, 1, "Foo")]
    [InlineData(ImportReason.UnsupportedCurrency, 5, "EUR")]
    public void Parse_EachRejectionReason_FiresAndRejectsTheRow(ImportReason expected, int column, string value)
    {
        ParsedRow result = Parse(Row(With(column, value)));

        Assert.Contains(expected, result.Reasons);
        Assert.True(result.IsRejected);
    }

    [Fact]
    public void Parse_DescriptionTooLong_201Chars_IsRejectedAndNeverTruncated_C15()
    {
        ParsedRow result = Parse(Row(With(2, new string('x', 201))));

        Assert.Contains(ImportReason.DescriptionTooLong, result.Reasons);
        Assert.True(result.IsRejected);
        Assert.Null(result.Description);
    }

    [Fact]
    public void Parse_Description200Chars_IsValidAndWhitespaceIsTrimmed_C15()
    {
        string exact = new('x', 200);

        ParsedRow result = Parse(Row(With(2, "  " + exact + "\t ")));

        Assert.False(result.IsRejected);
        Assert.Equal(exact, result.Description);
    }

    [Fact]
    public void Parse_CellErrorInAnyMappedColumn_IsCellError()
    {
        for (int column = 0; column < 7; column++)
        {
            RawCell[] cells = Good().Select(c => new RawCell(c, RawCellKind.Text)).ToArray();
            cells[column] = new RawCell("#N/A", RawCellKind.Error);

            ParsedRow result = Parse(RowWith(cells));

            Assert.Contains(ImportReason.CellError, result.Reasons);
            Assert.True(result.IsRejected);
            Assert.Single(result.Reasons, ImportReason.CellError);
        }
    }

    [Fact]
    public void Parse_CellErrorInAnUnmappedColumn_IsIgnored()
    {
        ColumnMapping map = new(0, null, 2, null, 4, null, null);
        RawCell[] cells = Good().Select(c => new RawCell(c, RawCellKind.Text)).ToArray();
        cells[1] = new RawCell("#N/A", RawCellKind.Error);

        ParsedRow result = Parse(RowWith(cells), map);

        Assert.False(result.IsRejected);
    }

    // (b) all reasons collected, not first-fail
    [Fact]
    public void Parse_CollectsEveryReason_NotFirstFail()
    {
        string[] cells = Good();
        cells[0] = "31/02/2026";
        cells[2] = "";
        cells[4] = "abc";

        ParsedRow result = Parse(Row(cells));

        Assert.Contains(ImportReason.InvalidDate, result.Reasons);
        Assert.Contains(ImportReason.MissingDescription, result.Reasons);
        Assert.Contains(ImportReason.InvalidAmount, result.Reasons);
        Assert.Equal(3, result.Reasons.Count);
    }

    // (c) column count, C-9
    [Fact]
    public void Row_ShiftedColumns_Rejected_C9()
    {
        // A shifted row: one field too many, the last of them non-empty.
        string[] cells = Good().Append("1,234").ToArray();

        ParsedRow result = Parse(Row(cells));

        Assert.True(result.IsRejected);
        Assert.Equal(new[] { ImportReason.ColumnCountMismatch }, result.Reasons);
        Assert.Null(result.Date);
        Assert.Null(result.Amount);
        Assert.Null(result.Description);
    }

    [Fact]
    public void Row_TooFewFields_RejectedAndNothingElseEvaluated_C9()
    {
        ParsedRow result = Parse(Row("2026-03-04", "Gasto", "Coffee"));

        Assert.Equal(new[] { ImportReason.ColumnCountMismatch }, result.Reasons);
        Assert.Null(result.Amount);
    }

    [Fact]
    public void Row_TrailingEmptyFieldsBeyondTheHeader_AreDropped()
    {
        string[] cells = Good().Concat(new[] { "", "  ", string.Empty }).ToArray();

        ParsedRow result = Parse(Row(cells));

        Assert.False(result.IsRejected);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Row_NonEmptyFieldBeyondTheHeaderAfterEmptyOnes_IsStillAMismatch_C9()
    {
        string[] cells = Good().Concat(new[] { "", "x" }).ToArray();

        ParsedRow result = Parse(Row(cells));

        Assert.Equal(new[] { ImportReason.ColumnCountMismatch }, result.Reasons);
    }

    [Fact]
    public void Row_ExtraCellThatIsAnErrorValue_IsAMismatchNotDropped_C9()
    {
        RawCell[] cells = Good().Select(c => new RawCell(c, RawCellKind.Text)).Append(new RawCell(string.Empty, RawCellKind.Error)).ToArray();

        ParsedRow result = Parse(RowWith(cells));

        Assert.Equal(new[] { ImportReason.ColumnCountMismatch }, result.Reasons);
    }

    [Fact]
    public void IsBlank_AllEmptyOrWhitespaceTextCells_IsTrue_ButNotWhenAnyCellHasContent()
    {
        Assert.True(ImportRowValidator.IsBlank(Row(string.Empty, "  ", string.Empty)));
        Assert.False(ImportRowValidator.IsBlank(Row(string.Empty, "x")));
        Assert.False(ImportRowValidator.IsBlank(RowWith(new RawCell(string.Empty, RawCellKind.Error))));
    }

    // (e) type synonyms
    [Theory]
    [InlineData("Expense", TransactionType.Expense)]
    [InlineData("expense", TransactionType.Expense)]
    [InlineData("GASTO", TransactionType.Expense)]
    [InlineData("Gasto", TransactionType.Expense)]
    [InlineData("egreso", TransactionType.Expense)]
    [InlineData("EGRESO", TransactionType.Expense)]
    [InlineData("Income", TransactionType.Income)]
    [InlineData("INCOME", TransactionType.Income)]
    [InlineData("Ingreso", TransactionType.Income)]
    [InlineData("ingreso", TransactionType.Income)]
    [InlineData("  Ingreso  ", TransactionType.Income)]
    public void Parse_TypeSynonyms_AreRecognizedInAnyCase(string text, TransactionType expected)
    {
        ParsedRow result = Parse(Row(With(1, text)));

        Assert.False(result.IsRejected);
        Assert.Equal(expected, result.Type);
        Assert.DoesNotContain(ImportReason.TypeDefaulted, result.Reasons);
    }

    [Fact]
    public void Parse_TypeWithAccentsFoldsToTheSynonym()
    {
        ParsedRow result = Parse(Row(With(1, "Íngreso")));

        Assert.Equal(TransactionType.Income, result.Type);
    }

    [Theory]
    [InlineData("Foo")]
    [InlineData("Gastos")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("Expense,Income")]
    [InlineData("Gasto.")]
    public void Parse_UnknownType_IsInvalidType(string text)
    {
        ParsedRow result = Parse(Row(With(1, text)));

        Assert.Contains(ImportReason.InvalidType, result.Reasons);
        Assert.True(result.IsRejected);
        Assert.Null(result.Type);
    }

    [Theory]
    [InlineData(TransactionType.Expense)]
    [InlineData(TransactionType.Income)]
    public void Parse_EmptyTypeCell_TakesTheDefaultWithAWarning(TransactionType defaultType)
    {
        ParsedRow result = Parse(Row(With(1, "  ")), options: Options(defaultType: defaultType));

        Assert.False(result.IsRejected);
        Assert.Equal(defaultType, result.Type);
        Assert.Contains(ImportReason.TypeDefaulted, result.Reasons);
    }

    [Fact]
    public void Parse_UnmappedType_TakesTheDefaultSilently()
    {
        ColumnMapping map = new(0, null, 2, 3, 4, 5, 6);

        ParsedRow result = Parse(Row(Good()), map, Options(defaultType: TransactionType.Income));

        Assert.Equal(TransactionType.Income, result.Type);
        Assert.Empty(result.Reasons);
    }

    // (f) sign modes
    [Theory]
    [InlineData("-50", TransactionType.Expense, "50")]
    [InlineData("50", TransactionType.Income, "50")]
    [InlineData("(1,234.50)", TransactionType.Expense, "1234.50")]
    [InlineData("+7.25", TransactionType.Income, "7.25")]
    public void Parse_NegativeIsExpense_TheSignDecidesTheType_C13(string amount, TransactionType expected, string abs)
    {
        ParsedRow result = Parse(Row(With(4, amount)), options: Options(SignMode.NegativeIsExpense));

        Assert.False(result.IsRejected);
        Assert.Equal(expected, result.Type);
        Assert.Equal(decimal.Parse(abs, CultureInfo.InvariantCulture), result.Amount);
    }

    [Theory]
    [InlineData("Foo")]
    [InlineData("Ingreso")]
    [InlineData("")]
    public void Parse_NegativeIsExpense_IgnoresTheTypeColumn(string typeText)
    {
        ParsedRow result = Parse(Row(With(1, typeText)), options: Options(SignMode.NegativeIsExpense));

        Assert.False(result.IsRejected);
        Assert.Equal(TransactionType.Income, result.Type);
        Assert.Empty(result.Reasons);
    }

    [Theory]
    [InlineData("-50")]
    [InlineData("(50.00)")]
    [InlineData("-0.01")]
    public void Parse_Unsigned_NegativeAmount_IsRejectedNeverMadePositive_C13(string amount)
    {
        ParsedRow result = Parse(Row(With(4, amount)), options: Options(SignMode.Unsigned));

        Assert.Contains(ImportReason.NegativeAmount, result.Reasons);
        Assert.True(result.IsRejected);
        Assert.Null(result.Amount);
    }

    [Theory]
    [InlineData(SignMode.Unsigned)]
    [InlineData(SignMode.NegativeIsExpense)]
    public void Parse_ZeroAmount_IsRejectedInEveryMode(SignMode mode)
    {
        foreach (string zero in new[] { "0", "0.00", "-0", "-0.00", "(0.00)" })
        {
            ParsedRow result = Parse(Row(With(4, zero)), options: Options(mode));

            Assert.Contains(ImportReason.ZeroAmount, result.Reasons);
            Assert.True(result.IsRejected);
            Assert.DoesNotContain(ImportReason.NegativeAmount, result.Reasons);
        }
    }

    // (g) currency, C-12
    [Fact]
    public void Parse_UnmappedCurrency_TakesTheDefaultSilently()
    {
        ColumnMapping map = new(0, 1, 2, 3, 4, null, 6);

        ParsedRow result = Parse(Row(Good()), map, Options(defaultCurrency: Currency.USD));

        Assert.Equal(Currency.USD, result.Currency);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Parse_EmptyMappedCurrency_TakesTheDefaultWithAWarning()
    {
        ParsedRow result = Parse(Row(With(5, " ")), options: Options(defaultCurrency: Currency.USD));

        Assert.False(result.IsRejected);
        Assert.Equal(Currency.USD, result.Currency);
        Assert.Contains(ImportReason.CurrencyDefaulted, result.Reasons);
    }

    [Theory]
    [InlineData("dop", Currency.DOP)]
    [InlineData("DOP", Currency.DOP)]
    [InlineData("usd", Currency.USD)]
    [InlineData(" Usd ", Currency.USD)]
    public void Parse_CurrencyCodes_AreCaseInsensitive(string text, Currency expected)
    {
        ParsedRow result = Parse(Row(With(5, text)));

        Assert.Equal(expected, result.Currency);
        Assert.False(result.IsRejected);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("DOP,USD")]
    [InlineData("RD$")]
    [InlineData("DO")]
    public void Currency_Numeric_Rejected_C12(string text)
    {
        ParsedRow result = Parse(Row(With(5, text)));

        Assert.Contains(ImportReason.UnsupportedCurrency, result.Reasons);
        Assert.True(result.IsRejected);
        Assert.Null(result.Currency);
    }

    // (h) future date
    [Fact]
    public void Parse_FutureDate_IsAcceptedWithAWarning_S14()
    {
        ParsedRow result = Parse(Row(With(0, "2027-01-01")));

        Assert.False(result.IsRejected);
        Assert.Equal(new DateOnly(2027, 1, 1), result.Date);
        Assert.Contains(ImportReason.FutureDate, result.Reasons);
    }

    [Fact]
    public void Parse_TodayAndBoundaryDates_DoNotWarn()
    {
        Assert.Empty(Parse(Row(With(0, "2026-09-30"))).Reasons);
        Assert.Empty(Parse(Row(With(0, "1900-01-01"))).Reasons);
    }

    // (i) ambiguity propagation
    [Fact]
    public void Parse_AmbiguousAmountUnderAutoStyle_PropagatesTheFlagAndWarns()
    {
        ParsedRow result = Parse(Row(With(4, "1,234")), options: Options(decimalStyle: DecimalStyle.Auto));

        Assert.True(result.AmountWasAmbiguous);
        Assert.Equal(1234m, result.Amount);
        Assert.Contains(ImportReason.AmbiguousAmount, result.Reasons);
        Assert.False(result.IsRejected);
    }

    [Fact]
    public void Parse_AmbiguousShapeWithAUserChosenStyle_IsNotFlagged()
    {
        ParsedRow result = Parse(Row(With(4, "1,234")), options: Options(decimalStyle: DecimalStyle.Point));

        Assert.False(result.AmountWasAmbiguous);
        Assert.DoesNotContain(ImportReason.AmbiguousAmount, result.Reasons);
    }

    [Fact]
    public void Parse_NumberCellsBypassTheDecimalStyle()
    {
        RawCell[] cells = Good().Select(c => new RawCell(c, RawCellKind.Text)).ToArray();
        cells[4] = new RawCell("1234.5", RawCellKind.Number);
        cells[0] = new RawCell("2026-03-04", RawCellKind.Date);

        ParsedRow result = Parse(RowWith(cells), style: DecimalStyle.Comma);

        Assert.Equal(1234.5m, result.Amount);
        Assert.Equal(new DateOnly(2026, 3, 4), result.Date);
    }

    [Fact]
    public void Parse_CategoryAndPaymentMethod_AreCarriedTrimmedAndBlankBecomesNull()
    {
        string[] cells = Good();
        cells[3] = "  Comida  ";
        cells[6] = "   ";

        ParsedRow result = Parse(Row(cells));

        Assert.Equal("Comida", result.CategoryText);
        Assert.Null(result.PaymentMethodText);
    }

    [Fact]
    public void Parse_UnmappedCategoryAndPaymentMethod_AreNull()
    {
        ColumnMapping map = new(0, 1, 2, null, 4, 5, null);

        ParsedRow result = Parse(Row(Good()), map);

        Assert.Null(result.CategoryText);
        Assert.Null(result.PaymentMethodText);
    }

    [Fact]
    public void Parse_IncompleteMapping_ThrowsRatherThanGuessing()
    {
        ColumnMapping incomplete = new(0, null, null, null, 4, null, null);

        Assert.Throws<ArgumentException>(() => Parse(Row(Good()), incomplete));
    }

    [Fact]
    public void Parse_MappingIndexBeyondTheHeader_ThrowsRatherThanGuessing()
    {
        ColumnMapping outOfRange = new(0, null, 2, null, 9, null, null);

        Assert.Throws<ArgumentException>(() => Parse(Row(Good()), outOfRange));
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    [InlineData("de-DE")]
    public void Parse_IgnoresTheCurrentCulture(string cultureName)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);

            ParsedRow good = Parse(Row(With(4, "1,234.56")));
            ParsedRow comma = Parse(Row(With(4, "1.234,56")), style: DecimalStyle.Comma);
            ParsedRow slashes = Parse(Row(With(0, "03/04/2026")), options: Options(dateFormat: ImportDateFormat.MonthDayYear));
            ParsedRow turkishI = Parse(Row(With(1, "INGRESO")));

            Assert.Equal(1234.56m, good.Amount);
            Assert.Equal(1234.56m, comma.Amount);
            Assert.Equal(new DateOnly(2026, 3, 4), slashes.Date);
            Assert.Equal(TransactionType.Income, turkishI.Type);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Parse_TurkishCultureDoesNotBreakTypeOrCurrencyMatching()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            ParsedRow result = Parse(Row(With(1, "Income")));

            Assert.Equal(TransactionType.Income, result.Type);
            Assert.Equal(Currency.USD, Parse(Row(With(5, "usd"))).Currency);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Parse_RoundTrip_ParsedAmountFormattedInvariantAndParsedAgainNeverChangesTheValue()
    {
        foreach (string token in new[] { "0.0001", "1", "12.5", "1,234.5678", "99999999999999.9999", "0.10", "1000000" })
        {
            decimal first = Parse(Row(With(4, token))).Amount!.Value;
            string formatted = first.ToString("0.0000", CultureInfo.InvariantCulture);
            decimal second = Parse(Row(With(4, formatted))).Amount!.Value;

            Assert.Equal(first, second);
        }
    }
}
