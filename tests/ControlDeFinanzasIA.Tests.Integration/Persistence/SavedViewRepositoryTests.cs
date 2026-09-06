using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integración para <see cref="SavedViewRepository"/>.
/// </summary>
/// <remarks>
/// Usa SQLite in-memory con <see cref="TestAppDbContextFactory"/> y
/// valida los 6 métodos del repositorio definidos en F-006 de la Fase 4.
/// </remarks>
public sealed class SavedViewRepositoryTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _fixedTimestamp2 = new(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

    private const string _defaultFilterJson = """
        {"schemaVersion":1,"type":"ExpenseFilter","data":{}}
        """;

    private const string _roundTripFilterJson = """
        {"schemaVersion":1,"type":"ExpenseFilter","data":{"page":2,"pageSize":25,"categoryId":null,"dateFrom":null,"dateTo":null,"tagIds":null}}
        """;

    private static SavedView CreateSavedView(
        string name,
        TransactionType entityType,
        string? filterJson = null)
        => SavedView.Create(name, entityType, filterJson ?? _defaultFilterJson, _fixedTimestamp);

    [Fact]
    public async Task AddAsync_WithUniqueName_Persists()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new SavedViewRepository(context);

        SavedView view = CreateSavedView("Transporte", TransactionType.Expense);

        Result result = await repo.AddAsync(view);

        Assert.True(result.IsSuccess);
        SavedView? persisted = await repo.GetByIdAsync(view.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Transporte", persisted!.Name);
        Assert.Equal(_defaultFilterJson, persisted.FilterJson);
        Assert.Equal(TransactionType.Expense, persisted.EntityType);
    }

    [Fact]
    public async Task AddAsync_WithDuplicateName_ReturnsDuplicateName()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new SavedViewRepository(context);

        await repo.AddAsync(CreateSavedView("Duplicate", TransactionType.Expense));

        Result result = await repo.AddAsync(CreateSavedView("Duplicate", TransactionType.Income));

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
        var repo = new SavedViewRepository(context);

        SavedView view = CreateSavedView("Original", TransactionType.Expense);
        await repo.AddAsync(view);

        const string newFilter = """
            {"schemaVersion":1,"type":"ExpenseFilter","data":{"page":3}}
            """;
        view.Update("Renamed", newFilter, _fixedTimestamp2);
        Result result = await repo.UpdateAsync(view);

        Assert.True(result.IsSuccess);
        SavedView? persisted = await repo.GetByIdAsync(view.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Renamed", persisted!.Name);
        Assert.Equal(newFilter, persisted.FilterJson);
        Assert.Equal(_fixedTimestamp2, persisted.UpdatedAt);
        Assert.Equal(TransactionType.Expense, persisted.EntityType);
        Assert.Equal(1, persisted.SchemaVersion);
        Assert.Equal(_fixedTimestamp, persisted.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_Missing_ReturnsNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new SavedViewRepository(context);

        SavedView view = CreateSavedView("Missing", TransactionType.Expense);
        Result result = await repo.UpdateAsync(view);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task RemoveAsync_Existing_DeletesSavedView()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new SavedViewRepository(context);

        SavedView view = CreateSavedView("ToDelete", TransactionType.Expense);
        await repo.AddAsync(view);

        Result result = await repo.RemoveAsync(view.Id);

        Assert.True(result.IsSuccess);
        SavedView? deleted = await repo.GetByIdAsync(view.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task RemoveAsync_NonExistent_ReturnsNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new SavedViewRepository(context);

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
        var repo = new SavedViewRepository(context);

        Result result = await repo.RemoveAsync(Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsSavedView()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new SavedViewRepository(context);

        SavedView view = CreateSavedView("ById", TransactionType.Expense);
        await repo.AddAsync(view);

        SavedView? persisted = await repo.GetByIdAsync(view.Id);

        Assert.NotNull(persisted);
        Assert.Equal("ById", persisted!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyGuid_ReturnsNull()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new SavedViewRepository(context);

        SavedView? view = await repo.GetByIdAsync(Guid.Empty);

        Assert.Null(view);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllOrderedByName()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new SavedViewRepository(context);

        await repo.AddAsync(CreateSavedView("Zebra", TransactionType.Expense));
        await repo.AddAsync(CreateSavedView("Apple", TransactionType.Income));
        await repo.AddAsync(CreateSavedView("Mango", TransactionType.Expense));

        IReadOnlyList<SavedView> views = await repo.GetAllAsync();

        Assert.Equal(3, views.Count);
        Assert.Equal(new[] { "Apple", "Mango", "Zebra" }, views.Select(v => v.Name).ToArray());
    }

    [Fact]
    public async Task GetByEntityTypeAsync_ReturnsMatchingType()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new SavedViewRepository(context);

        await repo.AddAsync(CreateSavedView("ExpenseA", TransactionType.Expense));
        await repo.AddAsync(CreateSavedView("ExpenseB", TransactionType.Expense));
        await repo.AddAsync(CreateSavedView("IncomeA", TransactionType.Income));

        IReadOnlyList<SavedView> views = await repo.GetByEntityTypeAsync(TransactionType.Expense);

        Assert.Equal(2, views.Count);
        Assert.All(views, v => Assert.Equal(TransactionType.Expense, v.EntityType));
        Assert.Equal(new[] { "ExpenseA", "ExpenseB" }, views.Select(v => v.Name).ToArray());
    }

    [Fact]
    public async Task FilterJson_RoundTripsThroughDatabase()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new SavedViewRepository(context);

        SavedView view = CreateSavedView("RoundTrip", TransactionType.Expense, _roundTripFilterJson);
        await repo.AddAsync(view);

        context.ChangeTracker.Clear();
        SavedView? persisted = await repo.GetByIdAsync(view.Id);

        Assert.NotNull(persisted);
        Assert.Equal(_roundTripFilterJson, persisted!.FilterJson);
    }
}
