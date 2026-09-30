using System.Diagnostics;
using System.Globalization;
using System.Text;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Infrastructure.Import;
using static ControlDeFinanzasIA.Tests.Integration.Import.CsvTestSupport;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>F-020 S2: the REAL <c>CsvTableReader</c> over real bytes (no fakes).</summary>
public sealed class CsvTableReaderTests
{
    // ---- (a) delimiter sniff and override ----

    [Theory]
    [InlineData(',')]
    [InlineData(';')]
    [InlineData('\t')]
    public async Task Sniff_PicksEachDelimiter(char d)
    {
        string text = $"a{d}b{d}c\r\n1{d}2{d}3\r\n4{d}5{d}6\r\n";

        RawTable table = await ReadTableAsync(Utf8(text));

        Assert.Equal(d, table.Source.Delimiter);
        Assert.Equal(new[] { "a", "b", "c" }, table.Headers);
        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public async Task Sniff_IgnoresDelimitersInsideQuotes()
    {
        // Commas only appear inside quotes; the semicolon is the real delimiter.
        string text = "\"a,b,c,d\";x;y\r\n\"1,2,3,4\";2;3\r\n";

        RawTable table = await ReadTableAsync(Utf8(text));

        Assert.Equal(';', table.Source.Delimiter);
        Assert.Equal("a,b,c,d", table.Headers[0]);
    }

    [Fact]
    public async Task Sniff_SemicolonFileWithDecimalCommas_StaysSemicolon()
    {
        RawTable table = await ReadTableAsync(Fixture("bank-es-semicolon-comma-1252.csv"));

        Assert.Equal(';', table.Source.Delimiter);
        Assert.Equal("1.234,56", table.Rows[0].Cells[3].Text);
    }

    [Fact]
    public async Task Sniff_NoCandidateWithTwoColumns_FallsBackToCommaSingleColumn()
    {
        RawTable table = await ReadTableAsync(Utf8("only\r\nx\r\ny\r\n"));

        Assert.Equal(',', table.Source.Delimiter);
        Assert.Single(table.Headers);
    }

    [Fact]
    public async Task ExplicitDelimiter_OverridesSniffAndReparsesDifferently()
    {
        byte[] bytes = Utf8("a;b,c\r\n1;2,3\r\n");

        RawTable sniffed = await ReadTableAsync(bytes);
        RawTable forced = await ReadTableAsync(bytes, ',');

        Assert.Equal(new[] { "a;b", "c" }, forced.Headers);
        Assert.Equal(',', forced.Source.Delimiter);
        Assert.NotEqual(sniffed.Headers, forced.Headers);
    }

    // ---- (b) quoting ----

    [Fact]
    public async Task Quotes_EscapeDelimiterAndEmbeddedNewlinesAreExact()
    {
        RawTable table = await ReadTableAsync(Fixture("quotes-multiline.csv"));

        Assert.Equal("Line one\r\nLine two, with \"quotes\"", table.Rows[0].Cells[1].Text);
        Assert.Equal("Lone\nLF inside", table.Rows[1].Cells[1].Text);
        Assert.Equal(3, table.Rows[0].Cells.Count);
    }

    [Theory]
    [InlineData("a,b\r\n\"x\ry\",z\r\n", "x\ry")]
    [InlineData("a,b\r\n\"x\ny\",z\r\n", "x\ny")]
    [InlineData("a,b\r\n\"x\r\ny\",z\r\n", "x\r\ny")]
    [InlineData("a,b\r\n\"say \"\"hi\"\"\",z\r\n", "say \"hi\"")]
    [InlineData("a,b\r\n\"p,q\",z\r\n", "p,q")]
    [InlineData("a,b\r\n\"\",z\r\n", "")]
    public async Task Quotes_YieldExactValues(string text, string expected)
    {
        RawTable table = await ReadTableAsync(Utf8(text), ',');

        Assert.Equal(expected, table.Rows[0].Cells[0].Text);
        Assert.Equal("z", table.Rows[0].Cells[1].Text);
    }

    [Fact]
    public async Task QuoteInsideUnquotedField_IsKeptLiterally()
    {
        RawTable table = await ReadTableAsync(Utf8("a,b\r\n5\" pipe,z\r\n"), ',');

        Assert.Equal("5\" pipe", table.Rows[0].Cells[0].Text);
    }

    // ---- (c) BOM, line endings, blanks, whitespace ----

    [Fact]
    public async Task Utf8Bom_IsRemovedFromTheFirstHeader()
    {
        RawTable table = await ReadTableAsync(Fixture("generic-en-comma-utf8bom.csv"));

        Assert.Equal("Date", table.Headers[0]);
        Assert.Equal("UTF-8", table.Source.EncodingName);
        Assert.False(table.Source.EncodingFallback);
        Assert.Equal(2, table.Rows.Count);
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    [InlineData("\r")]
    public async Task RecordEnds_CrlfLfAndLoneCr_AreAllRecognised(string eol)
    {
        RawTable table = await ReadTableAsync(Utf8($"a,b{eol}1,2{eol}3,4{eol}"));

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(new[] { "3", "4" }, Texts(table.Rows[1]));
    }

    [Fact]
    public async Task MixedLineEndings_AreAllRecognised()
    {
        RawTable table = await ReadTableAsync(Utf8("a,b\r\n1,2\n3,4\r5,6"));

        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(new[] { "5", "6" }, Texts(table.Rows[2]));
    }

    [Fact]
    public async Task BlankAndAllEmptyLines_AreSkippedAndDoNotCount()
    {
        RawTable table = await ReadTableAsync(Utf8("a,b,c\r\n\r\n1,2,3\r\n,,,\r\n   ,  ,\r\n4,5,6\r\n\r\n"), ',');

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(new[] { "4", "5", "6" }, Texts(table.Rows[1]));
    }

    [Fact]
    public async Task WhitespaceOutsideQuotes_IsPreserved()
    {
        RawTable table = await ReadTableAsync(Utf8("a,b\r\n  x  , y\r\n"), ',');

        Assert.Equal(new[] { "  x  ", " y" }, Texts(table.Rows[0]));
    }

    [Fact]
    public async Task EveryCell_IsText()
    {
        RawTable table = await ReadTableAsync(Fixture("generic-en-comma-utf8bom.csv"));

        Assert.All(table.Rows.SelectMany(r => r.Cells), c => Assert.Equal(RawCellKind.Text, c.Kind));
        Assert.Equal(ImportFileKind.Csv, table.Source.Kind);
        Assert.Null(table.Source.SheetName);
    }

    [Fact]
    public async Task SourceRowNumber_IsThePhysicalStartLineOfTheRecord()
    {
        // line 1 header, line 2 blank, lines 3-4 one record with an embedded newline, line 5 record.
        RawTable table = await ReadTableAsync(Utf8("a,b\r\n\r\n\"x\r\ny\",1\r\nlast,2\r\n"), ',');

        Assert.Equal(3, table.Rows[0].SourceRowNumber);
        Assert.Equal(5, table.Rows[1].SourceRowNumber);
    }

    [Fact]
    public async Task SourceRowNumber_CountsLoneCrAsALine()
    {
        RawTable table = await ReadTableAsync(Utf8("a,b\r1,2\r3,4"), ',');

        Assert.Equal(new[] { 2, 3 }, table.Rows.Select(r => r.SourceRowNumber).ToArray());
    }

    // ---- (d) encodings ----

    [Fact]
    public async Task ValidUtf8WithoutBom_ReportsUtf8NoFallback()
    {
        RawTable table = await ReadTableAsync(Utf8("Descripción,Niño\r\nCafé,ñu\r\n"));

        Assert.Equal("UTF-8", table.Source.EncodingName);
        Assert.False(table.Source.EncodingFallback);
        Assert.Equal("Descripción", table.Headers[0]);
        Assert.Equal("ñu", table.Rows[0].Cells[1].Text);
    }

    [Fact]
    public async Task Windows1252Fixture_DecodesAccentsAndReportsFallback()
    {
        RawTable table = await ReadTableAsync(Fixture("bank-es-semicolon-comma-1252.csv"));

        Assert.True(table.Source.EncodingFallback);
        Assert.Equal("Windows-1252", table.Source.EncodingName);
        Assert.Equal("Categoría", table.Headers[2]);
        Assert.Equal("Café", table.Rows[0].Cells[1].Text);
        Assert.Equal("Niño", table.Rows[1].Cells[1].Text);
        Assert.StartsWith("Fecha;Descripción;Categoría", table.Source.SampleText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SampleText_IsTheFirst200DecodedCharacters()
    {
        string text = "a,b\r\n" + string.Concat(Enumerable.Repeat("0123456789,x\r\n", 40));

        RawTable table = await ReadTableAsync(Utf8(text));

        Assert.Equal(200, table.Source.SampleText.Length);
        Assert.Equal(text[..200], table.Source.SampleText);
    }

    [Fact]
    public async Task Utf16LeBomFixture_DecodesWithTabDelimiter()
    {
        RawTable table = await ReadTableAsync(Fixture("excel-unicode-tab-utf16.txt"));

        Assert.Equal("UTF-16LE", table.Source.EncodingName);
        Assert.False(table.Source.EncodingFallback);
        Assert.Equal('\t', table.Source.Delimiter);
        Assert.Equal(new[] { "Date", "Description", "Category", "Amount" }, table.Headers);
        Assert.Equal("12.50", table.Rows[0].Cells[3].Text);
    }

    [Fact]
    public async Task Utf16BeBom_Decodes()
    {
        byte[] body = new UnicodeEncoding(true, false).GetBytes("a;ñ\r\n1;2\r\n");
        byte[] bytes = new byte[] { 0xFE, 0xFF }.Concat(body).ToArray();

        RawTable table = await ReadTableAsync(bytes);

        Assert.Equal("UTF-16BE", table.Source.EncodingName);
        Assert.Equal("ñ", table.Headers[1]);
    }

    [Fact]
    public async Task Utf16WithOddByteCount_IsUnreadableBinary()
    {
        byte[] bytes = new byte[] { 0xFF, 0xFE, 0x61, 0x00, 0x62 };

        AssertFailure(await ReadAsync(bytes), ImportErrorCodes.UnreadableFile, ImportErrorCodes.ReasonBinary);
    }

    [Fact]
    public async Task NulByteWithoutBom_IsUnreadableBinary()
    {
        AssertFailure(
            await ReadAsync(new byte[] { 0x61, 0x2C, 0x00, 0x62, 0x0D, 0x0A }),
            ImportErrorCodes.UnreadableFile,
            ImportErrorCodes.ReasonBinary);
    }

    [Fact]
    public async Task BinaryGarbage_IsRejectedNotDecodedAsRows()
    {
        var rng = new Random(1234);
        byte[] garbage = new byte[4096];
        rng.NextBytes(garbage);
        garbage[10] = 0;

        AssertFailure(await ReadAsync(garbage), ImportErrorCodes.UnreadableFile, ImportErrorCodes.ReasonBinary);
    }

    // ---- (e) malformed input ----

    [Fact]
    public async Task UnterminatedQuoteAtEof_FailsWithStartLine()
    {
        AssertFailure(
            await ReadAsync(Utf8("a,b\r\n1,2\r\n3,\"never closed\r\nmore\r\n")),
            ImportErrorCodes.UnreadableFile,
            ImportErrorCodes.ReasonMalformedCsv,
            line: 3);
    }

    [Fact]
    public async Task StrayQuoteAfterClosingQuote_FailsWithStartLine()
    {
        AssertFailure(
            await ReadAsync(Utf8("a,b\r\n1,2\r\n\"abc\"def,4\r\n")),
            ImportErrorCodes.UnreadableFile,
            ImportErrorCodes.ReasonMalformedCsv,
            line: 3);
    }

    // ---- (f) no rows ----

    [Theory]
    [InlineData("")]
    [InlineData("a,b,c")]
    [InlineData("a,b,c\r\n")]
    [InlineData("\r\n\r\n   \r\n")]
    [InlineData(",,,\r\n,,,\r\n")]
    public async Task NoDataRows_ReturnsNoRows(string text) =>
        AssertFailure(await ReadAsync(Utf8(text)), ImportErrorCodes.NoRows);

    [Fact]
    public async Task BomOnly_ReturnsNoRows() =>
        AssertFailure(await ReadAsync(new byte[] { 0xEF, 0xBB, 0xBF }), ImportErrorCodes.NoRows);

    // ---- (h) mismatched field counts are kept ----

    [Fact]
    public async Task RecordsWithDifferentFieldCount_AreKeptForTheValidator()
    {
        RawTable table = await ReadTableAsync(Utf8("a,b,c\r\n1,2\r\n1,2,3,4\r\n1,2,3\r\n"), ',');

        Assert.Equal(new[] { 2, 4, 3 }, table.Rows.Select(r => r.Cells.Count).ToArray());
    }

    // ---- (i) failures are values, never exceptions ----

    [Fact]
    public async Task BadInput_NeverThrowsOrFaultsTheTask()
    {
        Task<Result> task = new CsvTableReader().ReadAsync(new byte[] { 0xFF, 0x00, 0xFE }, new TabularReadOptions(null));

        Result result = await task;

        Assert.False(task.IsFaulted);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task AlreadyCancelledToken_Cancels()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CsvTableReader().ReadAsync(Utf8("a,b\r\n1,2\r\n"), new TabularReadOptions(null), cts.Token));
    }

    // ---- 2.3 limits ----

    private static string Header(int columns) => string.Join(',', Enumerable.Range(0, columns).Select(i => "c" + i));

    private static string Row(int columns) => string.Join(',', Enumerable.Repeat("v", columns));

    [Fact]
    public async Task Columns_50Passes_51Fails_StoppingBeforeMalformedTail()
    {
        RawTable ok = await ReadTableAsync(Utf8(Header(50) + "\r\n" + Row(50) + "\r\n"), ',');
        Assert.Equal(50, ok.Headers.Count);

        // The malformed tail (unterminated quote) would give malformedCsv if the reader read to the end.
        Result tooMany = await ReadAsync(Utf8(Header(51) + "\r\n" + Row(51) + "\r\n\"unterminated"), ',');
        AssertFailure(tooMany, ImportErrorCodes.TooLarge, ImportErrorCodes.ReasonTooManyColumns, line: 1);
    }

    [Fact]
    public async Task Columns_51stOnADataRow_Fails()
    {
        Result result = await ReadAsync(Utf8(Header(3) + "\r\n" + Row(3) + "\r\n" + Row(51) + "\r\n\"unterminated"), ',');

        AssertFailure(result, ImportErrorCodes.TooLarge, ImportErrorCodes.ReasonTooManyColumns, line: 3);
    }

    [Fact]
    public async Task Field_32768Passes_32769Fails_StoppingBeforeMalformedTail()
    {
        RawTable ok = await ReadTableAsync(Utf8("a,b\r\n" + new string('x', 32768) + ",1\r\n"), ',');
        Assert.Equal(32768, ok.Rows[0].Cells[0].Text.Length);

        RawTable okQuoted = await ReadTableAsync(Utf8("a,b\r\n\"" + new string('x', 32768) + "\",1\r\n"), ',');
        Assert.Equal(32768, okQuoted.Rows[0].Cells[0].Text.Length);

        AssertFailure(
            await ReadAsync(Utf8("a,b\r\n" + new string('x', 32769) + ",1\r\n\"unterminated"), ','),
            ImportErrorCodes.TooLarge,
            ImportErrorCodes.ReasonFieldTooLong,
            line: 2);
        AssertFailure(
            await ReadAsync(Utf8("a,b\r\n\"" + new string('x', 32769) + "\",1\r\n\"unterminated"), ','),
            ImportErrorCodes.TooLarge,
            ImportErrorCodes.ReasonFieldTooLong,
            line: 2);
    }

    [Fact]
    public async Task Rows_50000Passes_50001Fails_StoppingBeforeMalformedTail()
    {
        string body = string.Concat(Enumerable.Repeat("1,2\r\n", 50_000));

        RawTable ok = await ReadTableAsync(Utf8("a,b\r\n" + body), ',');
        Assert.Equal(50_000, ok.Rows.Count);

        Result tooMany = await ReadAsync(Utf8("a,b\r\n" + body + "1,2\r\n\"unterminated"), ',');
        AssertFailure(tooMany, ImportErrorCodes.TooLarge, ImportErrorCodes.ReasonTooManyRows);
    }

    [Fact]
    public async Task BlankLines_DoNotCountTowardsTheRowLimit()
    {
        string body = string.Concat(Enumerable.Repeat("1,2\r\n\r\n,\r\n", 20_000));

        RawTable table = await ReadTableAsync(Utf8("a,b\r\n" + body), ',');

        Assert.Equal(20_000, table.Rows.Count);
    }

    [Fact]
    public async Task File_OverTenMiB_FailsBeforeDecoding()
    {
        // Invalid UTF-8 (0xFF) with no NUL: decoding would fall through to Windows-1252 and succeed as text.
        byte[] over = new byte[ImportLimits.MaxFileBytes + 1];
        Array.Fill(over, (byte)0xFF);
        var sw = Stopwatch.StartNew();

        Result result = await ReadAsync(over);

        sw.Stop();
        AssertFailure(result, ImportErrorCodes.TooLarge, ImportErrorCodes.ReasonFileTooLarge);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"Took {sw.Elapsed}; the size check must run before any decoding.");
    }

    [Fact]
    public async Task File_ExactlyTenMiB_IsNotRejectedForSize()
    {
        byte[] exact = new byte[ImportLimits.MaxFileBytes];
        Array.Fill(exact, (byte)'a');
        Utf8("a,b\r\n").CopyTo(exact, 0);
        exact[^1] = (byte)'\n';

        Result result = await ReadAsync(exact, ',');

        // The single giant field trips the field limit, proving the size check itself let the file through.
        AssertFailure(result, ImportErrorCodes.TooLarge, ImportErrorCodes.ReasonFieldTooLong);
    }

    // ---- 50k performance ----

    [Fact]
    public async Task FiftyThousandRows_ParseWithinGenerousCeiling()
    {
        var sb = new StringBuilder("Date,Type,Description,Category,Amount,Currency\r\n");
        for (int i = 0; i < 50_000; i++)
        {
            sb.Append("2026-03-01,Expense,\"Item ").Append(i).Append(", with comma\",Food,12.50,DOP\r\n");
        }

        byte[] bytes = Utf8(sb.ToString());
        Assert.True(bytes.Length <= ImportLimits.MaxFileBytes);
        var sw = Stopwatch.StartNew();

        RawTable table = await ReadTableAsync(bytes);

        sw.Stop();
        Assert.Equal(50_000, table.Rows.Count);
        Assert.Equal("Item 49999, with comma", table.Rows[^1].Cells[2].Text);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"Took {sw.Elapsed}.");
    }

    // ---- 2.5 determinism ----

    [Fact]
    public async Task SameBytes_ParseIdenticallyUnderEsDoAndEnCultures()
    {
        byte[] bytes = Fixture("bank-es-semicolon-comma-1252.csv");
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-DO");
            RawTable es = await ReadTableAsync(bytes);
            CultureInfo.CurrentCulture = new CultureInfo("en");
            RawTable en = await ReadTableAsync(bytes);

            Assert.Equal(es.Headers, en.Headers);
            Assert.Equal(es.Source, en.Source);
            Assert.Equal(es.Rows.Count, en.Rows.Count);
            for (int i = 0; i < es.Rows.Count; i++)
            {
                Assert.Equal(es.Rows[i].SourceRowNumber, en.Rows[i].SourceRowNumber);
                Assert.Equal(es.Rows[i].Cells, en.Rows[i].Cells);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
