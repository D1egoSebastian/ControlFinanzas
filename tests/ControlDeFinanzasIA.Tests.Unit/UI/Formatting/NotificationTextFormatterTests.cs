using System.Globalization;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotifications;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.UI.Formatting;
using ControlDeFinanzasIA.UI.Resources;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI.Formatting;

/// <summary>
/// Unit tests for <see cref="NotificationTextFormatter"/> (F-017, R-11, D-10, slice S5a).
/// Expected strings are built from the same resx templates and the same culture, never
/// with hand-typed separators.
/// </summary>
public sealed class NotificationTextFormatterTests
{
    private static readonly DateOnly _today = new(2026, 9, 28);

    private static NotificationDto Dto(
        NotificationKind kind,
        DateOnly referenceDate,
        string label = "Auto",
        decimal? amount = null,
        Currency? currency = null,
        decimal? percent = null,
        NotificationTier? tier = null) =>
        new(
            Guid.NewGuid(),
            kind,
            Guid.NewGuid(),
            referenceDate,
            label,
            amount,
            currency,
            percent,
            tier,
            null,
            null,
            null,
            null,
            Array.Empty<NotificationCurrencyTotal>(),
            false);

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
    public void DebtInstallment_NotYetDue_UsesDueFormatWithRelativeDate(string cultureName)
    {
        CultureInfo culture = UseCulture(cultureName);
        NotificationDto dto = Dto(NotificationKind.DebtInstallmentDue, _today.AddDays(2), "Auto", 1500m, Currency.DOP);

        (string title, string body) = NotificationTextFormatter.Format(dto, _today, culture);

        Assert.Equal(Strings.NotificationDebtDueTitle, title);
        Assert.Equal(
            string.Format(
                culture,
                Strings.NotificationDebtDueFormat,
                "Auto",
                string.Format(culture, "{0:N2} {1}", 1500m, Currency.DOP),
                string.Format(culture, Strings.NotificationDateInDaysFormat, 2)),
            body);
    }

