namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>F-020 S2 (2.1): guards the binary fixtures against an editor re-save that would change their bytes.</summary>
public sealed class FixturesAreByteExactTests
{
    [Fact]
    public void Bank1252Fixture_HoldsLatin1AccentBytes_NotUtf8()
    {
        byte[] bytes = CsvTestSupport.Fixture("bank-es-semicolon-comma-1252.csv");

        Assert.Contains((byte)0xED, bytes);
        Assert.DoesNotContain((byte)0xC3, bytes);
    }

    [Fact]
    public void ExcelUnicodeFixture_StartsWithUtf16LittleEndianBom()
    {
        byte[] bytes = CsvTestSupport.Fixture("excel-unicode-tab-utf16.txt");

        Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
    }

    [Fact]
    public void GenericEnFixture_StartsWithUtf8Bom()
    {
        byte[] bytes = CsvTestSupport.Fixture("generic-en-comma-utf8bom.csv");

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
    }

    [Fact]
    public void QuotesMultilineFixture_KeepsItsCrlfAndLoneLfBytes()
    {
        byte[] bytes = CsvTestSupport.Fixture("quotes-multiline.csv");

        Assert.Contains((byte)'\r', bytes);
        Assert.Contains((byte)'\n', bytes);
    }
}
