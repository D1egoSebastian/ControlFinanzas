using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Tests.Unit.Domain;

/// <summary>
/// Tests para la entidad <see cref="Expense"/> (F-001, Phase 3).
/// Cubre happy path, invariantes de <see cref="Expense.Create"/>,
/// <see cref="Expense.Update"/>, igualdad por referencia e
/// inmutabilidad.
/// </summary>
public class ExpenseTests
{
    private static readonly Guid _defaultCategoryId = Guid.NewGuid();
    private static readonly DateOnly _defaultDate = new(2026, 1, 15);
    private static readonly Money _defaultAmount = Money.Create(1500.50m, Currency.DOP);
    private static readonly string _defaultDescription = "Pizza con amigos";
    private static readonly DateTime _defaultCreatedAt = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    private static Expense CreateValidExpense(
        Guid? categoryId = null,
        DateOnly? date = null,
        Money? amount = null,
        string? description = null,
        string? note = null,
        DateTime? createdAt = null,
        Guid? paymentMethodId = null)
    {
        return Expense.Create(
            categoryId ?? _defaultCategoryId,
            date ?? _defaultDate,
            amount ?? _defaultAmount,
            description ?? _defaultDescription,
            note,
            createdAt ?? _defaultCreatedAt,
            paymentMethodId);
    }

    private static Tag CreateTag(string name, string? color = null)
        => Tag.Create(name, color, _defaultCreatedAt);

    // ============================================================
    // T-1 a T-11: Create
    // ============================================================

    [Fact]
    public void Create_WithValidArgs_ReturnsExpenseWithExpectedState()
    {
        string note = "Nota de prueba";

        Expense expense = CreateValidExpense(note: note);

        Assert.NotEqual(Guid.Empty, expense.Id);
        Assert.Equal(_defaultCategoryId, expense.CategoryId);
        Assert.Equal(_defaultDate, expense.Date);
        Assert.Equal(_defaultAmount, expense.Amount);
        Assert.Equal(_defaultDescription, expense.Description);
        Assert.Equal(note, expense.Note);
        Assert.Equal(_defaultCreatedAt, expense.CreatedAt);
        Assert.Equal(_defaultCreatedAt, expense.UpdatedAt);
    }

