using System.Globalization;
using Avalonia.Data;
using ControlDeFinanzasIA.UI.Converters;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>Tests for <see cref="DateOnlyConverter"/>, shared by every date picker bound to a view-model date.</summary>
public sealed class DateOnlyConverterTests
{
    private static readonly DateOnlyConverter _converter = new();
    private static readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_DateOnly_BecomesTheMidnightOfThatDay()
    {
        object? result = _converter.Convert(new DateOnly(2026, 3, 5), typeof(DateTimeOffset?), null, _culture);

        Assert.Equal(new DateTime(2026, 3, 5), Assert.IsType<DateTimeOffset>(result).DateTime);
    }

    [Fact]
    public void Convert_DateTime_BecomesTheSameDay()
    {
        object? result = _converter.Convert(new DateTime(2026, 3, 5), typeof(DateTimeOffset?), null, _culture);

        Assert.Equal(new DateTime(2026, 3, 5), Assert.IsType<DateTimeOffset>(result).DateTime);
    }

    [Fact]
    public void Convert_Null_StaysNull() => Assert.Null(_converter.Convert(null, typeof(DateTimeOffset?), null, _culture));

    [Theory]
    [InlineData(typeof(DateOnly))]
    [InlineData(typeof(DateOnly?))]
    public void ConvertBack_ToADateOnlyProperty_ReturnsTheDay(Type target)
    {
        object? result = _converter.ConvertBack(new DateTimeOffset(new DateTime(2026, 4, 6, 13, 30, 0)), target, null, _culture);

        Assert.Equal(new DateOnly(2026, 4, 6), result);
    }

    [Theory]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(DateTime?))]
    public void ConvertBack_ToADateTimeProperty_ReturnsADateTimeNotADateOnly(Type target)
    {
        object? result = _converter.ConvertBack(new DateTimeOffset(new DateTime(2026, 4, 6, 13, 30, 0)), target, null, _culture);

        Assert.Equal(new DateTime(2026, 4, 6, 13, 30, 0), Assert.IsType<DateTime>(result));
    }

    [Fact]
    public void ConvertBack_Null_StaysNull() => Assert.Null(_converter.ConvertBack(null, typeof(DateOnly?), null, _culture));

    [Fact]
    public void ConvertBack_UnsupportedValue_ReportsABindingError() => Assert.IsType<BindingNotification>(_converter.ConvertBack("nope", typeof(DateOnly?), null, _culture));
}
