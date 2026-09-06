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
/// Tests de integración para <see cref="IncomeRepository"/>.
/// </summary>
/// <remarks>
/// Usa SQLite in-memory con <see cref="TestAppDbContextFactory"/> y valida
/// CRUD, validación condicional de FK a <see cref="Category"/>, filtros por
/// categoría, rango de fecha, paginación y el filtrado por <c>TagIds</c>.
/// </remarks>
public sealed class IncomeRepositoryTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _comidaCategoryId = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _transporteCategoryId = new("11111111-1111-1111-1111-000000000002");

    [Fact]
    public async Task AddAsync_WithValidIncome_Persists()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Income income = CreateIncome(
            _comidaCategoryId,
            new DateOnly(2026, 1, 15),
            1500.50m,
            "Sueldo mensual",
            source: "Transferencia");

        Result result = await repo.AddAsync(income);

        Assert.True(result.IsSuccess);

        Income? persisted = await repo.GetByIdAsync(income.Id);
        Assert.NotNull(persisted);
        Assert.Equal(income.Id, persisted.Id);
        Assert.Equal(_comidaCategoryId, persisted.CategoryId);
        Assert.Equal(new DateOnly(2026, 1, 15), persisted.Date);
        Assert.Equal(1500.50m, persisted.Amount.Amount);
        Assert.Equal(Currency.DOP, persisted.Amount.Currency);
        Assert.Equal("Sueldo mensual", persisted.Description);
        Assert.Equal("Transferencia", persisted.Source);
        Assert.Null(persisted.Note);
    }

    [Fact]
    public async Task AddAsync_WithUnknownCategoryId_ReturnsCategoryNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Income income = CreateIncome(
            Guid.NewGuid(),
            new DateOnly(2026, 1, 15),
            500m,
            "Ingreso sin categoría conocida");

        Result result = await repo.AddAsync(income);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("CategoryNotFound", result.Error!.Code);
        Assert.False(string.IsNullOrEmpty(result.Error.Message));
    }

    [Fact]
    public async Task AddAsync_WithNullCategoryId_Persists()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Income income = CreateIncome(
            null,
            new DateOnly(2026, 1, 15),
            1000m,
            "Ingreso sin categoría");

        Result result = await repo.AddAsync(income);

        Assert.True(result.IsSuccess);

        Income? persisted = await repo.GetByIdAsync(income.Id);
        Assert.NotNull(persisted);
        Assert.Null(persisted.CategoryId);
        Assert.Equal("Ingreso sin categoría", persisted.Description);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsIncome()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Income income = CreateIncome(
            _comidaCategoryId,
            new DateOnly(2026, 2, 10),
            250m,
            "Venta freelance");
        await repo.AddAsync(income);

        Income? retrieved = await repo.GetByIdAsync(income.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(income.Id, retrieved.Id);
        Assert.Equal("Venta freelance", retrieved.Description);
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNull()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Income? retrieved = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyGuid_ReturnsNull()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Income? retrieved = await repo.GetByIdAsync(Guid.Empty);

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllOrderedByDateDesc()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Income first = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 1), 100m, "Primero");
        Income second = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 3), 200m, "Segundo");
        Income third = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 2), 150m, "Tercero");

        await repo.AddAsync(first);
        await repo.AddAsync(second);
        await repo.AddAsync(third);

        IReadOnlyList<Income> all = await repo.GetAllAsync();

        Assert.Equal(3, all.Count);
        Assert.Equal(new[] { new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1) },
            all.Select(e => e.Date).ToArray());
    }

    [Fact]
    public async Task GetByFilterAsync_NoFilter_ReturnsFirstPageOrdered()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Income first = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 1), 100m, "Primero");
        Income second = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 3), 200m, "Segundo");
        Income third = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 2), 150m, "Tercero");

        await repo.AddAsync(first);
        await repo.AddAsync(second);
        await repo.AddAsync(third);

        var filter = new IncomeFilter { Page = 1, PageSize = 50 };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(3, page.Total);
        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal(3, page.Items.Count);
        Assert.Equal(new DateOnly(2026, 1, 3), page.Items[0].Date);
        Assert.Equal(new DateOnly(2026, 1, 2), page.Items[1].Date);
        Assert.Equal(new DateOnly(2026, 1, 1), page.Items[2].Date);
    }

    [Fact]
    public async Task GetByFilterAsync_WithCategoryId_FiltersCorrectly()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Income comida1 = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 10), 100m, "Comida 1");
        Income comida2 = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 11), 200m, "Comida 2");
        Income transporte = CreateIncome(_transporteCategoryId, new DateOnly(2026, 1, 12), 300m, "Transporte 1");

        await repo.AddAsync(comida1);
        await repo.AddAsync(comida2);
        await repo.AddAsync(transporte);

        var filter = new IncomeFilter { CategoryId = _comidaCategoryId };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, item => Assert.Equal(_comidaCategoryId, item.CategoryId));
    }

    [Fact]
    public async Task GetByFilterAsync_WithDateRange_FiltersCorrectly()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Income before = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 1), 100m, "Antes");
        Income inside = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 15), 200m, "Dentro");
        Income after = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 31), 300m, "Después");

        await repo.AddAsync(before);
        await repo.AddAsync(inside);
        await repo.AddAsync(after);

        var filter = new IncomeFilter
        {
            Page = 1,
            PageSize = 50,
            DateFrom = new DateOnly(2026, 1, 10),
            DateTo = new DateOnly(2026, 1, 20),
        };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(new DateOnly(2026, 1, 15), page.Items[0].Date);
    }

    [Fact]
    public async Task GetByFilterAsync_Paginates_CorrectTotalAndItems()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        for (int i = 0; i < 25; i++)
        {
            Income income = CreateIncome(
                _comidaCategoryId,
                new DateOnly(2026, 1, i + 1),
                100m * (i + 1),
                $"Ingreso {i + 1}");
            await repo.AddAsync(income);
        }

        var pageOneFilter = new IncomeFilter { Page = 1, PageSize = 10 };
        PagedResult<Income> pageOne = await repo.GetByFilterAsync(pageOneFilter);

        Assert.Equal(25, pageOne.Total);
        Assert.Equal(10, pageOne.Items.Count);
        Assert.Equal(3, pageOne.TotalPages);
        Assert.Equal(new DateOnly(2026, 1, 25), pageOne.Items[0].Date);
        Assert.Equal(new DateOnly(2026, 1, 16), pageOne.Items[^1].Date);

        var pageTwoFilter = new IncomeFilter { Page = 2, PageSize = 10 };
        PagedResult<Income> pageTwo = await repo.GetByFilterAsync(pageTwoFilter);

        Assert.Equal(25, pageTwo.Total);
        Assert.Equal(10, pageTwo.Items.Count);
        Assert.Equal(new DateOnly(2026, 1, 15), pageTwo.Items[0].Date);
        Assert.Equal(new DateOnly(2026, 1, 6), pageTwo.Items[^1].Date);

        var pageThreeFilter = new IncomeFilter { Page = 3, PageSize = 10 };
        PagedResult<Income> pageThree = await repo.GetByFilterAsync(pageThreeFilter);

        Assert.Equal(25, pageThree.Total);
        Assert.Equal(5, pageThree.Items.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), pageThree.Items[^1].Date);
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

        Income income1 = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 1), 100m, "Uno");
        income1.AddTag(tagA);
        Income income2 = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 2), 200m, "Dos");
        income2.AddTag(tagB);
        Income income3 = CreateIncome(_comidaCategoryId, new DateOnly(2026, 1, 3), 300m, "Tres");

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
    public async Task UpdateAsync_Existing_UpdatesFields()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Income income = CreateIncome(
            _comidaCategoryId,
            new DateOnly(2026, 1, 10),
            1000m,
            "Original",
            _fixedTimestamp);
        await repo.AddAsync(income);

        income.Update(
            _comidaCategoryId,
            new DateOnly(2026, 1, 11),
            Money.Create(2000m, Currency.DOP),
            "Actualizado",
            "Nota",
            "Sueldo",
            _fixedTimestamp.AddDays(1));

        Result result = await repo.UpdateAsync(income);

        Assert.True(result.IsSuccess);

        Income? updated = await repo.GetByIdAsync(income.Id);
        Assert.NotNull(updated);
        Assert.Equal(income.Id, updated.Id);
        Assert.Equal(new DateOnly(2026, 1, 11), updated.Date);
        Assert.Equal(2000m, updated.Amount.Amount);
        Assert.Equal(Currency.DOP, updated.Amount.Currency);
        Assert.Equal("Actualizado", updated.Description);
        Assert.Equal("Nota", updated.Note);
        Assert.Equal("Sueldo", updated.Source);
        Assert.Equal(_fixedTimestamp, updated.CreatedAt);
        Assert.Equal(_fixedTimestamp.AddDays(1), updated.UpdatedAt);
    }

    [Fact]
    public async Task RemoveAsync_Existing_DeletesIncome()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        Income income = CreateIncome(
            _comidaCategoryId,
            new DateOnly(2026, 1, 10),
            350m,
            "Para borrar");
        await repo.AddAsync(income);

        Result result = await repo.RemoveAsync(income.Id);

        Assert.True(result.IsSuccess);

        Income? deleted = await repo.GetByIdAsync(income.Id);
        Assert.Null(deleted);
    }

    private static Income CreateIncome(
        Guid? categoryId,
        DateOnly date,
        decimal amount,
        string description,
        DateTime? createdAt = null,
        string? note = null,
        string? source = null)
    {
        return Income.Create(
            categoryId,
            date,
            Money.Create(amount, Currency.DOP),
            description,
            note,
            source,
            createdAt ?? _fixedTimestamp);
    }

    private static Tag CreateTag(string name, string? color = null)
        => Tag.Create(name, color, _fixedTimestamp);

    private static async Task<Category> SeedCategoryAsync(AppDbContext ctx)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Test Category",
            Color = "#000000",
            Icon = "🧪",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

        ctx.Categories.Add(category);
        await ctx.SaveChangesAsync();

        return category;
    }
}
