using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Tests.Unit.Domain;

/// <summary>
/// Tests para la entidad <see cref="Income"/> (F-001, Phase 4).
/// Cubre happy path, invariantes de <see cref="Income.Create"/>,
/// <see cref="Income.Update"/>, igualdad por referencia e
/// inmutabilidad.
/// </summary>
public sealed class IncomeTests
{
    private static readonly Guid _defaultCategoryId = Guid.NewGuid();
    private static readonly DateOnly _defaultDate = new(2026, 1, 15);
    private static readonly Money _defaultAmount = Money.Create(1500.50m, Currency.DOP);
    private static readonly string _defaultDescription = "Sueldo mensual";
    private static readonly string _defaultSource = "Transferencia";
    private static readonly DateTime _defaultCreatedAt = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    private static Income CreateValidIncome(
        Guid? categoryId = null,
        DateOnly? date = null,
        Money? amount = null,
        string? description = null,
        string? note = null,
        string? source = null,
        DateTime? createdAt = null)
    {
        return Income.Create(
            categoryId ?? _defaultCategoryId,
            date ?? _defaultDate,
            amount ?? _defaultAmount,
            description ?? _defaultDescription,
            note,
            source ?? _defaultSource,
            createdAt ?? _defaultCreatedAt);
    }

    private static Tag CreateTag(string name, string? color = null)
        => Tag.Create(name, color, _defaultCreatedAt);

    // ============================================================
    // T-1 a T-12: Create
    // ============================================================

    [Fact]
    public void Create_WithValidArgs_ReturnsIncomeWithExpectedState()
    {
        string note = "Nota de prueba";

        Income income = CreateValidIncome(note: note);

        Assert.NotEqual(Guid.Empty, income.Id);
        Assert.Equal(_defaultCategoryId, income.CategoryId);
        Assert.Equal(_defaultDate, income.Date);
        Assert.Equal(_defaultAmount, income.Amount);
        Assert.Equal(_defaultDescription, income.Description);
        Assert.Equal(note, income.Note);
        Assert.Equal(_defaultSource, income.Source);
        Assert.Equal(_defaultCreatedAt, income.CreatedAt);
        Assert.Equal(_defaultCreatedAt, income.UpdatedAt);
    }

