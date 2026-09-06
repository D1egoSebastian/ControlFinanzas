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
/// Tests de integración para el filtro <see cref="IncomeFilter.TagIds"/>
/// en <see cref="IncomeRepository.GetByFilterAsync"/>.
/// </summary>
public sealed class IncomeFilterTagTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _comidaCategoryId = new("11111111-1111-1111-1111-000000000001");

    private static Tag CreateTag(string name, string? color = null)
        => Tag.Create(name, color, _fixedTimestamp);

    private static Income CreateIncome(
        Guid? categoryId,
        string description,
        DateOnly? date = null,
        decimal amount = 100m)
    {
        return Income.Create(
            categoryId,
            date ?? new DateOnly(2026, 1, 15),
            Money.Create(amount, Currency.DOP),
            description,
            null,
            null,
            _fixedTimestamp);
    }

    [Fact]
    public async Task GetByFilterAsync_WithTagIds_FiltersCorrectly()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Tag tagA = CreateTag("TagA");
        Tag tagB = CreateTag("TagB");
        context.Tags.AddRange(tagA, tagB);
        await context.SaveChangesAsync();

        Income income1 = CreateIncome(_comidaCategoryId, "I1");
        income1.AddTag(tagA);
        Income income2 = CreateIncome(_comidaCategoryId, "I2");
        income2.AddTag(tagB);
        Income income3 = CreateIncome(_comidaCategoryId, "I3");

        await repo.AddAsync(income1);
        await repo.AddAsync(income2);
        await repo.AddAsync(income3);

        var filter = new IncomeFilter { TagIds = new[] { tagA.Id } };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(income1.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task GetByFilterAsync_WithMultipleTagIds_ReturnsIncomesWithAny()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Tag tagA = CreateTag("TagA");
        Tag tagB = CreateTag("TagB");
        context.Tags.AddRange(tagA, tagB);
        await context.SaveChangesAsync();

        Income income1 = CreateIncome(_comidaCategoryId, "I1");
        income1.AddTag(tagA);
        Income income2 = CreateIncome(_comidaCategoryId, "I2");
        income2.AddTag(tagB);
        Income income3 = CreateIncome(_comidaCategoryId, "I3");

        await repo.AddAsync(income1);
        await repo.AddAsync(income2);
        await repo.AddAsync(income3);

        var filter = new IncomeFilter { TagIds = new[] { tagA.Id, tagB.Id } };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.Contains(page.Items, i => i.Id == income1.Id);
        Assert.Contains(page.Items, i => i.Id == income2.Id);
    }

    [Fact]
    public async Task GetByFilterAsync_WithEmptyTagIds_ReturnsAll()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Tag tagA = CreateTag("TagA");
        context.Tags.Add(tagA);
        await context.SaveChangesAsync();

        Income income1 = CreateIncome(_comidaCategoryId, "I1");
        income1.AddTag(tagA);
        Income income2 = CreateIncome(_comidaCategoryId, "I2");
        Income income3 = CreateIncome(_comidaCategoryId, "I3");

        await repo.AddAsync(income1);
        await repo.AddAsync(income2);
        await repo.AddAsync(income3);

        var filter = new IncomeFilter { TagIds = Array.Empty<Guid>() };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(3, page.Total);
        Assert.Equal(3, page.Items.Count);
    }

    [Fact]
    public async Task GetByFilterAsync_WithNullTagIds_ReturnsAll()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Tag tagA = CreateTag("TagA");
        context.Tags.Add(tagA);
        await context.SaveChangesAsync();

        Income income1 = CreateIncome(_comidaCategoryId, "I1");
        income1.AddTag(tagA);
        Income income2 = CreateIncome(_comidaCategoryId, "I2");
        Income income3 = CreateIncome(_comidaCategoryId, "I3");

        await repo.AddAsync(income1);
        await repo.AddAsync(income2);
        await repo.AddAsync(income3);

        var filter = new IncomeFilter { TagIds = null };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(3, page.Total);
        Assert.Equal(3, page.Items.Count);
    }

    [Fact]
    public async Task GetByFilterAsync_WithNonExistentTagId_ReturnsEmpty()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Tag tagA = CreateTag("TagA");
        context.Tags.Add(tagA);
        await context.SaveChangesAsync();

        Income income1 = CreateIncome(_comidaCategoryId, "I1");
        income1.AddTag(tagA);

        await repo.AddAsync(income1);

        var filter = new IncomeFilter { TagIds = new[] { Guid.NewGuid() } };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(0, page.Total);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task GetByFilterAsync_WithCombinedCategoryAndTagIds_ReturnsIntersection()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

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

        Income income1 = CreateIncome(categoryX.Id, "I1");
        income1.AddTag(tagA);
        Income income2 = CreateIncome(categoryY.Id, "I2");
        income2.AddTag(tagA);
        Income income3 = CreateIncome(categoryX.Id, "I3");
        income3.AddTag(tagB);

        await repo.AddAsync(income1);
        await repo.AddAsync(income2);
        await repo.AddAsync(income3);

        var filter = new IncomeFilter
        {
            CategoryId = categoryX.Id,
            TagIds = new[] { tagA.Id },
        };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(income1.Id, page.Items[0].Id);
    }
}
