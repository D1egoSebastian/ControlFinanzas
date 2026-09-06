using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Views;

/// <summary>
/// Tests para la entidad <see cref="SavedView"/> (F-006, Phase 4).
/// Cubre happy path, invariantes de <see cref="SavedView.Create"/>
/// y <see cref="SavedView.Update"/>.
/// </summary>
public sealed class SavedViewTests
{
    private static readonly DateTime _defaultTimestamp = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    private const string _defaultFilterJson = "{\"schemaVersion\":1,\"type\":\"ExpenseFilter\",\"data\":{}}";

    private static SavedView CreateValidSavedView(
        string? name = null,
        TransactionType? entityType = null,
        string? filterJson = null,
        DateTime? createdAt = null)
        => SavedView.Create(
            name ?? "Transporte",
            entityType ?? TransactionType.Expense,
            filterJson ?? _defaultFilterJson,
            createdAt ?? _defaultTimestamp);

    [Fact]
    public void Create_WithValidArgs_ReturnsSavedView()
    {
        SavedView view = CreateValidSavedView("Transporte", TransactionType.Expense, _defaultFilterJson, _defaultTimestamp);

        Assert.NotEqual(Guid.Empty, view.Id);
        Assert.Equal("Transporte", view.Name);
        Assert.Equal(TransactionType.Expense, view.EntityType);
        Assert.Equal(_defaultFilterJson, view.FilterJson);
        Assert.Equal(1, view.SchemaVersion);
        Assert.Equal(_defaultTimestamp, view.CreatedAt);
        Assert.Equal(_defaultTimestamp, view.UpdatedAt);
    }

    [Fact]
    public void Create_WithNullName_Throws()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => SavedView.Create(null!, TransactionType.Expense, _defaultFilterJson, _defaultTimestamp));

        Assert.Equal("name", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithEmptyName_Throws(string name)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => SavedView.Create(name, TransactionType.Expense, _defaultFilterJson, _defaultTimestamp));

        Assert.Contains("Name", ex.Message);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_WithWhitespaceName_Throws(string name)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => SavedView.Create(name, TransactionType.Expense, _defaultFilterJson, _defaultTimestamp));

        Assert.Contains("Name", ex.Message);
    }

    [Fact]
    public void Create_WithNameOver100Chars_Throws()
    {
        string name = new('x', 101);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => SavedView.Create(name, TransactionType.Expense, _defaultFilterJson, _defaultTimestamp));

        Assert.Contains("Name", ex.Message);
        Assert.Contains("100", ex.Message);
    }

    [Fact]
    public void Create_WithNullFilterJson_Throws()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => SavedView.Create("Transporte", TransactionType.Expense, null!, _defaultTimestamp));

        Assert.Equal("filterJson", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyFilterJson_Throws(string filterJson)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => SavedView.Create("Transporte", TransactionType.Expense, filterJson, _defaultTimestamp));

        Assert.Contains("FilterJson", ex.Message);
    }

    [Fact]
    public void Create_WithDefaultTimestamp_Throws()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => SavedView.Create("Transporte", TransactionType.Expense, _defaultFilterJson, default));

        Assert.Contains("Timestamp", ex.Message);
    }

    [Fact]
    public void Update_WithValidArgs_UpdatesFields()
    {
        SavedView view = CreateValidSavedView("Original");
        Guid originalId = view.Id;
        TransactionType originalEntityType = view.EntityType;
        int originalSchemaVersion = view.SchemaVersion;
        DateTime originalCreatedAt = view.CreatedAt;
        DateTime updatedAt = _defaultTimestamp.AddDays(1);
        string newFilter = "{\"schemaVersion\":1,\"type\":\"ExpenseFilter\",\"data\":{\"page\":2}}";

        view.Update("Renamed", newFilter, updatedAt);

        Assert.Equal(originalId, view.Id);
        Assert.Equal(originalEntityType, view.EntityType);
        Assert.Equal(originalSchemaVersion, view.SchemaVersion);
        Assert.Equal(originalCreatedAt, view.CreatedAt);
        Assert.Equal("Renamed", view.Name);
        Assert.Equal(newFilter, view.FilterJson);
        Assert.Equal(updatedAt, view.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithInvalidName_Throws(string name)
    {
        SavedView view = CreateValidSavedView();

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => view.Update(name, _defaultFilterJson, _defaultTimestamp));

        Assert.Contains("Name", ex.Message);
    }

    [Fact]
    public void Update_WithNullName_ThrowsArgumentNullException()
    {
        SavedView view = CreateValidSavedView();

        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => view.Update(null!, _defaultFilterJson, _defaultTimestamp));

        Assert.Equal("name", ex.ParamName);
    }
}
