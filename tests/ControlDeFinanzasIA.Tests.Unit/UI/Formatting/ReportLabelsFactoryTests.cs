using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.UI.Formatting;
using ControlDeFinanzasIA.UI.Resources;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI.Formatting;

/// <summary>Unit tests for <see cref="ReportLabelsFactory"/> (F-018, R-10, S-13).</summary>
public sealed class ReportLabelsFactoryTests
{
    // CurrentUICulture is async-local, so setting it inside a test does not leak across tests.
    private static CultureInfo UseCulture(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        return culture;
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public void Create_CarriesCurrentCultureAndResxStrings(string cultureName)
    {
        CultureInfo culture = UseCulture(cultureName);

        ReportLabels labels = ReportLabelsFactory.Create();

        Assert.Equal(culture, labels.Culture);
        Assert.Equal(Strings.ReportMonthSummaryTitle, labels.ReportMonthSummaryTitle);
        Assert.Equal(Strings.ReportPageLabel, labels.ReportPageLabel);
        Assert.Equal(Strings.ReportGeneratedOnFormat, labels.ReportGeneratedOnFormat);
    }

    [Fact]
    public void Create_TitlesDifferBetweenEnglishAndSpanish()
    {
        UseCulture("es-DO");
        string es = ReportLabelsFactory.Create().ReportMonthSummaryTitle;
        UseCulture("en");
        string en = ReportLabelsFactory.Create().ReportMonthSummaryTitle;

        Assert.NotEqual(es, en);
        Assert.Equal("Resumen mensual", es);
        Assert.Equal("Monthly summary", en);
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public void Create_EveryStringIsNonEmpty(string cultureName)
    {
        UseCulture(cultureName);

        ReportLabels labels = ReportLabelsFactory.Create();

        var empty = typeof(ReportLabels).GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Where(p => string.IsNullOrWhiteSpace((string?)p.GetValue(labels)))
            .Select(p => p.Name)
            .ToList();
        Assert.Empty(empty);
    }
}
