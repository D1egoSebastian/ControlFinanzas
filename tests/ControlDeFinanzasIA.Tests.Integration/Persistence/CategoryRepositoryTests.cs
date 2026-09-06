using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integración para <see cref="CategoryRepository"/>.
/// </summary>
/// <remarks>
/// Usa SQLite in-memory con <see cref="TestAppDbContextFactory"/> y
/// valida los 5 métodos del repositorio más las reglas de negocio
/// definidas en F-004 de la Fase 2.
/// </remarks>
public class CategoryRepositoryTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsCategory()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new CategoryRepository(context);

        Category? category = await repo.GetByIdAsync(
            new Guid("11111111-1111-1111-1111-000000000001"));

        Assert.NotNull(category);
        Assert.Equal("Comida", category.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithMissingId_ReturnsNull()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new CategoryRepository(context);

        Category? category = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(category);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyGuid_ReturnsNull()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new CategoryRepository(context);

        Category? category = await repo.GetByIdAsync(Guid.Empty);

        Assert.Null(category);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllCategoriesOrderedByName()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new CategoryRepository(context);

        IReadOnlyList<Category> categories = await repo.GetAllAsync();

        Assert.Equal(9, categories.Count);
        Assert.Equal(
            new[] { "Comida", "Educación", "Hogar", "Ocio", "Otros", "Ropa", "Salud", "Servicios", "Transporte" },
            categories.Select(c => c.Name).ToArray());
    }

    [Fact]
    public async Task AddAsync_WithUniqueName_PersistsCategory()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new CategoryRepository(context);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Custom",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

        Result result = await repo.AddAsync(category);

        Assert.True(result.IsSuccess);
        IReadOnlyList<Category> all = await repo.GetAllAsync();
        Assert.Equal(10, all.Count);
    }

    [Fact]
    public async Task AddAsync_WithDuplicateName_ReturnsFailureWithDuplicateName()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new CategoryRepository(context);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Comida",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

        Result result = await repo.AddAsync(category);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("DuplicateName", result.Error!.Code);
    }

    [Fact]
    public async Task AddAsync_WithNullCategory_ThrowsArgumentNullException()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new CategoryRepository(context);

        ArgumentNullException ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => repo.AddAsync(null!));

        Assert.Contains("category", ex.ParamName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_WithExistingId_UpdatesCategory()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new CategoryRepository(context);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Custom",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

        await repo.AddAsync(category);

        var updated = new Category
        {
            Id = category.Id,
            Name = "Updated",
            IsSystem = false,
            CreatedAt = category.CreatedAt,
            UpdatedAt = _fixedTimestamp,
        };

        Result result = await repo.UpdateAsync(updated);

        Assert.True(result.IsSuccess);
        Category? persisted = await repo.GetByIdAsync(category.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Updated", persisted.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithMissingId_ReturnsFailureWithNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new CategoryRepository(context);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Missing",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

        Result result = await repo.UpdateAsync(category);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_WithSystemCategoryRenamed_ReturnsFailureWithSystemCategoryImmutable()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new CategoryRepository(context);

        var category = new Category
        {
            Id = new Guid("11111111-1111-1111-1111-000000000001"),
            Name = "Alimentación",
            IsSystem = true,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

        Result result = await repo.UpdateAsync(category);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("SystemCategoryImmutable", result.Error!.Code);
    }

    [Fact]
    public async Task RemoveAsync_WithExistingId_DeletesCategory()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new CategoryRepository(context);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "ToDelete",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

        await repo.AddAsync(category);

        Result result = await repo.RemoveAsync(category.Id);

        Assert.True(result.IsSuccess);
        Category? deleted = await repo.GetByIdAsync(category.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task RemoveAsync_WithMissingId_ReturnsFailureWithNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new CategoryRepository(context);

        Result result = await repo.RemoveAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task RemoveAsync_WithSystemCategory_ReturnsFailureWithSystemCategoryImmutable()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new CategoryRepository(context);

        Result result = await repo.RemoveAsync(
            new Guid("11111111-1111-1111-1111-000000000001"));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("SystemCategoryImmutable", result.Error!.Code);
    }
}
