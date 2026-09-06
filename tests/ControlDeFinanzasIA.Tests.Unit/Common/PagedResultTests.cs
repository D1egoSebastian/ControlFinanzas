using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Tests.Unit.Common;

/// <summary>
/// Tests para <see cref="PagedResult{T}"/>. Cubre los criterios
/// AC-5 del spec F-005.
/// </summary>
public class PagedResultTests
{
    [Fact]
    public void PagedResult_Constructor_StoresAllProperties()
    {
        int[] items = [1, 2, 3];
        PagedResult<int> result = new(items, 10, 1, 3);

        Assert.Equal(items, result.Items);
        Assert.Equal(10, result.Total);
        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.PageSize);
    }

    [Fact]
    public void PagedResult_TotalPages_ComputesCorrectly()
    {
        // Caso típico: 100 items, 10 por página = 10 páginas
        PagedResult<int> r1 = new(Array.Empty<int>(), 100, 1, 10);
        Assert.Equal(10, r1.TotalPages);

        // Caso con remainder: 95 items, 10 por página = 10 páginas
        // (no 9.5, usa ceiling division)
        PagedResult<int> r2 = new(Array.Empty<int>(), 95, 1, 10);
        Assert.Equal(10, r2.TotalPages);

        // Exacto: 50 items, 5 por página = 10 páginas
        PagedResult<int> r3 = new(Array.Empty<int>(), 50, 1, 5);
        Assert.Equal(10, r3.TotalPages);
    }

    [Fact]
    public void PagedResult_TotalPages_WithZeroItems_IsZero()
    {
        PagedResult<int> result = new(Array.Empty<int>(), 0, 1, 10);

        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void PagedResult_TotalPages_WithZeroPageSize_IsZero()
    {
        // Defensive: PageSize = 0 retorna 0 sin DivideByZeroException
        PagedResult<int> result = new(Array.Empty<int>(), 100, 1, 0);

        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void PagedResult_TwoSameResults_AreEqual()
    {
        // Misma referencia de array: la record equality usa reference
        // equality para IReadOnlyList<T> (arrays), por lo que se
        // requiere la misma instancia del array.
        int[] items = [1, 2];
        PagedResult<int> r1 = new(items, 10, 1, 2);
        PagedResult<int> r2 = new(items, 10, 1, 2);

        Assert.Equal(r1, r2);
    }

    [Fact]
    public void PagedResult_DifferentPageNumbers_AreNotEqual()
    {
        int[] items = [1, 2];
        PagedResult<int> r1 = new(items, 10, 1, 2);
        PagedResult<int> r2 = new(items, 10, 2, 2);

        Assert.NotEqual(r1, r2);
    }

    [Fact]
    public void PagedResult_ItemsIsReadOnlyList()
    {
        int[] items = [1, 2, 3];
        PagedResult<int> result = new(items, 10, 1, 3);

        // Items es IReadOnlyList<T>, no IList<T>
        Assert.IsAssignableFrom<IReadOnlyList<int>>(result.Items);
    }

    [Fact]
    public void PagedResult_EmptyList_HasZeroItems()
    {
        PagedResult<int> result = new(Array.Empty<int>(), 0, 1, 10);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalPages);
    }
}
