using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Tests.Unit.ValueObjects;

/// <summary>
/// Tests para el value object <see cref="DateRange"/> (F-003, Phase 1).
/// Cubre AC-1 (factory y validación) y AC-6 (equality) de la spec F-003.
/// </summary>
public class DateRangeTests
{
    // ============================================================
    // AC-1: Factory y validación
    // ============================================================

    [Fact]
    public void Create_WithStartLessThanEnd_ReturnsValidRange()
    {
        var start = new DateOnly(2026, 1, 5);
        var end = new DateOnly(2026, 1, 10);

        var range = DateRange.Create(start, end);

        Assert.Equal(start, range.Start);
        Assert.Equal(end, range.End);
    }

    [Fact]
    public void Create_WithStartEqualToEnd_ReturnsSingleDayRange()
    {
        var date = new DateOnly(2026, 1, 5);

        var range = DateRange.Create(date, date);

        Assert.Equal(date, range.Start);
        Assert.Equal(date, range.End);
    }

    [Fact]
    public void Create_WithStartGreaterThanEnd_ThrowsArgumentException()
    {
        var start = new DateOnly(2026, 1, 10);
        var end = new DateOnly(2026, 1, 5);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => DateRange.Create(start, end));

        Assert.Contains("Start", ex.Message);
        Assert.Contains("End", ex.Message);
    }

    // ============================================================
    // AC-2: Contains (semántica inclusiva)
    // ============================================================

    [Fact]
    public void Contains_DateInsideRange_ReturnsTrue()
    {
        var range = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));

        Assert.True(range.Contains(new DateOnly(2026, 1, 7)));
    }

    [Fact]
    public void Contains_DateEqualToStart_ReturnsTrue()
    {
        var range = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));

        Assert.True(range.Contains(new DateOnly(2026, 1, 5)));
    }

    [Fact]
    public void Contains_DateEqualToEnd_ReturnsTrue()
    {
        var range = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));

        Assert.True(range.Contains(new DateOnly(2026, 1, 10)));
    }

    [Fact]
    public void Contains_DateBeforeStart_ReturnsFalse()
    {
        var range = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));

        Assert.False(range.Contains(new DateOnly(2026, 1, 4)));
    }

    [Fact]
    public void Contains_DateAfterEnd_ReturnsFalse()
    {
        var range = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));

        Assert.False(range.Contains(new DateOnly(2026, 1, 11)));
    }

    [Fact]
    public void Contains_SingleDayRange_ContainsOnlyThatDay()
    {
        var range = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5));

        Assert.True(range.Contains(new DateOnly(2026, 1, 5)));
        Assert.False(range.Contains(new DateOnly(2026, 1, 4)));
        Assert.False(range.Contains(new DateOnly(2026, 1, 6)));
    }

    // ============================================================
    // AC-3: Overlaps (con null check)
    // ============================================================

    [Fact]
    public void Overlaps_TwoOverlappingRanges_ReturnsTrue()
    {
        var a = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        var b = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 15));

        Assert.True(a.Overlaps(b));
        Assert.True(b.Overlaps(a));
    }

    [Fact]
    public void Overlaps_TwoDisjointRanges_ReturnsFalse()
    {
        var a = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
        var b = DateRange.Create(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 15));

        Assert.False(a.Overlaps(b));
        Assert.False(b.Overlaps(a));
    }

    [Fact]
    public void Overlaps_AdjacentRanges_ReturnsTrue()
    {
        // RN-5: rangos adyacentes (uno termina el día que el otro empieza)
        // comparten ese día y se solapan.
        var a = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
        var b = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));

        Assert.True(a.Overlaps(b));
        Assert.True(b.Overlaps(a));
    }

    [Fact]
    public void Overlaps_OneContainsTheOther_ReturnsTrue()
    {
        var outer = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var inner = DateRange.Create(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 20));

        Assert.True(outer.Overlaps(inner));
        Assert.True(inner.Overlaps(outer));
    }

    [Fact]
    public void Overlaps_IdenticalRanges_ReturnsTrue()
    {
        var a = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        var b = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));

        Assert.True(a.Overlaps(b));
    }

    [Fact]
    public void Overlaps_Null_ThrowsArgumentNullException()
    {
        var range = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));

        Assert.Throws<ArgumentNullException>(() => range.Overlaps(null!));
    }

    // ============================================================
    // AC-4: DurationInDays (conteo inclusivo)
    // ============================================================

    [Fact]
    public void DurationInDays_SingleDayRange_ReturnsOne()
    {
        var range = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5));

        Assert.Equal(1, range.DurationInDays());
    }

    [Fact]
    public void DurationInDays_TwoDayRange_ReturnsTwo()
    {
        var range = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2));

        Assert.Equal(2, range.DurationInDays());
    }

    [Fact]
    public void DurationInDays_FullJanuary_ReturnsThirtyOne()
    {
        var range = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(31, range.DurationInDays());
    }

    [Fact]
    public void DurationInDays_FullLeapYear_ReturnsThreeHundredSixtySix()
    {
        var range = DateRange.Create(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31));

        Assert.Equal(366, range.DurationInDays());
    }

    [Fact]
    public void DurationInDays_DoesNotMutate()
    {
        var range = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        DateOnly originalStart = range.Start;
        DateOnly originalEnd = range.End;

        _ = range.DurationInDays();

        Assert.Equal(originalStart, range.Start);
        Assert.Equal(originalEnd, range.End);
    }

    // ============================================================
    // AC-5: Intersect (con null check)
    // ============================================================

    [Fact]
    public void Intersect_TwoOverlappingRanges_ReturnsIntersection()
    {
        var a = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        var b = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 15));

        DateRange? result = a.Intersect(b);

        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 1, 5), result.Start);
        Assert.Equal(new DateOnly(2026, 1, 10), result.End);
    }

    [Fact]
    public void Intersect_DisjointRanges_ReturnsNull()
    {
        var a = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
        var b = DateRange.Create(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 15));

        DateRange? result = a.Intersect(b);

        Assert.Null(result);
    }

    [Fact]
    public void Intersect_OneContainsTheOther_ReturnsInner()
    {
        var outer = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var inner = DateRange.Create(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 20));

        DateRange? result = outer.Intersect(inner);

        Assert.NotNull(result);
        Assert.Equal(inner.Start, result.Start);
        Assert.Equal(inner.End, result.End);
    }

    [Fact]
    public void Intersect_IdenticalRanges_ReturnsSameRange()
    {
        var a = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        var b = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));

        DateRange? result = a.Intersect(b);

        Assert.NotNull(result);
        Assert.Equal(a.Start, result.Start);
        Assert.Equal(a.End, result.End);
    }

    [Fact]
    public void Intersect_AdjacentRanges_ReturnsSingleDay()
    {
        // RN-5: rangos adyacentes (uno termina el día que el otro empieza)
        // comparten ese día. La intersección es un rango de un solo día.
        var a = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
        var b = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));

        DateRange? result = a.Intersect(b);

        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 1, 5), result.Start);
        Assert.Equal(new DateOnly(2026, 1, 5), result.End);
    }

    [Fact]
    public void Intersect_Null_ThrowsArgumentNullException()
    {
        var range = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));

        Assert.Throws<ArgumentNullException>(() => range.Intersect(null!));
    }

    // ============================================================
    // AC-6: Equality (proporcionada por record automáticamente)
    // ============================================================

    [Fact]
    public void Equality_SameStartSameEnd_AreEqual()
    {
        var a = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));
        var b = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));

        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.False(a != b);
    }

    [Fact]
    public void Equality_DifferentStartSameEnd_AreNotEqual()
    {
        var a = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));
        var b = DateRange.Create(new DateOnly(2026, 1, 6), new DateOnly(2026, 1, 10));

        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equality_SameStartDifferentEnd_AreNotEqual()
    {
        var a = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));
        var b = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11));

        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void GetHashCode_SameStartSameEnd_ReturnsSameHash()
    {
        var a = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));
        var b = DateRange.Create(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
