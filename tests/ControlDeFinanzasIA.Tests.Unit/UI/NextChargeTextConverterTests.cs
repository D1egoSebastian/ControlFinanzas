using System.Globalization;
using ControlDeFinanzasIA.UI.Converters;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Pre-1.0 slice C: a next-charge date is written with the month name in the UI language, so "11/9" can never be
/// read as November 9 by one person and September 11 by another.
/// </summary>
public sealed class NextChargeTextConverterTests
{
    [Theory]
    [InlineData("es-DO", "Próximo cobro: 11 sept 2026")]
    [InlineData("en", "Next charge: 11 Sep 2026")]
    public void Format_UsesTheUiLanguageAndTheMonthName(string culture, string expected)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            string text = NextChargeTextConverter.Format(new DateOnly(2026, 9, 11));

            Assert.StartsWith(expected[..expected.IndexOf(':', StringComparison.Ordinal)], text, StringComparison.Ordinal);
            Assert.Contains("2026", text, StringComparison.Ordinal);
            Assert.Contains(
                CultureInfo.GetCultureInfo(culture).DateTimeFormat.GetAbbreviatedMonthName(9),
                text,
                StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Convert_AcceptsADateOnly_AndLeavesAnythingElseBlank()
    {
        var converter = new NextChargeTextConverter();

        Assert.Equal(
            NextChargeTextConverter.Format(new DateOnly(2026, 10, 5)),
            converter.Convert(new DateOnly(2026, 10, 5), typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal(string.Empty, converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
    }
}
