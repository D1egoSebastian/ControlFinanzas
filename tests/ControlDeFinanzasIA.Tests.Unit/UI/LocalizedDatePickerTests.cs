using System.Globalization;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.UI.Controls;
using ControlDeFinanzasIA.UI.Resources;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Pre-1.0 slice E4: the stock date picker paints the English words "day", "month" and "year" while no date is
/// picked, whatever the app language is. <see cref="LocalizedDatePicker"/> replaces them with resx text and keeps
/// the stock picker's styling, template and behavior.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class LocalizedDatePickerTests
{
    private static Task<T> InCulture<T>(string culture, Func<T> body) =>
        HeadlessUi.OnUiThread(() =>
        {
            CultureInfo previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            try
            {
                return Task.FromResult(body());
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        });

    private static string[] Placeholders(DatePicker picker) =>
    [
        .. new[] { "PART_DayTextBlock", "PART_MonthTextBlock", "PART_YearTextBlock" }
            .Select(name => picker.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == name).Text ?? string.Empty),
    ];

    [Theory]
    [InlineData("es-DO", "día", "mes", "año")]
    [InlineData("en", "day", "month", "year")]
    public async Task AnEmptyPicker_ShowsThePlaceholdersInTheAppLanguage(string culture, string day, string month, string year)
    {
        string[] shown = await InCulture(culture, () =>
        {
            var picker = new LocalizedDatePicker();
            Window window = HeadlessUi.Show(picker, new object());
            try
            {
                HeadlessUi.Flush();
                return Placeholders(picker);
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.Equal(new[] { day, month, year }, shown);
    }

    [Fact]
    public async Task APickedDate_ShowsTheDateAndAClearedPickerGoesBackToThePlaceholders()
    {
        (string[] Picked, string[] Cleared) result = await InCulture("es-DO", () =>
        {
            var picker = new LocalizedDatePicker();
            Window window = HeadlessUi.Show(picker, new object());
            try
            {
                HeadlessUi.Flush();
                picker.SelectedDate = new DateTimeOffset(new DateTime(2026, 10, 2));
                HeadlessUi.Flush();
                string[] picked = Placeholders(picker);
                picker.SelectedDate = null;
                HeadlessUi.Flush();
                return (picked, Placeholders(picker));
            }
            finally
            {
                window.Close();
                HeadlessUi.Flush();
            }
        });

        Assert.Equal("2", result.Picked[0]);
        Assert.NotEqual("mes", result.Picked[1]);
        Assert.Equal("2026", result.Picked[2]);
        Assert.Equal(new[] { "día", "mes", "año" }, result.Cleared);
    }
}
