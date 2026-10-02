using ControlDeFinanzasIA.UI.ViewModels;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>A day with nothing spent shows only its number: no "0.00 DOP" noise, in the cell or in its accessible name.</summary>
public sealed class CalendarCellSpendTests
{
    private static readonly DateOnly _day = new(2026, 10, 14);

    [Fact]
    public void ADayWithSpend_ShowsItsTotal_AndNamesIt()
    {
        var cell = new CalendarCell(_day, "14", "≈ 1,200.00 DOP", IsCurrentMonth: true, IsToday: false, HasSpend: true);

        Assert.True(cell.HasSpend);
        Assert.EndsWith("≈ 1,200.00 DOP", cell.AutomationName, StringComparison.Ordinal);
    }

    [Fact]
    public void ADayWithoutSpend_HidesTheTotal_AndLeavesItOutOfItsName()
    {
        var cell = new CalendarCell(_day, "14", "≈ 0.00 DOP", IsCurrentMonth: true, IsToday: false, HasSpend: false);

        Assert.DoesNotContain("0.00", cell.AutomationName, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDefault_IsToShowTheTotal()
    {
        var cell = new CalendarCell(_day, "14", "≈ 5.00 DOP", true, false);

        Assert.True(cell.HasSpend);
    }
}
