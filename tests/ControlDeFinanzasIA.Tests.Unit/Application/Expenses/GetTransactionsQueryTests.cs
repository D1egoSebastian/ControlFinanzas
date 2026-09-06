using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Application.UseCases.Expenses.GetTransactions;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Expenses;

/// <summary>
/// Unit tests for <see cref="GetTransactionsQuery"/>.
/// </summary>
public sealed class GetTransactionsQueryTests
{
    private static GetTransactionsQuery CreateQuery(FakeExpenseRepository fake)
        => new(fake, NullLogger<GetTransactionsQuery>.Instance);

    private static Expense CreateExpense(DateOnly date, decimal amount, string description)
        => Expense.Create(
            Guid.NewGuid(),
            date,
            Money.Create(amount, Currency.DOP),
            description,
            null,
            new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task ExecuteAsync_EmptyRepo_ReturnsSuccessWithEmptyPagedResult()
    {
        // Arrange
        FakeExpenseRepository fake = new();
        fake.SetFilterResult(new PagedResult<Expense>(Array.Empty<Expense>(), 0, 1, 50));
        GetTransactionsQuery query = CreateQuery(fake);
        GetTransactionsQueryRequest request = new(1, 50);

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        PagedResult<ExpenseDto> paged = Assert.IsType<PagedResult<ExpenseDto>>(result.Value);
        Assert.Empty(paged.Items);
        Assert.Equal(0, paged.Total);
        Assert.Equal(1, paged.Page);
        Assert.Equal(50, paged.PageSize);
        Assert.Equal(0, paged.TotalPages);
    }

    [Fact]
    public async Task ExecuteAsync_WithItems_ReturnsMappedDtosPreservingOrder()
    {
        // Arrange
        Expense first = CreateExpense(new DateOnly(2024, 1, 3), 150.00m, "First");
        Expense second = CreateExpense(new DateOnly(2024, 1, 2), 75.50m, "Second");
        Expense third = CreateExpense(new DateOnly(2024, 1, 1), 200.00m, "Third");

        FakeExpenseRepository fake = new();
        fake.SetFilterResult(new List<Expense> { first, second, third }, 3, 1, 50);
        GetTransactionsQuery query = CreateQuery(fake);
        GetTransactionsQueryRequest request = new(1, 50);

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        PagedResult<ExpenseDto> paged = Assert.IsType<PagedResult<ExpenseDto>>(result.Value);
        Assert.Equal(3, paged.Items.Count);

        ExpenseDto dto0 = paged.Items[0];
        Assert.Equal(first.Id, dto0.Id);
        Assert.Equal(first.CategoryId, dto0.CategoryId);
        Assert.Equal(first.Date, dto0.Date);
        Assert.Equal(first.Amount.Amount, dto0.Amount);
        Assert.Equal(first.Amount.Currency, dto0.Currency);
        Assert.Equal(first.Description, dto0.Description);
        Assert.Equal(first.Note, dto0.Note);
        Assert.Equal(first.CreatedAt, dto0.CreatedAt);
        Assert.Equal(first.UpdatedAt, dto0.UpdatedAt);

        ExpenseDto dto1 = paged.Items[1];
        Assert.Equal(second.Id, dto1.Id);
        Assert.Equal(second.Description, dto1.Description);

        ExpenseDto dto2 = paged.Items[2];
        Assert.Equal(third.Id, dto2.Id);
        Assert.Equal(third.Description, dto2.Description);
    }

    [Fact]
    public async Task ExecuteAsync_PageBeyondEnd_ReturnsEmptyItemsWithRealTotal()
    {
        // Arrange
        FakeExpenseRepository fake = new();
        fake.SetFilterResult(new PagedResult<Expense>(Array.Empty<Expense>(), 30, 999, 50));
        GetTransactionsQuery query = CreateQuery(fake);
        GetTransactionsQueryRequest request = new(999, 50);

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        PagedResult<ExpenseDto> paged = Assert.IsType<PagedResult<ExpenseDto>>(result.Value);
        Assert.Empty(paged.Items);
        Assert.Equal(30, paged.Total);
        Assert.Equal(999, paged.Page);
        Assert.Equal(50, paged.PageSize);
    }

    [Fact]
    public async Task ExecuteAsync_RepositoryThrows_ReturnsRepositoryErrorFailure()
    {
        // Arrange
        FakeExpenseRepository fake = new();
        fake.SetThrowOnGetFilter(new InvalidOperationException("boom"));
        GetTransactionsQuery query = CreateQuery(fake);
        GetTransactionsQueryRequest request = new(1, 50);

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("RepositoryError", result.Error!.Code);
        Assert.Equal("Ocurrió un error al obtener los gastos.", result.Error.Message);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_PageLessThanOne_ReturnsInvalidFilter(int page)
    {
        // Arrange
        FakeExpenseRepository fake = new();
        GetTransactionsQuery query = CreateQuery(fake);
        GetTransactionsQueryRequest request = new(page, 50);

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("InvalidFilter", result.Error!.Code);
        Assert.Equal(0, fake.GetByFilterCallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_PageSizeZeroOrNegative_ReturnsInvalidFilter(int pageSize)
    {
        // Arrange
        FakeExpenseRepository fake = new();
        GetTransactionsQuery query = CreateQuery(fake);
        GetTransactionsQueryRequest request = new(1, pageSize);

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("InvalidFilter", result.Error!.Code);
        Assert.Equal(0, fake.GetByFilterCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_PageSizeOver200_ReturnsInvalidFilter()
    {
        // Arrange
        FakeExpenseRepository fake = new();
        GetTransactionsQuery query = CreateQuery(fake);
        GetTransactionsQueryRequest request = new(1, 201);

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("InvalidFilter", result.Error!.Code);
        Assert.Equal(0, fake.GetByFilterCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithFilters_MapsFilterToRepository()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var fromDate = new DateOnly(2024, 1, 1);
        var toDate = new DateOnly(2024, 1, 31);
        var tagIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        FakeExpenseRepository fake = new();
        fake.SetFilterResult(new PagedResult<Expense>(Array.Empty<Expense>(), 0, 1, 25));
        GetTransactionsQuery query = CreateQuery(fake);
        GetTransactionsQueryRequest request = new(2, 25, categoryId, fromDate, toDate, tagIds);

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(fake.LastExpenseFilter);
        Assert.Equal(2, fake.LastExpenseFilter!.Page);
        Assert.Equal(25, fake.LastExpenseFilter.PageSize);
        Assert.Equal(categoryId, fake.LastExpenseFilter.CategoryId);
        Assert.Equal(fromDate, fake.LastExpenseFilter.DateFrom);
        Assert.Equal(toDate, fake.LastExpenseFilter.DateTo);
        Assert.Equal(tagIds, fake.LastExpenseFilter.TagIds);
    }

    [Fact]
    public async Task ExecuteAsync_DateFromGreaterThanDateTo_ReturnsInvalidFilter()
    {
        // Arrange
        FakeExpenseRepository fake = new();
        GetTransactionsQuery query = CreateQuery(fake);
        GetTransactionsQueryRequest request = new(
            1,
            50,
            null,
            new DateOnly(2024, 1, 31),
            new DateOnly(2024, 1, 1));

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("InvalidFilter", result.Error!.Code);
        Assert.Equal(0, fake.GetByFilterCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_TagIdsContainsEmptyGuid_ReturnsInvalidFilter()
    {
        // Arrange
        FakeExpenseRepository fake = new();
        GetTransactionsQuery query = CreateQuery(fake);
        GetTransactionsQueryRequest request = new(
            1,
            50,
            null,
            null,
            null,
            new List<Guid> { Guid.NewGuid(), Guid.Empty });

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("InvalidFilter", result.Error!.Code);
        Assert.Equal(0, fake.GetByFilterCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_RethrowsOperationCanceledException()
    {
        // Arrange
        FakeExpenseRepository fake = new();
        fake.SetThrowOnGetFilter(new OperationCanceledException());
        GetTransactionsQuery query = CreateQuery(fake);
        GetTransactionsQueryRequest request = new(1, 50);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => query.ExecuteAsync(request));
    }
}
