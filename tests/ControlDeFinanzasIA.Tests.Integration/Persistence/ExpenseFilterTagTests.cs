using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integración para el filtro <see cref="ExpenseFilter.TagIds"/>
/// en <see cref="ExpenseRepository.GetByFilterAsync"/>.
/// </summary>
public sealed class ExpenseFilterTagTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _comidaCategoryId = new("11111111-1111-1111-1111-000000000001");

    private static Tag CreateTag(string name, string? color = null)
        => Tag.Create(name, color, _fixedTimestamp);

    private static Expense CreateExpense(
        Guid categoryId,
        string description,
        DateOnly? date = null)
    {
        return Expense.Create(
            categoryId,
            date ?? new DateOnly(2026, 1, 15),
            Money.Create(100m, Currency.DOP),
            description,
            null,
            _fixedTimestamp);
    }

    [Fact]
    public async Task GetByFilterAsync_WithSingleTagId_ReturnsOnlyMatching()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Tag tagA = CreateTag("TagA");
        Tag tagB = CreateTag("TagB");
        context.Tags.AddRange(tagA, tagB);
        await context.SaveChangesAsync();

        Expense expense1 = CreateExpense(_comidaCategoryId, "E1");
        expense1.AddTag(tagA);
        Expense expense2 = CreateExpense(_comidaCategoryId, "E2");
        expense2.AddTag(tagB);
        Expense expense3 = CreateExpense(_comidaCategoryId, "E3");

        await repo.AddAsync(expense1);
        await repo.AddAsync(expense2);
        await repo.AddAsync(expense3);

        var filter = new ExpenseFilter { TagIds = new[] { tagA.Id } };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(expense1.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task GetByFilterAsync_WithMultipleTagIds_ReturnsExpensesWithAny()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Tag tagA = CreateTag("TagA");
        Tag tagB = CreateTag("TagB");
        context.Tags.AddRange(tagA, tagB);
        await context.SaveChangesAsync();

        Expense expense1 = CreateExpense(_comidaCategoryId, "E1");
        expense1.AddTag(tagA);
        Expense expense2 = CreateExpense(_comidaCategoryId, "E2");
        expense2.AddTag(tagB);
        Expense expense3 = CreateExpense(_comidaCategoryId, "E3");

        await repo.AddAsync(expense1);
        await repo.AddAsync(expense2);
        await repo.AddAsync(expense3);

        var filter = new ExpenseFilter { TagIds = new[] { tagA.Id, tagB.Id } };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.Contains(page.Items, e => e.Id == expense1.Id);
        Assert.Contains(page.Items, e => e.Id == expense2.Id);
    }

    [Fact]
    public async Task GetByFilterAsync_WithEmptyTagIds_ReturnsAll()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Tag tagA = CreateTag("TagA");
        context.Tags.Add(tagA);
        await context.SaveChangesAsync();

        Expense expense1 = CreateExpense(_comidaCategoryId, "E1");
        expense1.AddTag(tagA);
        Expense expense2 = CreateExpense(_comidaCategoryId, "E2");
        Expense expense3 = CreateExpense(_comidaCategoryId, "E3");

        await repo.AddAsync(expense1);
        await repo.AddAsync(expense2);
        await repo.AddAsync(expense3);

        var filter = new ExpenseFilter { TagIds = Array.Empty<Guid>() };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(3, page.Total);
        Assert.Equal(3, page.Items.Count);
    }

    [Fact]
    public async Task GetByFilterAsync_WithNullTagIds_ReturnsAll()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Tag tagA = CreateTag("TagA");
        context.Tags.Add(tagA);
        await context.SaveChangesAsync();

        Expense expense1 = CreateExpense(_comidaCategoryId, "E1");
        expense1.AddTag(tagA);
        Expense expense2 = CreateExpense(_comidaCategoryId, "E2");
        Expense expense3 = CreateExpense(_comidaCategoryId, "E3");

        await repo.AddAsync(expense1);
        await repo.AddAsync(expense2);
        await repo.AddAsync(expense3);

        var filter = new ExpenseFilter { TagIds = null };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(3, page.Total);
        Assert.Equal(3, page.Items.Count);
    }

    [Fact]
    public async Task GetByFilterAsync_WithNonExistentTagId_ReturnsEmpty()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Tag tagA = CreateTag("TagA");
        context.Tags.Add(tagA);
        await context.SaveChangesAsync();

        Expense expense1 = CreateExpense(_comidaCategoryId, "E1");
        expense1.AddTag(tagA);

        await repo.AddAsync(expense1);

        var filter = new ExpenseFilter { TagIds = new[] { Guid.NewGuid() } };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(0, page.Total);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task GetByFilterAsync_WithCombinedCategoryAndTagIds_ReturnsIntersection()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        var categoryX = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Category X",
            Color = "#000000",
            Icon = "🧪",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };
        var categoryY = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Category Y",
            Color = "#000000",
            Icon = "🧪",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };
        context.Categories.AddRange(categoryX, categoryY);

        Tag tagA = CreateTag("TagA");
        Tag tagB = CreateTag("TagB");
        context.Tags.AddRange(tagA, tagB);
        await context.SaveChangesAsync();

        Expense expense1 = CreateExpense(categoryX.Id, "E1");
        expense1.AddTag(tagA);
        Expense expense2 = CreateExpense(categoryY.Id, "E2");
        expense2.AddTag(tagA);
        Expense expense3 = CreateExpense(categoryX.Id, "E3");
        expense3.AddTag(tagB);

        await repo.AddAsync(expense1);
        await repo.AddAsync(expense2);
        await repo.AddAsync(expense3);

        var filter = new ExpenseFilter
        {
            CategoryId = categoryX.Id,
            TagIds = new[] { tagA.Id },
        };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(expense1.Id, page.Items[0].Id);
    }
}