    [Fact]
    public void Create_WithNullAmount_ThrowsArgumentNullException()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => Income.Create(_defaultCategoryId, _defaultDate, null!, _defaultDescription, null, null, _defaultCreatedAt));

        Assert.Equal("amount", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Create_WithZeroOrNegativeAmount_ThrowsArgumentException(decimal amountValue)
    {
        ArgumentException ex = Assert.ThrowsAny<ArgumentException>(
            () => Income.Create(
                _defaultCategoryId,
                _defaultDate,
                Money.Create(amountValue, Currency.DOP),
                _defaultDescription,
                null,
                null,
                _defaultCreatedAt));

        Assert.Contains("Amount", ex.Message);
    }

    [Theory]
    [InlineData(1899, 12, 31)]
    public void Create_WithDateBeforeSanityFloor_ThrowsArgumentException(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Income.Create(_defaultCategoryId, date, _defaultAmount, _defaultDescription, null, null, _defaultCreatedAt));

        Assert.Contains("Date", ex.Message);
        Assert.Contains("1900-01-01", ex.Message);
    }

    [Fact]
    public void Create_WithFutureDate_Succeeds()
    {
        var futureDate = new DateOnly(2099, 12, 31);

        var income = Income.Create(
            _defaultCategoryId, futureDate, _defaultAmount, _defaultDescription, null, null, _defaultCreatedAt);

        Assert.Equal(futureDate, income.Date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_WithEmptyOrWhitespaceDescription_ThrowsArgumentException(string description)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Income.Create(_defaultCategoryId, _defaultDate, _defaultAmount, description, null, null, _defaultCreatedAt));

        Assert.Contains("Description", ex.Message);
    }

    [Fact]
    public void Create_WithDescriptionOverMaxLength_ThrowsArgumentException()
    {
        string description = new('a', 201);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Income.Create(_defaultCategoryId, _defaultDate, _defaultAmount, description, null, null, _defaultCreatedAt));

        Assert.Contains("Description", ex.Message);
        Assert.Contains("200", ex.Message);
    }

    [Fact]
    public void Create_WithNoteOverMaxLength_ThrowsArgumentException()
    {
        string note = new('a', 501);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Income.Create(_defaultCategoryId, _defaultDate, _defaultAmount, _defaultDescription, note, null, _defaultCreatedAt));

        Assert.Contains("Note", ex.Message);
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public void Create_WithSourceOverMaxLength_ThrowsArgumentException()
    {
        string source = new('a', 51);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Income.Create(_defaultCategoryId, _defaultDate, _defaultAmount, _defaultDescription, null, source, _defaultCreatedAt));

        Assert.Contains("Source", ex.Message);
        Assert.Contains("50", ex.Message);
    }

    [Fact]
    public void Create_WithNullCategoryId_Succeeds()
    {
        var income = Income.Create(
            null, _defaultDate, _defaultAmount, _defaultDescription, null, null, _defaultCreatedAt);

        Assert.Null(income.CategoryId);
    }

    [Fact]
    public void Create_WithEmptyCategoryId_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Income.Create(Guid.Empty, _defaultDate, _defaultAmount, _defaultDescription, null, null, _defaultCreatedAt));

        Assert.Contains("CategoryId", ex.Message);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void Create_WithWhitespaceNoteAndSource_NormalizesToNull(string? note, string? source)
    {
        var income = Income.Create(
            _defaultCategoryId, _defaultDate, _defaultAmount, _defaultDescription, note, source, _defaultCreatedAt);

        Assert.Null(income.Note);
        Assert.Null(income.Source);
    }

    // ============================================================
    // T-13 a T-15: Update
    // ============================================================

    [Fact]
    public void Update_WithValidArgs_RefreshesUpdatedAt_PreservesIdAndCreatedAt()
    {
        Income income = CreateValidIncome();
        Guid originalId = income.Id;
        DateTime originalCreatedAt = income.CreatedAt;
        var newDate = new DateOnly(2026, 2, 1);
        var newAmount = Money.Create(2500m, Currency.DOP);
        var newCategoryId = Guid.NewGuid();
        var newUpdatedAt = new DateTime(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc);

        income.Update(newCategoryId, newDate, newAmount, "Nueva descripción", "Nueva nota", "Nuevo origen", newUpdatedAt);

        Assert.Equal(originalId, income.Id);
        Assert.Equal(originalCreatedAt, income.CreatedAt);
        Assert.Equal(newCategoryId, income.CategoryId);
        Assert.Equal(newDate, income.Date);
        Assert.Equal(newAmount, income.Amount);
        Assert.Equal("Nueva descripción", income.Description);
        Assert.Equal("Nueva nota", income.Note);
        Assert.Equal("Nuevo origen", income.Source);
        Assert.Equal(newUpdatedAt, income.UpdatedAt);
    }

    [Theory]
    [InlineData(0, "Nueva descripción", "")]
    [InlineData(1500, "", "")]
    [InlineData(1500, "Nueva descripción", "00000000-0000-0000-0000-000000000000")]
    public void Update_WithInvalidArgs_ThrowsArgumentException(
        decimal amountValue,
        string description,
        string categoryIdValue)
    {
        Income income = CreateValidIncome();
        Guid? categoryId = string.IsNullOrEmpty(categoryIdValue)
            ? _defaultCategoryId
            : Guid.Parse(categoryIdValue);
        var amount = Money.Create(amountValue, Currency.DOP);

        Assert.Throws<ArgumentException>(
            () => income.Update(
                categoryId,
                _defaultDate,
                amount,
                description,
                null,
                null,
                _defaultCreatedAt.AddDays(1)));
    }

    [Fact]
    public void Update_WithUpdatedAtBeforeCreatedAt_ThrowsArgumentException()
    {
        Income income = CreateValidIncome();
        DateTime invalidUpdatedAt = _defaultCreatedAt.AddDays(-1);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => income.Update(_defaultCategoryId, _defaultDate, _defaultAmount, _defaultDescription, null, null, invalidUpdatedAt));

        Assert.Contains("UpdatedAt", ex.Message);
    }

    // ============================================================
    // T-16 y T-17: Igualdad por referencia
    // ============================================================

    [Fact]
    public void Equality_ByReference_TwoInstancesNotEqual()
    {
        Income a = CreateValidIncome();
        Income b = CreateValidIncome();

        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equality_SameInstance_IsEqual()
    {
        Income income = CreateValidIncome();
        Income sameReference = income;

        Assert.Equal(income, income);
        Assert.True(income == sameReference);
        Assert.False(income != sameReference);
    }

    // ============================================================
    // T-25 a T-32: Tags
    // ============================================================

    [Fact]
    public void NewIncome_HasEmptyNotNullTagsCollection()
    {
        Income income = CreateValidIncome();

        Assert.NotNull(income.Tags);
        Assert.Empty(income.Tags);
    }

    [Fact]
    public void AddTag_WithNewTag_AddsToCollection()
    {
        Income income = CreateValidIncome();
        Tag tag = CreateTag("Comida");

        income.AddTag(tag);

        Assert.Single(income.Tags);
        Assert.Contains(tag, income.Tags);
    }

    [Fact]
    public void AddTag_WithDuplicateId_IsNoOp()
    {
        Income income = CreateValidIncome();
        Tag tag = CreateTag("Comida");

        income.AddTag(tag);
        income.AddTag(tag);

        Assert.Single(income.Tags);
    }

    [Fact]
    public void AddTag_WithNullTag_ThrowsArgumentNullException()
    {
        Income income = CreateValidIncome();

        Assert.Throws<ArgumentNullException>(() => income.AddTag(null!));
    }

    [Fact]
    public void RemoveTag_Existing_RemovesFromCollection()
    {
        Income income = CreateValidIncome();
        Tag tagA = CreateTag("Comida");
        Tag tagB = CreateTag("Transporte");

        income.AddTag(tagA);
        income.AddTag(tagB);
        income.RemoveTag(tagA);

        Assert.Single(income.Tags);
        Assert.Contains(tagB, income.Tags);
        Assert.DoesNotContain(tagA, income.Tags);
    }

    [Fact]
    public void RemoveTag_NotInCollection_IsNoOp()
    {
        Income income = CreateValidIncome();
        Tag tagA = CreateTag("Comida");
        Tag tagB = CreateTag("Transporte");

        income.AddTag(tagA);
        income.RemoveTag(tagB);

        Assert.Single(income.Tags);
    }

    [Fact]
    public void RemoveTag_WithNullTag_ThrowsArgumentNullException()
    {
        Income income = CreateValidIncome();

        Assert.Throws<ArgumentNullException>(() => income.RemoveTag(null!));
    }
}
