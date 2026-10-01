using System.Globalization;

namespace ControlDeFinanzasIA.UI.Formatting;

/// <summary>
/// The one place that spells month and weekday names for the screen. They follow the language the app displays
/// (<see cref="CultureInfo.CurrentUICulture"/>, set from the persisted locale at startup), never the operating
/// system's regional format (<see cref="CultureInfo.CurrentCulture"/>), so a Spanish UI never shows "October".
/// Numbers and currency keep following the regional format.
/// </summary>
internal static class UiCulture
{
    /// <summary>Gets the culture the app displays text in.</summary>
    internal static CultureInfo Current => CultureInfo.CurrentUICulture;

    /// <summary>The full month name and the year, first letter upper-cased ("Octubre 2026", "October 2026").</summary>
    internal static string MonthYear(DateOnly date)
    {
        string text = date.ToString("MMMM yyyy", Current);
        return text.Length == 0 ? text : char.ToUpper(text[0], Current) + text[1..];
    }

    /// <summary>The abbreviated month name ("oct.", "Oct").</summary>
    internal static string MonthAbbreviated(DateOnly date) => date.ToString("MMM", Current);

    /// <summary>The day number and the abbreviated month ("14 oct.", "14 Oct").</summary>
    internal static string DayMonth(DateOnly date) => date.ToString("d MMM", Current);

    /// <summary>The long date with the weekday ("miércoles, 14 de octubre de 2026").</summary>
    internal static string LongDate(DateOnly date) => date.ToString("D", Current);
}
