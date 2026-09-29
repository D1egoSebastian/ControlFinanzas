using System.Globalization;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.UI.Formatting;

/// <summary>
/// Builds the user-facing title and body of a notification from its stored facts (F-017, R-11, D-10).
/// </summary>
/// <remarks>
/// Pure and static: templates come from <see cref="Strings"/> (resolved against the current UI
/// culture at call time) while numbers and dates use the supplied culture, following the
/// <see cref="ResxAnomalyMessageFormatter"/> precedent. "Overdue" is derived from
/// <c>ReferenceDate &lt; today</c> and is never stored.
/// </remarks>
public static class NotificationTextFormatter
{
    /// <summary>
    /// Formats one notification.
    /// </summary>
    /// <param name="dto">The notification facts.</param>
    /// <param name="today">The current calendar day, used for relative dates and overdue detection.</param>
    /// <param name="culture">Culture used for money, percent and short-date formatting.</param>
    /// <returns>The display title and body.</returns>
    public static (string Title, string Body) Format(NotificationDto dto, DateOnly today, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(culture);

        string label = string.IsNullOrWhiteSpace(dto.SubjectLabel)
            ? Strings.NotificationUnnamedSubject
            : dto.SubjectLabel;

        return dto.Kind switch
        {
            NotificationKind.DebtInstallmentDue => (
                Strings.NotificationDebtDueTitle,
                dto.ReferenceDate < today
                    ? string.Format(culture, Strings.NotificationDebtOverdueFormat, label, Money(dto, culture), ShortDate(dto.ReferenceDate, culture))
                    : string.Format(culture, Strings.NotificationDebtDueFormat, label, Money(dto, culture), RelativeDate(dto.ReferenceDate, today, culture))),
            NotificationKind.RecurringChargeDue => (
                Strings.NotificationRecurringDueTitle,
                string.Format(culture, Strings.NotificationRecurringDueFormat, label, Money(dto, culture), RelativeDate(dto.ReferenceDate, today, culture))),
            NotificationKind.BudgetThreshold => (
                dto.Tier == NotificationTier.Exceeded
                    ? Strings.NotificationBudgetExceededTitle
                    : Strings.NotificationBudgetNearTitle,
                string.Format(culture, Strings.NotificationBudgetFormat, label, (dto.Percent ?? 0m).ToString("0.##", culture))),
            NotificationKind.Anomaly => (
                Strings.NotificationAnomalyTitle,
                string.Format(culture, Strings.NotificationAnomalyFormat, label, ShortDate(dto.ReferenceDate, culture))),
            NotificationKind.Digest => (
                dto.DigestFrequency == DigestFrequency.Daily
                    ? Strings.NotificationDigestDailyTitle
                    : Strings.NotificationDigestWeeklyTitle,
                string.Format(
                    culture,
                    Strings.NotificationDigestFormat,
                    dto.DigestPaymentCount ?? 0,
                    DigestTotals(dto, culture),
                    dto.DigestBudgetCount ?? 0,
                    dto.DigestAnomalyCount ?? 0)),
            _ => (Strings.NotificationsTitle, string.Empty),
        };
    }

    // Per-currency totals joined with " + " (never summed across currencies, D-12). A digest with
    // no payments has no totals, so a localized placeholder avoids rendering empty parentheses.
    private static string DigestTotals(NotificationDto dto, CultureInfo culture)
        => dto.DigestTotals.Count == 0
            ? Strings.NotificationDigestNoTotals
            : string.Join(
                " + ",
                dto.DigestTotals.Select(t => string.Format(culture, "{0:N2} {1}", t.Amount, t.Currency)));

    private static string Money(NotificationDto dto, CultureInfo culture)
        => string.Format(culture, "{0:N2} {1}", dto.Amount ?? 0m, dto.Currency ?? Currency.DOP);

    private static string ShortDate(DateOnly date, CultureInfo culture)
        => date.ToString("d", culture);

    private static string RelativeDate(DateOnly date, DateOnly today, CultureInfo culture)
    {
        int days = date.DayNumber - today.DayNumber;

        return days switch
        {
            0 => Strings.NotificationDateToday,
            1 => Strings.NotificationDateTomorrow,
            > 1 => string.Format(culture, Strings.NotificationDateInDaysFormat, days),
            _ => ShortDate(date, culture),
        };
    }
}
