using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Recurring;

/// <summary>
/// Tests para <see cref="RecurringOccurrenceCalculator"/>. Cubre los
/// escenarios de borde mas comunes: fin de mes, a~no bisiesto,
/// rango vacio, regla inactiva, frecuencias multiples.
/// </summary>
public sealed class RecurringOccurrenceCalculatorTests
{
    private static readonly RecurringOccurrenceCalculator _calculator = new();

    private static RecurringRule CreateRule(
        DateOnly startDate,
        string frequency = "Monthly",
        DateOnly? endDate = null,
        bool active = true)
    {
        return new Subscription(
            Id: Guid.NewGuid(),
            Name: "Test Rule",
            Amount: Money.Create(100m, Currency.DOP),
            Currency: Currency.DOP,
            Frequency: frequency,
            StartDate: startDate,
            EndDate: endDate,
            CategoryId: Guid.NewGuid(),
            PaymentMethodId: Guid.NewGuid(),
            TagIds: Array.Empty<Guid>(),
            Active: active,
            NextGenerationDate: startDate);
    }

    [Fact]
    public void CalculateOccurrences_MonthlyRule_ReturnsExpectedDates()
    {
        RecurringRule rule = CreateRule(new DateOnly(2026, 1, 15));
        IReadOnlyList<DateOnly> dates = _calculator.CalculateOccurrences(
            rule,
            from: new DateOnly(2026, 1, 1),
            to: new DateOnly(2026, 5, 31));

        Assert.Equal(5, dates.Count);
        Assert.Equal(new DateOnly(2026, 1, 15), dates[0]);
        Assert.Equal(new DateOnly(2026, 2, 15), dates[1]);
        Assert.Equal(new DateOnly(2026, 3, 15), dates[2]);
        Assert.Equal(new DateOnly(2026, 4, 15), dates[3]);
        Assert.Equal(new DateOnly(2026, 5, 15), dates[4]);
    }

    [Fact]
    public void CalculateOccurrences_FromAfterStart_AlignsToFirstFutureDate()
    {
        RecurringRule rule = CreateRule(new DateOnly(2026, 1, 15));
        IReadOnlyList<DateOnly> dates = _calculator.CalculateOccurrences(
            rule,
            from: new DateOnly(2026, 3, 1),
            to: new DateOnly(2026, 6, 30));

        Assert.Equal(new DateOnly(2026, 3, 15), dates[0]);
        Assert.Equal(new DateOnly(2026, 4, 15), dates[1]);
        Assert.Equal(new DateOnly(2026, 5, 15), dates[2]);
        Assert.Equal(new DateOnly(2026, 6, 15), dates[3]);
    }

    [Fact]
    public void CalculateOccurrences_BeforeStart_ReturnsEmpty()
    {
        RecurringRule rule = CreateRule(new DateOnly(2026, 12, 1));
        IReadOnlyList<DateOnly> dates = _calculator.CalculateOccurrences(
            rule,
            from: new DateOnly(2026, 1, 1),
            to: new DateOnly(2026, 6, 30));

        Assert.Empty(dates);
    }

    [Fact]
    public void CalculateOccurrences_AfterEndDate_ReturnsEmpty()
    {
        RecurringRule rule = CreateRule(
            startDate: new DateOnly(2026, 1, 15),
            endDate: new DateOnly(2026, 3, 15));
        IReadOnlyList<DateOnly> dates = _calculator.CalculateOccurrences(
            rule,
            from: new DateOnly(2026, 4, 1),
            to: new DateOnly(2026, 6, 30));

        Assert.Empty(dates);
    }

    [Fact]
    public void CalculateOccurrences_InactiveRule_ReturnsEmpty()
    {
        RecurringRule rule = CreateRule(new DateOnly(2026, 1, 15), active: false);
        IReadOnlyList<DateOnly> dates = _calculator.CalculateOccurrences(
            rule,
            from: new DateOnly(2026, 1, 1),
            to: new DateOnly(2026, 12, 31));

        Assert.Empty(dates);
    }

    [Fact]
    public void CalculateOccurrences_RangeBeforeStart_ReturnsEmpty()
    {
        RecurringRule rule = CreateRule(new DateOnly(2026, 6, 1));
        IReadOnlyList<DateOnly> dates = _calculator.CalculateOccurrences(
            rule,
            from: new DateOnly(2026, 1, 1),
            to: new DateOnly(2026, 5, 31));

        Assert.Empty(dates);
    }

    [Fact]
    public void CalculateOccurrences_DayClampsToEndOfMonth_31ToFebruary()
    {
        RecurringRule rule = CreateRule(new DateOnly(2026, 1, 31));
        IReadOnlyList<DateOnly> dates = _calculator.CalculateOccurrences(
            rule,
            from: new DateOnly(2026, 1, 1),
            to: new DateOnly(2026, 4, 30));

        Assert.Equal(new DateOnly(2026, 1, 31), dates[0]);
        Assert.Equal(new DateOnly(2026, 2, 28), dates[1]);
        Assert.Equal(new DateOnly(2026, 3, 31), dates[2]);
        Assert.Equal(new DateOnly(2026, 4, 30), dates[3]);
    }

