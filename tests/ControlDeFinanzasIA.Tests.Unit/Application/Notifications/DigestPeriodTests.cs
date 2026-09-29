using ControlDeFinanzasIA.Application.Notifications;
using ControlDeFinanzasIA.Domain.Notifications;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Notifications;

/// <summary>
/// Tests for <see cref="DigestPeriod"/> (F-017, R-13, D-12): the period start and the
/// forward-only content window of the periodic digest.
/// </summary>
public sealed class DigestPeriodTests
{
    [Fact]
    public void Start_Daily_ReturnsToday()
    {
        var today = new DateOnly(2026, 9, 26);

        Assert.Equal(today, DigestPeriod.Start(DigestFrequency.Daily, today));
    }

    [Theory]
    [InlineData(2026, 9, 28, 2026, 9, 28)] // Monday -> itself
    [InlineData(2026, 9, 29, 2026, 9, 28)] // Tuesday
    [InlineData(2026, 10, 2, 2026, 9, 28)] // Friday
    [InlineData(2026, 10, 4, 2026, 9, 28)] // Sunday belongs to the week that started on the previous Monday
    [InlineData(2026, 9, 27, 2026, 9, 21)] // Sunday, previous week
    [InlineData(2026, 1, 1, 2025, 12, 29)] // Year boundary: Thursday -> Monday of the prior year
    public void Start_Weekly_ReturnsTheMondayOfTheIsoWeek(
        int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay)
    {
        DateOnly result = DigestPeriod.Start(DigestFrequency.Weekly, new DateOnly(year, month, day));

        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), result);
    }

    [Fact]
    public void Start_Off_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DigestPeriod.Start(DigestFrequency.Off, new DateOnly(2026, 9, 26)));
    }

    [Fact]
    public void WindowEnd_Daily_IsToday()
    {
        var today = new DateOnly(2026, 9, 26);

        Assert.Equal(today, DigestPeriod.WindowEnd(DigestFrequency.Daily, today));
    }

    [Fact]
    public void WindowEnd_Weekly_IsTodayPlusSixDays()
    {
        var today = new DateOnly(2026, 9, 26);

        Assert.Equal(new DateOnly(2026, 10, 2), DigestPeriod.WindowEnd(DigestFrequency.Weekly, today));
    }

    [Fact]
    public void WindowEnd_Off_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DigestPeriod.WindowEnd(DigestFrequency.Off, new DateOnly(2026, 9, 26)));
    }
}
