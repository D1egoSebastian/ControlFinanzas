using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Recurring;

/// <summary>
/// Pre-1.0 slice C: a recurring charge is defined by the day of the month it always happens on. These tests pin the
/// pure schedule maths: the first charge, the stored anchor start date, and drift-free advancing.
/// </summary>
public sealed class RecurringScheduleTests
{
    [Theory]
    [InlineData("2026-09-20", 25, "2026-09-25")]
    [InlineData("2026-09-20", 20, "2026-09-20")]
    [InlineData("2026-09-20", 11, "2026-10-11")]
    [InlineData("2026-09-20", 31, "2026-09-30")]
    [InlineData("2026-09-30", 31, "2026-09-30")]
    [InlineData("2026-10-01", 31, "2026-10-31")]
    [InlineData("2027-02-10", 30, "2027-02-28")]
    [InlineData("2028-02-10", 30, "2028-02-29")]
    [InlineData("2026-12-20", 5, "2027-01-05")]
    public void FirstCharge_Monthly_IsTheNextTimeTheDayComesAround(string today, int day, string expected)
    {
        DateOnly result = RecurringSchedule.FirstCharge("Monthly", day, null, DateOnly.Parse(today));

        Assert.Equal(DateOnly.Parse(expected), result);
        Assert.Equal(result, RecurringSchedule.FirstCharge("Bimonthly", day, null, DateOnly.Parse(today)));
    }

    [Theory]
    [InlineData("2026-09-20", 3, 15, "2027-03-15")]
    [InlineData("2026-09-20", 9, 20, "2026-09-20")]
    [InlineData("2026-09-20", 12, 1, "2026-12-01")]
    [InlineData("2026-09-20", 2, 29, "2027-02-28")]
    [InlineData("2027-09-20", 2, 29, "2028-02-29")]
    public void FirstCharge_Yearly_UsesTheMonthAndTheDay(string today, int month, int day, string expected) => Assert.Equal(DateOnly.Parse(expected), RecurringSchedule.FirstCharge("Yearly", day, month, DateOnly.Parse(today)));

    [Theory]
    [InlineData("Monthly", 0)]
    [InlineData("Monthly", 32)]
    [InlineData("Yearly", 0)]
    public void FirstCharge_RejectsADayOutsideOneToThirtyOne(string frequency, int day) => Assert.Throws<ArgumentOutOfRangeException>(() => RecurringSchedule.FirstCharge(frequency, day, 1, new DateOnly(2026, 9, 20)));

    [Fact]
    public void FirstCharge_Yearly_RequiresAMonth()
    {
        Assert.Throws<ArgumentException>(() => RecurringSchedule.FirstCharge("Yearly", 5, null, new DateOnly(2026, 9, 20)));
        Assert.Throws<ArgumentOutOfRangeException>(() => RecurringSchedule.FirstCharge("Yearly", 5, 13, new DateOnly(2026, 9, 20)));
    }

    [Theory]
    [InlineData("Monthly", 11, "2026-10-11", "2026-10-11")]
    [InlineData("Monthly", 31, "2026-11-30", "2026-10-31")]
    [InlineData("Monthly", 30, "2027-02-28", "2027-01-30")]
    [InlineData("Monthly", 29, "2027-02-28", "2027-01-29")]
    [InlineData("Monthly", 31, "2026-10-31", "2026-10-31")]
    [InlineData("Bimonthly", 31, "2026-11-30", "2026-07-31")]
    public void AnchorStartDate_CarriesTheChosenDayEvenWhenTheFirstChargeIsClamped(string frequency, int day, string firstCharge, string expected) => Assert.Equal(DateOnly.Parse(expected), RecurringSchedule.AnchorStartDate(frequency, day, null, DateOnly.Parse(firstCharge)));

