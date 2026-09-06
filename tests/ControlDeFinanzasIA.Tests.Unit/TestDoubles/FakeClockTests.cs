using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Tests para <see cref="FakeClock"/>. Verifican que el fake
/// permite controlar la hora y la fecha explícitamente, y que
/// retorna exactamente lo que se le setea.
/// </summary>
public class FakeClockTests
{
    [Fact]
    public void FakeClock_DefaultConstruction_ReturnsDefaultValues()
    {
        var fake = new FakeClock();

        Assert.Equal(default(DateTime), fake.UtcNow);
        Assert.Equal(default(DateOnly), fake.Today);
    }

    [Fact]
    public void FakeClock_SetUtcNow_ReturnsSetValue()
    {
        var fake = new FakeClock();
        var expected = new DateTime(2026, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        fake.UtcNow = expected;

        Assert.Equal(expected, fake.UtcNow);
    }

    [Fact]
    public void FakeClock_SetToday_ReturnsSetValue()
    {
        var fake = new FakeClock();
        var expected = new DateOnly(2026, 6, 15);

        fake.Today = expected;

        Assert.Equal(expected, fake.Today);
    }

    [Fact]
    public void FakeClock_SetDateTimeMinValue_ReturnsMinValue()
    {
        var fake = new FakeClock();

        fake.UtcNow = DateTime.MinValue;

        Assert.Equal(DateTime.MinValue, fake.UtcNow);
    }

    [Fact]
    public void FakeClock_SetDateTimeMaxValue_ReturnsMaxValue()
    {
        var fake = new FakeClock();

        fake.UtcNow = DateTime.MaxValue;

        Assert.Equal(DateTime.MaxValue, fake.UtcNow);
    }

    [Fact]
    public void FakeClock_SetDateOnlyMinAndMax_ReturnsCorrectValues()
    {
        var fake = new FakeClock();

        fake.Today = DateOnly.MinValue;
        Assert.Equal(DateOnly.MinValue, fake.Today);

        fake.Today = DateOnly.MaxValue;
        Assert.Equal(DateOnly.MaxValue, fake.Today);
    }

    [Fact]
    public void FakeClock_SetKindToUtc_PreservesUtcKind()
    {
        var fake = new FakeClock();
        var expected = new DateTime(2026, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        fake.UtcNow = expected;

        Assert.Equal(DateTimeKind.Utc, fake.UtcNow.Kind);
    }
}
