using ControlDeFinanzasIA.Domain.Catalog;

namespace ControlDeFinanzasIA.Tests.Unit.Domain;

/// <summary>
/// Tests para la entidad <see cref="Tag"/> (F-004, Phase 4).
/// Cubre happy path, invariantes de <see cref="Tag.Create"/>,
/// <see cref="Tag.Update"/> e igualdad por referencia.
/// </summary>
public sealed class TagTests
{
    private static readonly DateTime _defaultCreatedAt = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    private static Tag CreateValidTag(string? name = null, string? color = null, DateTime? createdAt = null)
        => Tag.Create(name ?? "Trabajo", color, createdAt ?? _defaultCreatedAt);

    // ============================================================
    // T-1 a T-8: Create
    // ============================================================

    [Fact]
    public void Create_WithValidArgs_ReturnsTagWithExpectedState()
    {
        Tag tag = CreateValidTag("Comida", "#FF8800");

        Assert.NotEqual(Guid.Empty, tag.Id);
        Assert.Equal("Comida", tag.Name);
        Assert.Equal("#FF8800", tag.Color);
        Assert.Equal(_defaultCreatedAt, tag.CreatedAt);
    }

    [Fact]
    public void Create_WithNullName_ThrowsArgumentNullException()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => Tag.Create(null!, null, _defaultCreatedAt));

        Assert.Equal("name", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_WithEmptyOrWhitespaceName_ThrowsArgumentException(string name)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Tag.Create(name, null, _defaultCreatedAt));

        Assert.Contains("Name", ex.Message);
    }

    [Fact]
    public void Create_WithNameOverMaxLength_ThrowsArgumentException()
    {
        string name = new('a', 51);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Tag.Create(name, null, _defaultCreatedAt));

        Assert.Contains("Name", ex.Message);
        Assert.Contains("50", ex.Message);
    }

    [Fact]
    public void Create_WithColorOverMaxLength_ThrowsArgumentException()
    {
        string color = new('a', 10);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Tag.Create("X", color, _defaultCreatedAt));

        Assert.Contains("Color", ex.Message);
        Assert.Contains("9", ex.Message);
    }

    [Fact]
    public void Create_WithNullColor_Succeeds()
    {
        var tag = Tag.Create("X", null, _defaultCreatedAt);

        Assert.Null(tag.Color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_WithWhitespaceColor_NormalizesToNull(string color)
    {
        var tag = Tag.Create("X", color, _defaultCreatedAt);

        Assert.Null(tag.Color);
    }

    [Fact]
    public void Create_WithDefaultCreatedAt_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Tag.Create("X", null, default));

        Assert.Contains("CreatedAt", ex.Message);
    }

    // ============================================================
    // T-9 a T-11: Update
    // ============================================================

    [Fact]
    public void Update_WithValidArgs_RefreshesNameAndColor_PreservesIdAndCreatedAt()
    {
        Tag tag = CreateValidTag("Original", null);
        Guid originalId = tag.Id;
        DateTime originalCreatedAt = tag.CreatedAt;

        tag.Update("Actualizado", "#00AAFF");

        Assert.Equal(originalId, tag.Id);
        Assert.Equal(originalCreatedAt, tag.CreatedAt);
        Assert.Equal("Actualizado", tag.Name);
        Assert.Equal("#00AAFF", tag.Color);
    }

    [Theory]
    [InlineData("   ", null)]
    [InlineData("X", "0000000000")]
    public void Update_WithInvalidArgs_ThrowsArgumentException(string name, string? color)
    {
        Tag tag = CreateValidTag();

        Assert.Throws<ArgumentException>(() => tag.Update(name, color));
    }

    [Fact]
    public void Update_WithWhitespaceColor_NormalizesToNull()
    {
        Tag tag = CreateValidTag("Original", "#FF8800");

        tag.Update("Actualizado", "   ");

        Assert.Null(tag.Color);
    }

    // ============================================================
    // T-12: Igualdad por referencia
    // ============================================================

    [Fact]
    public void Equality_ByReference_TwoInstancesNotEqual()
    {
        Tag a = CreateValidTag("X", null);
        Tag b = CreateValidTag("X", null);

        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
    }
}
