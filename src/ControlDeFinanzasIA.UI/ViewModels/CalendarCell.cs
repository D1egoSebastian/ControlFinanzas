namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// A display-ready cell of the calendar grid (F-009, slice 3), projected
/// from <c>Application.UseCases.Calendar.GetCalendarMonth.CalendarDay</c>.
/// </summary>
/// <param name="Date">The cell's date.</param>
/// <param name="DayNumber">The day-of-month number, pre-formatted for display.</param>
/// <param name="TotalDisplay">
/// The DOP-normalized total for <see cref="Date"/>, formatted via
/// <see cref="Formatting.MoneyFormat.ApproximateDop"/>, or an empty string
/// when the day has no applicable total (adjacent-month filler, see
/// <see cref="IsCurrentMonth"/>).
/// </param>
/// <param name="IsCurrentMonth">
/// <see langword="true"/> when <see cref="Date"/> belongs to the currently
/// navigated month; <see langword="false"/> for adjacent-month filler days
/// (R-3), which are shown visually dimmed and do not drill down to Gastos.
/// </param>
/// <param name="HasSpend">
/// <see langword="false"/> when nothing was spent that day: the cell then shows only the day number.
/// </param>
/// <param name="IsToday">
/// <see langword="true"/> when <see cref="Date"/> is the current date.
/// </param>
public sealed record CalendarCell(
    DateOnly Date,
    string DayNumber,
    string TotalDisplay,
    bool IsCurrentMonth,
    bool IsToday,
    bool HasSpend = true)
{
    /// <summary>Gets a value indicating whether the day total is shown: only for a day of the navigated month with something spent.</summary>
    public bool ShowTotal => IsCurrentMonth && HasSpend && TotalDisplay.Length > 0;

    /// <summary>Gets the accessible name of the day button: the full date in the app language, followed by the day total when it is shown (F-021 S8).</summary>
    public string AutomationName =>
        ShowTotal
            ? $"{ControlDeFinanzasIA.UI.Formatting.UiCulture.LongDate(Date)}, {TotalDisplay}"
            : ControlDeFinanzasIA.UI.Formatting.UiCulture.LongDate(Date);
}
