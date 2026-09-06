using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Application.UseCases.Incomes.GetIncomes;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Incomes.GetIncomes;

/// <summary>
/// Unit tests for <see cref="GetIncomesQuery"/>.
/// </summary>
public sealed class GetIncomesQueryTests
{
    private static GetIncomesQuery CreateQuery(FakeIncomeRepository fake)
        => new(fake, NullLogger<GetIncomesQuery>.Instance);

    private static Income CreateIncome(DateOnly date, decimal amount, string description)
        => Income.Create(
            null,
            date,
            Money.Create(amount, Currency.DOP),
            description,
            null,
            null,
            new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task ExecuteAsync_EmptyRepo_ReturnsSuccessWithEmptyPagedResult()
    {
        // Arrange
        FakeIncomeRepository fake = new();
        fake.SetFilterResult(new PagedResult<Income>(Array.Empty<Income>(), 0, 1, 50));
        GetIncomesQuery query = CreateQuery(fake);
        GetIncomesQueryRequest request = new(1, 50);

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        PagedResult<IncomeDto> paged = Assert.IsType<PagedResult<IncomeDto>>(result.Value);
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
        Income first = CreateIncome(new DateOnly(2024, 1, 3), 150.00m, "First");
        Income second = CreateIncome(new DateOnly(2024, 1, 2), 75.50m, "Second");
        Income third = CreateIncome(new DateOnly(2024, 1, 1), 200.00m, "Third");

        FakeIncomeRepository fake = new();
        fake.SetFilterResult(new List<Income> { first, second, third }, 3, 1, 50);
        GetIncomesQuery query = CreateQuery(fake);
        GetIncomesQueryRequest request = new(1, 50);

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        PagedResult<IncomeDto> paged = Assert.IsType<PagedResult<IncomeDto>>(result.Value);
        Assert.Equal(3, paged.Items.Count);

        IncomeDto dto0 = paged.Items[0];
        Assert.Equal(first.Id, dto0.Id);
        Assert.Null(dto0.CategoryId);
        Assert.Equal(first.Date, dto0.Date);
        Assert.Equal(first.Amount.Amount, dto0.Amount);
        Assert.Equal(first.Amount.Currency, dto0.Currency);
        Assert.Equal(first.Description, dto0.Description);
        Assert.Equal(first.Note, dto0.Note);
        Assert.Equal(first.Source, dto0.Source);
        Assert.Equal(first.CreatedAt, dto0.CreatedAt);
        Assert.Equal(first.UpdatedAt, dto0.UpdatedAt);

        IncomeDto dto1 = paged.Items[1];
        Assert.Equal(second.Id, dto1.Id);
        Assert.Equal(second.Description, dto1.Description);

        IncomeDto dto2 = paged.Items[2];
        Assert.Equal(third.Id, dto2.Id);
        Assert.Equal(third.Description, dto2.Description);
    }

    [Fact]
    public async Task ExecuteAsync_WithFilters_MapsFilterToRepository()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var fromDate = new DateOnly(2024, 1, 1);
        var toDate = new DateOnly(2024, 1, 31);
        var tagIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        FakeIncomeRepository fake = new();
        fake.SetFilterResult(new PagedResult<Income>(Array.Empty<Income>(), 0, 1, 25));
        GetIncomesQuery query = CreateQuery(fake);
        GetIncomesQueryRequest request = new(2, 25, categoryId, fromDate, toDate, tagIds);

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(fake.LastIncomeFilter);
        Assert.Equal(2, fake.LastIncomeFilter!.Page);
        Assert.Equal(25, fake.LastIncomeFilter.PageSize);
        Assert.Equal(categoryId, fake.LastIncomeFilter.CategoryId);
        Assert.Equal(fromDate, fake.LastIncomeFilter.DateFrom);
        Assert.Equal(toDate, fake.LastIncomeFilter.DateTo);
        Assert.Equal(tagIds, fake.LastIncomeFilter.TagIds);
    }

    [Fact]
    public async Task ExecuteAsync_RepositoryThrows_ReturnsRepositoryErrorFailure()
    {
        // Arrange
        FakeIncomeRepository fake = new();
        fake.SetThrowOnGetFilter(new InvalidOperationException("boom"));
        GetIncomesQuery query = CreateQuery(fake);
        GetIncomesQueryRequest request = new(1, 50);

        // Act
        Result result = await query.ExecuteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("RepositoryError", result.Error!.Code);
        Assert.Equal("Ocurrió un error al obtener los ingresos.", result.Error.Message);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ExecuteAsync_DateFromGreaterThanDateTo_ReturnsInvalidFilter()
    {
        // Arrange
        FakeIncomeRepository fake = new();
        GetIncomesQuery query = CreateQuery(fake);
        GetIncomesQueryRequest request = new(
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
        FakeIncomeRepository fake = new();
        GetIncomesQuery query = CreateQuery(fake);
        GetIncomesQueryRequest request = new(
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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_PageLessThanOne_ReturnsInvalidFilter(int page)
    {
        // Arrange
        FakeIncomeRepository fake = new();
        GetIncomesQuery query = CreateQuery(fake);
        GetIncomesQueryRequest request = new(page, 50);

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
        FakeIncomeRepository fake = new();
        GetIncomesQuery query = CreateQuery(fake);
        GetIncomesQueryRequest request = new(1, pageSize);

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
        FakeIncomeRepository fake = new();
        GetIncomesQuery query = CreateQuery(fake);
        GetIncomesQueryRequest request = new(1, 201);

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
        FakeIncomeRepository fake = new();
        fake.SetThrowOnGetFilter(new OperationCanceledException());
        GetIncomesQuery query = CreateQuery(fake);
        GetIncomesQueryRequest request = new(1, 50);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => query.ExecuteAsync(request));
    }
}