    [Fact]
    public void AnchorStartDate_Yearly_FebruaryTwentyNinthFallsBackToALeapYear()
    {
        DateOnly start = RecurringSchedule.AnchorStartDate("Yearly", 29, 2, new DateOnly(2027, 2, 28));

        Assert.Equal(new DateOnly(2024, 2, 29), start);
    }

    [Theory]
    [InlineData(30, "2026-02-28", 1, "2026-03-30")]
    [InlineData(29, "2026-02-28", 1, "2026-03-29")]
    [InlineData(31, "2026-02-28", 1, "2026-03-31")]
    [InlineData(30, "2026-01-30", 1, "2026-02-28")]
    [InlineData(31, "2026-04-30", 1, "2026-05-31")]
    [InlineData(15, "2026-12-15", 2, "2027-02-15")]
    [InlineData(29, "2028-02-29", 12, "2029-02-28")]
    public void AddMonthsAnchored_KeepsTheChosenDay_NeverDriftingToTheEndOfTheMonth(int anchor, string from, int months, string expected) => Assert.Equal(DateOnly.Parse(expected), RecurringSchedule.AddMonthsAnchored(anchor, DateOnly.Parse(from), months));

    private static Subscription NewRule(DateOnly start, string frequency = "Monthly") =>
        RecurringRule.Create("Gym", Money.Create(30m, Currency.DOP), Currency.DOP, frequency, start, null, Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void AdvanceAfterGeneration_ADayThirtyRule_ReturnsToTheThirtiethAfterFebruary()
    {
        Subscription rule = NewRule(new DateOnly(2026, 1, 30));

        rule.AdvanceAfterGeneration(new DateOnly(2026, 1, 30), 1);
        Assert.Equal(new DateOnly(2026, 2, 28), rule.NextGenerationDate);
        rule.AdvanceAfterGeneration(new DateOnly(2026, 2, 28), 1);

        Assert.Equal(new DateOnly(2026, 3, 30), rule.NextGenerationDate);
    }

    [Fact]
    public void CalculateOccurrences_ADayThirtyRule_NeverDriftsToTheThirtyFirst()
    {
        var calculator = new RecurringOccurrenceCalculator();
        Subscription rule = NewRule(new DateOnly(2026, 1, 30));

        IReadOnlyList<DateOnly> dates = calculator.CalculateOccurrences(rule, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 31));

        Assert.Equal(
            [new DateOnly(2026, 1, 30), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 30), new DateOnly(2026, 4, 30), new DateOnly(2026, 5, 30)],
            dates);
    }

    [Fact]
    public void CalculateOccurrences_ALegacyRuleStartedOnTheLastDayOfFebruary_KeepsTheTwentyEighth()
    {
        var calculator = new RecurringOccurrenceCalculator();
        Subscription rule = NewRule(new DateOnly(2026, 2, 28));

        IReadOnlyList<DateOnly> dates = calculator.CalculateOccurrences(rule, new DateOnly(2026, 2, 1), new DateOnly(2026, 4, 30));

        Assert.Equal([new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 28), new DateOnly(2026, 4, 28)], dates);
    }

    [Fact]
    public void CalculateOccurrences_ARuleWhoseStartIsBeforeItsFirstCharge_OnlyEmitsTheAnchoredDays()
    {
        var calculator = new RecurringOccurrenceCalculator();
        DateOnly start = RecurringSchedule.AnchorStartDate("Monthly", 31, null, new DateOnly(2026, 11, 30));
        Subscription rule = RecurringRule.Create("Rent", Money.Create(900m, Currency.DOP), Currency.DOP, "Monthly", start, null, Guid.NewGuid(), Guid.NewGuid(), null, new DateOnly(2026, 11, 30));

        IReadOnlyList<DateOnly> dates = calculator.CalculateOccurrences(rule, new DateOnly(2026, 11, 1), new DateOnly(2027, 2, 28));

        Assert.Equal(new DateOnly(2026, 11, 30), rule.NextGenerationDate);
        Assert.Equal([new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28)], dates);
    }
}
