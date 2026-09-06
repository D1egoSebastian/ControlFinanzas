using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integración para <see cref="TagRepository"/>.
/// </summary>
/// <remarks>
/// Usa SQLite in-memory con <see cref="TestAppDbContextFactory"/> y
/// valida los 5 métodos del repositorio definidos en F-004 de la Fase 4.
/// </remarks>
public sealed class TagRepositoryTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Tag CreateTag(string name, string? color = null)
        => Tag.Create(name, color, _fixedTimestamp);

    [Fact]
    public async Task AddAsync_WithUniqueName_Persists()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new TagRepository(context);

        Tag tag = CreateTag("Custom");

        Result result = await repo.AddAsync(tag);

        Assert.True(result.IsSuccess);
        Tag? persisted = await repo.GetByIdAsync(tag.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Custom", persisted!.Name);
    }

    [Fact]
    public async Task AddAsync_WithDuplicateName_ReturnsDuplicateName()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new TagRepository(context);

        await repo.AddAsync(CreateTag("Duplicate"));

        Result result = await repo.AddAsync(CreateTag("Duplicate", "#FF0000"));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("DuplicateName", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_Existing_UpdatesFields()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new TagRepository(context);

        Tag tag = CreateTag("Original", "#FF8800");
        await repo.AddAsync(tag);

        tag.Update("Updated", "#00AAFF");
        Result result = await repo.UpdateAsync(tag);

        Assert.True(result.IsSuccess);
        Tag? persisted = await repo.GetByIdAsync(tag.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Updated", persisted!.Name);
        Assert.Equal("#00AAFF", persisted.Color);
        Assert.Equal(tag.CreatedAt, persisted.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_Missing_ReturnsNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new TagRepository(context);

        Tag tag = CreateTag("Missing");
        Result result = await repo.UpdateAsync(tag);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task RemoveAsync_Existing_DeletesTag()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new TagRepository(context);

        Tag tag = CreateTag("ToDelete");
        await repo.AddAsync(tag);

        Result result = await repo.RemoveAsync(tag.Id);

        Assert.True(result.IsSuccess);
        Tag? deleted = await repo.GetByIdAsync(tag.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task RemoveAsync_NonExistent_ReturnsNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new TagRepository(context);

        Result result = await repo.RemoveAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task RemoveAsync_WithEmptyGuid_ReturnsNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new TagRepository(context);

        Result result = await repo.RemoveAsync(Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsTag()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new TagRepository(context);

        Tag tag = CreateTag("ById");
        await repo.AddAsync(tag);

        Tag? persisted = await repo.GetByIdAsync(tag.Id);

        Assert.NotNull(persisted);
        Assert.Equal("ById", persisted!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNull()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new TagRepository(context);

        Tag? tag = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(tag);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyGuid_ReturnsNull()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new TagRepository(context);

        Tag? tag = await repo.GetByIdAsync(Guid.Empty);

        Assert.Null(tag);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllOrderedByName()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new TagRepository(context);

        await repo.AddAsync(CreateTag("Zebra"));
        await repo.AddAsync(CreateTag("Apple"));
        await repo.AddAsync(CreateTag("Mango"));

        IReadOnlyList<Tag> tags = await repo.GetAllAsync();

        Assert.Equal(3, tags.Count);
        Assert.Equal(new[] { "Apple", "Mango", "Zebra" }, tags.Select(t => t.Name).ToArray());
    }
}
