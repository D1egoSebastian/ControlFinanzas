using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Domain.Recurring;

/// <summary>
/// Pure schedule maths for recurring rules: a charge is defined by the day of the month it always happens on
/// (plus the month, for a yearly charge). The chosen day is the <em>anchor</em>: it is carried by the day of the
/// rule's <see cref="RecurringRule.StartDate"/>, and every occurrence is computed from it, so a day 29, 30 or 31
/// charge is clamped to the last day of a short month for that month only and returns to its day afterwards.
/// </summary>
public static class RecurringSchedule
{
    /// <summary>Gets the number of months between two occurrences of a frequency.</summary>
    /// <param name="frequency">Monthly, Bimonthly or Yearly (anything else counts as monthly).</param>
    /// <returns>1, 2 or 12.</returns>
    public static int MonthsOf(string frequency) => frequency switch
    {
        nameof(RecurringFrequency.Bimonthly) => 2,
        nameof(RecurringFrequency.Yearly) => 12,
        _ => 1,
    };

    /// <summary>
    /// Adds months to a date keeping the anchor day: the result is on <paramref name="anchorDay"/>, or on the last
    /// day of the target month when that month is shorter.
    /// </summary>
    /// <param name="anchorDay">The day of the month the charge always happens on (1-31).</param>
    /// <param name="date">A date in the month to start from.</param>
    /// <param name="months">Months to add (may be negative).</param>
    /// <returns>The anchored date.</returns>
    public static DateOnly AddMonthsAnchored(int anchorDay, DateOnly date, int months)
    {
        int index = (date.Year * 12) + (date.Month - 1) + months;
        int year = index / 12;
        int month = (index % 12) + 1;
        return new DateOnly(year, month, Math.Min(anchorDay, DateTime.DaysInMonth(year, month)));
    }

    /// <summary>Gets the first charge: the first time on or after <paramref name="today"/> the chosen day comes around.</summary>
    /// <param name="frequency">Monthly, Bimonthly or Yearly.</param>
    /// <param name="dayOfMonth">The day of the month (1-31).</param>
    /// <param name="month">The month (1-12); required for a yearly charge, ignored otherwise.</param>
    /// <param name="today">Today.</param>
    /// <returns>The first charge date (a day beyond the month's end is clamped to its last day).</returns>
    public static DateOnly FirstCharge(string frequency, int dayOfMonth, int? month, DateOnly today)
    {
        ValidateDay(dayOfMonth);
        if (frequency == nameof(RecurringFrequency.Yearly))
        {
            int m = ValidateMonth(month);
            DateOnly thisYear = Clamp(today.Year, m, dayOfMonth);
            return thisYear >= today ? thisYear : Clamp(today.Year + 1, m, dayOfMonth);
        }

        DateOnly thisMonth = Clamp(today.Year, today.Month, dayOfMonth);
        return thisMonth >= today ? thisMonth : AddMonthsAnchored(dayOfMonth, thisMonth, 1);
    }

    /// <summary>
    /// Gets the start date to store for a rule whose first charge is <paramref name="firstCharge"/>. It is the first
    /// charge itself, unless that charge was clamped (a day 31 charge in November): then it is the closest earlier
    /// date of the same cadence that really has the chosen day, so the stored start date still carries the day.
    /// </summary>
    /// <param name="frequency">Monthly, Bimonthly or Yearly.</param>
    /// <param name="dayOfMonth">The day of the month (1-31).</param>
    /// <param name="month">The month (1-12); required for a yearly charge, ignored otherwise.</param>
    /// <param name="firstCharge">The first charge date from <see cref="FirstCharge"/>.</param>
    /// <returns>The start date that carries the chosen day.</returns>
    public static DateOnly AnchorStartDate(string frequency, int dayOfMonth, int? month, DateOnly firstCharge)
    {
        ValidateDay(dayOfMonth);
        if (firstCharge.Day == dayOfMonth)
        {
            return firstCharge;
        }

        int step = MonthsOf(frequency);
        DateOnly probe = firstCharge;
        for (int i = 0; i < 12; i++)
        {
            probe = AddMonthsAnchored(dayOfMonth, probe, -step);
            if (probe.Day == dayOfMonth)
            {
                return probe;
            }
        }

        return firstCharge;
    }

    private static DateOnly Clamp(int year, int month, int day) =>
        new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

    private static void ValidateDay(int dayOfMonth)
    {
        if (dayOfMonth is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(dayOfMonth), dayOfMonth, "The day of the month must be between 1 and 31.");
        }
    }

    private static int ValidateMonth(int? month)
    {
        if (month is null)
        {
            throw new ArgumentException("A yearly charge needs a month.", nameof(month));
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "The month must be between 1 and 12.");
        }

        return month.Value;
    }
}