    [Fact]
    public void DebtInstallment_TitleIsLocalized()
    {
        NotificationDto dto = Dto(NotificationKind.DebtInstallmentDue, _today, "Auto", 10m, Currency.DOP);

        CultureInfo es = UseCulture("es-DO");
        string spanish = NotificationTextFormatter.Format(dto, _today, es).Title;
        CultureInfo en = UseCulture("en");
        string english = NotificationTextFormatter.Format(dto, _today, en).Title;

        Assert.Equal("Cuota de deuda por vencer", spanish);
        Assert.Equal("Debt installment due", english);
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public void DebtInstallment_ReferenceDateBeforeToday_UsesOverdueFormatWithShortDate(string cultureName)
    {
        CultureInfo culture = UseCulture(cultureName);
        DateOnly due = _today.AddDays(-3);
        NotificationDto dto = Dto(NotificationKind.DebtInstallmentDue, due, "Auto", 1500m, Currency.USD);

        (_, string body) = NotificationTextFormatter.Format(dto, _today, culture);

        Assert.Equal(
            string.Format(
                culture,
                Strings.NotificationDebtOverdueFormat,
                "Auto",
                string.Format(culture, "{0:N2} {1}", 1500m, Currency.USD),
                due.ToString("d", culture)),
            body);
    }

    [Fact]
    public void DebtInstallment_DueTodayIsNotOverdue()
    {
        CultureInfo culture = UseCulture("en");
        NotificationDto dto = Dto(NotificationKind.DebtInstallmentDue, _today, "Auto", 10m, Currency.DOP);

        (_, string body) = NotificationTextFormatter.Format(dto, _today, culture);

        Assert.Contains(Strings.NotificationDateToday, body, StringComparison.Ordinal);
        Assert.DoesNotContain("overdue", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public void RecurringCharge_TodayTomorrowAndInDays_UseRelativeDateKeys(string cultureName)
    {
        CultureInfo culture = UseCulture(cultureName);

        string Body(int offset) => NotificationTextFormatter
            .Format(Dto(NotificationKind.RecurringChargeDue, _today.AddDays(offset), "Netflix", 9.99m, Currency.USD), _today, culture)
            .Body;

        string money = string.Format(culture, "{0:N2} {1}", 9.99m, Currency.USD);

        Assert.Equal(
            string.Format(culture, Strings.NotificationRecurringDueFormat, "Netflix", money, Strings.NotificationDateToday),
            Body(0));
        Assert.Equal(
            string.Format(culture, Strings.NotificationRecurringDueFormat, "Netflix", money, Strings.NotificationDateTomorrow),
            Body(1));
        Assert.Equal(
            string.Format(
                culture,
                Strings.NotificationRecurringDueFormat,
                "Netflix",
                money,
                string.Format(culture, Strings.NotificationDateInDaysFormat, 3)),
            Body(3));
        Assert.Equal(Strings.NotificationRecurringDueTitle, NotificationTextFormatter
            .Format(Dto(NotificationKind.RecurringChargeDue, _today, "Netflix", 1m, Currency.DOP), _today, culture).Title);
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public void Budget_TierSelectsTitle_AndBodyShowsPercent(string cultureName)
    {
        CultureInfo culture = UseCulture(cultureName);

        (string nearTitle, string nearBody) = NotificationTextFormatter.Format(
            Dto(NotificationKind.BudgetThreshold, _today, "Comida", percent: 85.5m, tier: NotificationTier.Near), _today, culture);
        (string exceededTitle, _) = NotificationTextFormatter.Format(
            Dto(NotificationKind.BudgetThreshold, _today, "Comida", percent: 120m, tier: NotificationTier.Exceeded), _today, culture);

        Assert.Equal(Strings.NotificationBudgetNearTitle, nearTitle);
        Assert.Equal(Strings.NotificationBudgetExceededTitle, exceededTitle);
        Assert.Equal(
            string.Format(culture, Strings.NotificationBudgetFormat, "Comida", 85.5m.ToString("0.##", culture)),
            nearBody);
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public void Anomaly_BodyShowsLabelAndShortDate(string cultureName)
    {
        CultureInfo culture = UseCulture(cultureName);
        DateOnly date = _today.AddDays(-4);

        (string title, string body) = NotificationTextFormatter.Format(
            Dto(NotificationKind.Anomaly, date, "Supermercado"), _today, culture);

        Assert.Equal(Strings.NotificationAnomalyTitle, title);
        Assert.Equal(
            string.Format(culture, Strings.NotificationAnomalyFormat, "Supermercado", date.ToString("d", culture)),
            body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptySubjectLabel_FallsBackToUnnamedSubject(string label)
    {
        CultureInfo culture = UseCulture("en");

        (_, string body) = NotificationTextFormatter.Format(
            Dto(NotificationKind.Anomaly, _today, label), _today, culture);

        Assert.Contains(Strings.NotificationUnnamedSubject, body, StringComparison.Ordinal);
    }

    [Fact]
    public void Money_UsesTwoDecimalsAndCurrencyCode()
    {
        CultureInfo culture = UseCulture("en");
        NotificationDto dto = Dto(NotificationKind.DebtInstallmentDue, _today.AddDays(5), "Auto", 1234.5m, Currency.USD);

        (_, string body) = NotificationTextFormatter.Format(dto, _today, culture);

        Assert.Contains(1234.5m.ToString("N2", culture) + " USD", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_NullArguments_Throw()
    {
        NotificationDto dto = Dto(NotificationKind.Anomaly, _today);

        Assert.Throws<ArgumentNullException>(() => NotificationTextFormatter.Format(null!, _today, CultureInfo.InvariantCulture));
        Assert.Throws<ArgumentNullException>(() => NotificationTextFormatter.Format(dto, _today, null!));
    }

    // --- S7: digest (R-11, D-10, D-12) ---

    private static NotificationDto DigestDto(
        DigestFrequency frequency,
        int payments,
        IReadOnlyList<NotificationCurrencyTotal> totals,
        int budgets,
        int anomalies) =>
        new(
            Guid.NewGuid(),
            NotificationKind.Digest,
            null,
            _today,
            string.Empty,
            null,
            null,
            null,
            null,
            frequency,
            payments,
            budgets,
            anomalies,
            totals,
            false);

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public void Digest_Daily_UsesDailyTitleAndJoinsPerCurrencyTotalsWithPlus(string cultureName)
    {
        CultureInfo culture = UseCulture(cultureName);
        NotificationDto dto = DigestDto(
            DigestFrequency.Daily,
            payments: 3,
            totals: [new NotificationCurrencyTotal(Currency.DOP, 2000m), new NotificationCurrencyTotal(Currency.USD, 20m)],
            budgets: 1,
            anomalies: 2);

        (string title, string body) = NotificationTextFormatter.Format(dto, _today, culture);

        Assert.Equal(Strings.NotificationDigestDailyTitle, title);
        string totals = string.Format(culture, "{0:N2} {1}", 2000m, Currency.DOP)
            + " + "
            + string.Format(culture, "{0:N2} {1}", 20m, Currency.USD);
        Assert.Equal(string.Format(culture, Strings.NotificationDigestFormat, 3, totals, 1, 2), body);
    }

    [Fact]
    public void Digest_Weekly_UsesWeeklyTitle()
    {
        CultureInfo culture = UseCulture("en");
        NotificationDto dto = DigestDto(
            DigestFrequency.Weekly, 1, [new NotificationCurrencyTotal(Currency.DOP, 10m)], 0, 0);

        (string title, _) = NotificationTextFormatter.Format(dto, _today, culture);

        Assert.Equal(Strings.NotificationDigestWeeklyTitle, title);
    }

    [Fact]
    public void Digest_DailyAndWeeklyTitles_AreDistinct()
    {
        UseCulture("en");

        Assert.NotEqual(Strings.NotificationDigestDailyTitle, Strings.NotificationDigestWeeklyTitle);
    }

    [Fact]
    public void Digest_NoPayments_ShowsNoTotalsPlaceholderInsteadOfEmptyParentheses()
    {
        CultureInfo culture = UseCulture("en");
        NotificationDto dto = DigestDto(DigestFrequency.Weekly, 0, [], 2, 0);

        (_, string body) = NotificationTextFormatter.Format(dto, _today, culture);

        Assert.Equal(
            string.Format(culture, Strings.NotificationDigestFormat, 0, Strings.NotificationDigestNoTotals, 2, 0),
            body);
    }

    [Fact]
    public void Digest_TitleIsLocalized()
    {
        NotificationDto dto = DigestDto(DigestFrequency.Weekly, 1, [new NotificationCurrencyTotal(Currency.DOP, 10m)], 0, 0);

        CultureInfo es = UseCulture("es-DO");
        (string esTitle, _) = NotificationTextFormatter.Format(dto, _today, es);
        CultureInfo en = UseCulture("en");
        (string enTitle, _) = NotificationTextFormatter.Format(dto, _today, en);

        Assert.NotEqual(esTitle, enTitle);
    }
}