    [Fact]
    public void CalculateOccurrences_31InLeapYear_ClampsToFeb29()
    {
        RecurringRule rule = CreateRule(new DateOnly(2024, 1, 31));
        IReadOnlyList<DateOnly> dates = _calculator.CalculateOccurrences(
            rule,
            from: new DateOnly(2024, 1, 1),
            to: new DateOnly(2024, 3, 31));

        Assert.Equal(new DateOnly(2024, 1, 31), dates[0]);
        Assert.Equal(new DateOnly(2024, 2, 29), dates[1]);
        Assert.Equal(new DateOnly(2024, 3, 31), dates[2]);
    }

    [Fact]
    public void CalculateOccurrences_BimonthlyRule_SkipsOneMonth()
    {
        RecurringRule rule = CreateRule(new DateOnly(2026, 1, 10), frequency: "Bimonthly");
        IReadOnlyList<DateOnly> dates = _calculator.CalculateOccurrences(
            rule,
            from: new DateOnly(2026, 1, 1),
            to: new DateOnly(2026, 7, 31));

        Assert.Equal(4, dates.Count);
        Assert.Equal(new DateOnly(2026, 1, 10), dates[0]);
        Assert.Equal(new DateOnly(2026, 3, 10), dates[1]);
        Assert.Equal(new DateOnly(2026, 5, 10), dates[2]);
        Assert.Equal(new DateOnly(2026, 7, 10), dates[3]);
    }

    [Fact]
    public void CalculateOccurrences_YearlyRule_ReturnsOnePerYear()
    {
        RecurringRule rule = CreateRule(new DateOnly(2026, 3, 15), frequency: "Yearly");
        IReadOnlyList<DateOnly> dates = _calculator.CalculateOccurrences(
            rule,
            from: new DateOnly(2026, 1, 1),
            to: new DateOnly(2029, 12, 31));

        Assert.Equal(4, dates.Count);
        Assert.Equal(new DateOnly(2026, 3, 15), dates[0]);
        Assert.Equal(new DateOnly(2027, 3, 15), dates[1]);
        Assert.Equal(new DateOnly(2028, 3, 15), dates[2]);
        Assert.Equal(new DateOnly(2029, 3, 15), dates[3]);
    }

    [Fact]
    public void CalculateOccurrences_StartDateExactlyAtFrom_IncludesStart()
    {
        RecurringRule rule = CreateRule(new DateOnly(2026, 1, 15));
        IReadOnlyList<DateOnly> dates = _calculator.CalculateOccurrences(
            rule,
            from: new DateOnly(2026, 1, 15),
            to: new DateOnly(2026, 3, 31));

        Assert.Equal(3, dates.Count);
        Assert.Equal(new DateOnly(2026, 1, 15), dates[0]);
    }

    [Fact]
    public void CalculateOccurrences_RangeCrossesEndDate_StopsAtEndDate()
    {
        RecurringRule rule = CreateRule(
            startDate: new DateOnly(2026, 1, 15),
            endDate: new DateOnly(2026, 4, 15));
        IReadOnlyList<DateOnly> dates = _calculator.CalculateOccurrences(
            rule,
            from: new DateOnly(2026, 1, 1),
            to: new DateOnly(2026, 7, 31));

        Assert.Equal(4, dates.Count);
        Assert.Equal(new DateOnly(2026, 4, 15), dates[3]);
    }

    [Fact]
    public void CalculateOccurrences_ToBeforeFrom_ReturnsEmpty()
    {
        RecurringRule rule = CreateRule(new DateOnly(2026, 1, 15));
        IReadOnlyList<DateOnly> dates = _calculator.CalculateOccurrences(
            rule,
            from: new DateOnly(2026, 6, 1),
            to: new DateOnly(2026, 3, 31));

        Assert.Empty(dates);
    }

    [Fact]
    public void NextGenerationDate_AfterGeneration_AdvancesByInterval()
    {
        RecurringRule rule = CreateRule(new DateOnly(2026, 1, 15));
        DateOnly next = _calculator.NextGenerationDate(rule, new DateOnly(2026, 1, 15));
        Assert.Equal(new DateOnly(2026, 2, 15), next);
    }

    [Fact]
    public void NextGenerationDate_AfterBimonthlyGeneration_AdvancesTwoMonths()
    {
        RecurringRule rule = CreateRule(new DateOnly(2026, 1, 10), frequency: "Bimonthly");
        DateOnly next = _calculator.NextGenerationDate(rule, new DateOnly(2026, 3, 10));
        Assert.Equal(new DateOnly(2026, 5, 10), next);
    }

    [Fact]
    public void NextGenerationDate_NeverGenerated_ReturnsStartDate()
    {
        RecurringRule rule = CreateRule(new DateOnly(2026, 1, 15));
        DateOnly? next = _calculator.NextGenerationDate(rule, lastGeneratedDate: null);
        Assert.Equal(new DateOnly(2026, 1, 15), next);
    }
}