    [Fact]
    public void Create_WithNullAmount_ThrowsArgumentNullException()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => Expense.Create(_defaultCategoryId, _defaultDate, null!, _defaultDescription, null, _defaultCreatedAt));

        Assert.Equal("amount", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Create_WithZeroOrNegativeAmount_ThrowsArgumentException(decimal amountValue)
    {
        ArgumentException ex = Assert.ThrowsAny<ArgumentException>(
            () => Expense.Create(
                _defaultCategoryId,
                _defaultDate,
                Money.Create(amountValue, Currency.DOP),
                _defaultDescription,
                null,
                _defaultCreatedAt));

        Assert.Contains("Amount", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_WithEmptyOrWhitespaceDescription_ThrowsArgumentException(string description)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Expense.Create(_defaultCategoryId, _defaultDate, _defaultAmount, description, null, _defaultCreatedAt));

        Assert.Contains("Description", ex.Message);
    }

    [Fact]
    public void Create_WithDescriptionOverMaxLength_ThrowsArgumentException()
    {
        string description = new('a', 201);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Expense.Create(_defaultCategoryId, _defaultDate, _defaultAmount, description, null, _defaultCreatedAt));

        Assert.Contains("Description", ex.Message);
        Assert.Contains("200", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyCategoryId_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Expense.Create(Guid.Empty, _defaultDate, _defaultAmount, _defaultDescription, null, _defaultCreatedAt));

        Assert.Contains("CategoryId", ex.Message);
    }

    [Theory]
    [InlineData(1899, 12, 31)]
    public void Create_WithDateBeforeSanityFloor_ThrowsArgumentException(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Expense.Create(_defaultCategoryId, date, _defaultAmount, _defaultDescription, null, _defaultCreatedAt));

        Assert.Contains("Date", ex.Message);
        Assert.Contains("1900-01-01", ex.Message);
    }

    [Fact]
    public void Create_WithFutureDate_Succeeds()
    {
        var futureDate = new DateOnly(2099, 12, 31);

        var expense = Expense.Create(
            _defaultCategoryId, futureDate, _defaultAmount, _defaultDescription, null, _defaultCreatedAt);

        Assert.Equal(futureDate, expense.Date);
    }

    [Fact]
    public void Create_WithNoteOverMaxLength_ThrowsArgumentException()
    {
        string note = new('a', 501);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Expense.Create(_defaultCategoryId, _defaultDate, _defaultAmount, _defaultDescription, note, _defaultCreatedAt));

        Assert.Contains("Note", ex.Message);
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public void Create_WithNullNote_NormalizesToNull()
    {
        var expense = Expense.Create(
            _defaultCategoryId, _defaultDate, _defaultAmount, _defaultDescription, null, _defaultCreatedAt);

        Assert.Null(expense.Note);
    }

    [Fact]
    public void Create_WithWhitespaceNote_NormalizesToNull()
    {
        var expense = Expense.Create(
            _defaultCategoryId, _defaultDate, _defaultAmount, _defaultDescription, "   ", _defaultCreatedAt);

        Assert.Null(expense.Note);
    }

    // ============================================================
    // PaymentMethodId (F-503)
    // ============================================================

    [Fact]
    public void Create_WithPaymentMethodId_ReturnsExpenseWithThatId()
    {
        var paymentMethodId = Guid.NewGuid();

        Expense expense = CreateValidExpense(paymentMethodId: paymentMethodId);

        Assert.Equal(paymentMethodId, expense.PaymentMethodId);
    }

    [Fact]
    public void Create_WithoutPaymentMethodId_ReturnsExpenseWithNull()
    {
        Expense expense = CreateValidExpense();

        Assert.Null(expense.PaymentMethodId);
    }

    [Fact]
    public void Create_WithEmptyPaymentMethodId_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Expense.Create(
                _defaultCategoryId, _defaultDate, _defaultAmount,
                _defaultDescription, null,
                _defaultCreatedAt,
                paymentMethodId: Guid.Empty));

        Assert.Equal("paymentMethodId", ex.ParamName);
        Assert.Contains("PaymentMethodId", ex.Message);
    }

    [Fact]
    public void Create_WithPaymentMethodId_AssignsPaymentMethodId()
    {
        var paymentMethodId = Guid.NewGuid();
        Expense expense = CreateValidExpense(paymentMethodId: paymentMethodId);

        Assert.Equal(paymentMethodId, expense.PaymentMethodId);
    }

    [Fact]
    public void Update_WithPaymentMethodId_AssignsNewPaymentMethodId()
    {
        Expense expense = CreateValidExpense();
        DateTime updatedAt = _defaultCreatedAt.AddMinutes(1);
        var newPaymentMethodId = Guid.NewGuid();

        expense.Update(
            _defaultCategoryId, _defaultDate, _defaultAmount,
            _defaultDescription, null,
            updatedAt,
            newPaymentMethodId);

        Assert.Equal(newPaymentMethodId, expense.PaymentMethodId);
        Assert.Equal(updatedAt, expense.UpdatedAt);
    }

    [Fact]
    public void Update_WithNullPaymentMethodId_DoesNotThrow()
    {
        Expense expense = CreateValidExpense();
        DateTime updatedAt = _defaultCreatedAt.AddMinutes(1);

        expense.Update(
            _defaultCategoryId, _defaultDate, _defaultAmount,
            _defaultDescription, null,
            updatedAt,
            paymentMethodId: null);

        Assert.Null(expense.PaymentMethodId);
    }

    [Fact]
    public void Update_WithEmptyPaymentMethodId_ThrowsArgumentException()
    {
        Expense expense = CreateValidExpense();
        DateTime updatedAt = _defaultCreatedAt.AddMinutes(1);

        Assert.Throws<ArgumentException>(
            () => expense.Update(
                _defaultCategoryId, _defaultDate, _defaultAmount,
                _defaultDescription, null,
                updatedAt,
                paymentMethodId: Guid.Empty));
    }

    // ============================================================
    // T-12 a T-14: Update
    // ============================================================

    [Fact]
    public void Update_WithValidArgs_RefreshesUpdatedAt_PreservesIdAndCreatedAt()
    {
        Expense expense = CreateValidExpense();
        Guid originalId = expense.Id;
        DateTime originalCreatedAt = expense.CreatedAt;
        var newDate = new DateOnly(2026, 2, 1);
        var newAmount = Money.Create(2500m, Currency.DOP);
        var newCategoryId = Guid.NewGuid();
        var newUpdatedAt = new DateTime(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc);

        expense.Update(newCategoryId, newDate, newAmount, "Nueva descripción", "Nueva nota", newUpdatedAt, paymentMethodId: null);

        Assert.Equal(originalId, expense.Id);
        Assert.Equal(originalCreatedAt, expense.CreatedAt);
        Assert.Equal(newCategoryId, expense.CategoryId);
        Assert.Equal(newDate, expense.Date);
        Assert.Equal(newAmount, expense.Amount);
        Assert.Equal("Nueva descripción", expense.Description);
        Assert.Equal("Nueva nota", expense.Note);
        Assert.Equal(newUpdatedAt, expense.UpdatedAt);
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
        Expense expense = CreateValidExpense();
        Guid categoryId = string.IsNullOrEmpty(categoryIdValue)
            ? _defaultCategoryId
            : Guid.Parse(categoryIdValue);
        var amount = Money.Create(amountValue, Currency.DOP);

        Assert.Throws<ArgumentException>(
            () => expense.Update(
                categoryId,
                _defaultDate,
                amount,
                description,
                null,
                _defaultCreatedAt.AddDays(1),
                paymentMethodId: null));
    }

    [Fact]
    public void Update_WithNoteOverMaxLength_ThrowsArgumentException()
    {
        Expense expense = CreateValidExpense();
        string note = new('a', 501);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => expense.Update(_defaultCategoryId, _defaultDate, _defaultAmount, _defaultDescription, note, _defaultCreatedAt.AddDays(1), paymentMethodId: null));

        Assert.Contains("Note", ex.Message);
    }

    [Fact]
    public void Update_WithUpdatedAtBeforeCreatedAt_ThrowsArgumentException()
    {
        Expense expense = CreateValidExpense();
        DateTime invalidUpdatedAt = _defaultCreatedAt.AddDays(-1);

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => expense.Update(_defaultCategoryId, _defaultDate, _defaultAmount, _defaultDescription, null, invalidUpdatedAt, paymentMethodId: null));

        Assert.Contains("UpdatedAt", ex.Message);
    }

    // ============================================================
    // T-15 y T-16: Igualdad por referencia
    // ============================================================

    [Fact]
    public void Equality_ByReference_TwoInstancesNotEqual()
    {
        Expense a = CreateValidExpense();
        Expense b = CreateValidExpense();

        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equality_SameInstance_IsEqual()
    {
        Expense expense = CreateValidExpense();
        Expense sameReference = expense;

        Assert.Equal(expense, expense);
        Assert.True(expense == sameReference);
        Assert.False(expense != sameReference);
    }

    // ============================================================
    // T-17 a T-24: Tags
    // ============================================================

    [Fact]
    public void NewExpense_HasEmptyNotNullTagsCollection()
    {
        Expense expense = CreateValidExpense();

        Assert.NotNull(expense.Tags);
        Assert.Empty(expense.Tags);
    }

    [Fact]
    public void AddTag_WithNewTag_AddsToCollection()
    {
        Expense expense = CreateValidExpense();
        Tag tag = CreateTag("Comida");

        expense.AddTag(tag);

        Assert.Single(expense.Tags);
        Assert.Contains(tag, expense.Tags);
    }

    [Fact]
    public void AddTag_WithDuplicateId_IsNoOp()
    {
        Expense expense = CreateValidExpense();
        Tag tag = CreateTag("Comida");

        expense.AddTag(tag);
        expense.AddTag(tag);

        Assert.Single(expense.Tags);
    }

    [Fact]
    public void AddTag_WithNullTag_ThrowsArgumentNullException()
    {
        Expense expense = CreateValidExpense();

        Assert.Throws<ArgumentNullException>(() => expense.AddTag(null!));
    }

    [Fact]
    public void RemoveTag_Existing_RemovesFromCollection()
    {
        Expense expense = CreateValidExpense();
        Tag tagA = CreateTag("Comida");
        Tag tagB = CreateTag("Transporte");

        expense.AddTag(tagA);
        expense.AddTag(tagB);
        expense.RemoveTag(tagA);

        Assert.Single(expense.Tags);
        Assert.Contains(tagB, expense.Tags);
        Assert.DoesNotContain(tagA, expense.Tags);
    }

    [Fact]
    public void RemoveTag_NotInCollection_IsNoOp()
    {
        Expense expense = CreateValidExpense();
        Tag tagA = CreateTag("Comida");
        Tag tagB = CreateTag("Transporte");

        expense.AddTag(tagA);
        expense.RemoveTag(tagB);

        Assert.Single(expense.Tags);
    }

    [Fact]
    public void RemoveTag_WithNullTag_ThrowsArgumentNullException()
    {
        Expense expense = CreateValidExpense();

        Assert.Throws<ArgumentNullException>(() => expense.RemoveTag(null!));
    }
}
