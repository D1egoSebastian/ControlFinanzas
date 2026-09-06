using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integración para <see cref="ExpenseRepository"/>.
/// </summary>
/// <remarks>
    /// Usa SQLite in-memory con <see cref="TestAppDbContextFactory"/> y valida
    /// CRUD, validación de FK a <see cref="Category"/>, filtros por rango de
    /// fecha y paginación.
/// </remarks>
public sealed class ExpenseRepositoryTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _comidaCategoryId = new("11111111-1111-1111-1111-000000000001");

    [Fact]
    public async Task AddAsync_WithValidExpense_Persists()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Expense expense = CreateExpense(
            _comidaCategoryId,
            new DateOnly(2026, 1, 15),
            1500.50m,
            "Almuerzo de trabajo");

        Result result = await repo.AddAsync(expense);

        Assert.True(result.IsSuccess);

        Expense? persisted = await repo.GetByIdAsync(expense.Id);
        Assert.NotNull(persisted);
        Assert.Equal(expense.Id, persisted.Id);
        Assert.Equal(_comidaCategoryId, persisted.CategoryId);
        Assert.Equal(new DateOnly(2026, 1, 15), persisted.Date);
        Assert.Equal(1500.50m, persisted.Amount.Amount);
        Assert.Equal(Currency.DOP, persisted.Amount.Currency);
        Assert.Equal("Almuerzo de trabajo", persisted.Description);
        Assert.Null(persisted.Note);
    }

    [Fact]
    public async Task AddAsync_WithUnknownCategoryId_ReturnsCategoryNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Expense expense = CreateExpense(
            Guid.NewGuid(),
            new DateOnly(2026, 1, 15),
            500m,
            "Gasto sin categoría");

        Result result = await repo.AddAsync(expense);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("CategoryNotFound", result.Error!.Code);
        Assert.False(string.IsNullOrEmpty(result.Error.Message));
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsExpense()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Expense expense = CreateExpense(
            _comidaCategoryId,
            new DateOnly(2026, 2, 10),
            250m,
            "Café");
        await repo.AddAsync(expense);

        Expense? retrieved = await repo.GetByIdAsync(expense.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(expense.Id, retrieved.Id);
        Assert.Equal("Café", retrieved.Description);
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNull()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Expense? retrieved = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllOrderedByDateDesc()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Expense first = CreateExpense(_comidaCategoryId, new DateOnly(2026, 1, 1), 100m, "Primero");
        Expense second = CreateExpense(_comidaCategoryId, new DateOnly(2026, 1, 3), 200m, "Segundo");
        Expense third = CreateExpense(_comidaCategoryId, new DateOnly(2026, 1, 2), 150m, "Tercero");

        await repo.AddAsync(first);
        await repo.AddAsync(second);
        await repo.AddAsync(third);

        IReadOnlyList<Expense> all = await repo.GetAllAsync();

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
        var repo = new ExpenseRepository(context);

        Expense first = CreateExpense(_comidaCategoryId, new DateOnly(2026, 1, 1), 100m, "Primero");
        Expense second = CreateExpense(_comidaCategoryId, new DateOnly(2026, 1, 3), 200m, "Segundo");
        Expense third = CreateExpense(_comidaCategoryId, new DateOnly(2026, 1, 2), 150m, "Tercero");

        await repo.AddAsync(first);
        await repo.AddAsync(second);
        await repo.AddAsync(third);

        var filter = new ExpenseFilter { Page = 1, PageSize = 50 };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

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
    public async Task GetByFilterAsync_WithDateRange_FiltersCorrectly()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Expense before = CreateExpense(_comidaCategoryId, new DateOnly(2026, 1, 1), 100m, "Antes");
        Expense inside = CreateExpense(_comidaCategoryId, new DateOnly(2026, 1, 15), 200m, "Dentro");
        Expense after = CreateExpense(_comidaCategoryId, new DateOnly(2026, 1, 31), 300m, "Después");

        await repo.AddAsync(before);
        await repo.AddAsync(inside);
        await repo.AddAsync(after);

        var filter = new ExpenseFilter
        {
            Page = 1,
            PageSize = 50,
            DateFrom = new DateOnly(2026, 1, 10),
            DateTo = new DateOnly(2026, 1, 20),
        };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

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
        var repo = new ExpenseRepository(context);

        for (int i = 0; i < 5; i++)
        {
            Expense expense = CreateExpense(
                _comidaCategoryId,
                new DateOnly(2026, 1, i + 1),
                100m * (i + 1),
                $"Gasto {i + 1}");
            await repo.AddAsync(expense);
        }

        var pageOneFilter = new ExpenseFilter { Page = 1, PageSize = 2 };
        PagedResult<Expense> pageOne = await repo.GetByFilterAsync(pageOneFilter);

        Assert.Equal(5, pageOne.Total);
        Assert.Equal(2, pageOne.Items.Count);
        Assert.Equal(3, pageOne.TotalPages);
        Assert.Equal(new DateOnly(2026, 1, 5), pageOne.Items[0].Date);
        Assert.Equal(new DateOnly(2026, 1, 4), pageOne.Items[1].Date);

        var pageTwoFilter = new ExpenseFilter { Page = 2, PageSize = 2 };
        PagedResult<Expense> pageTwo = await repo.GetByFilterAsync(pageTwoFilter);

        Assert.Equal(5, pageTwo.Total);
        Assert.Equal(2, pageTwo.Items.Count);
        Assert.Equal(new DateOnly(2026, 1, 3), pageTwo.Items[0].Date);
        Assert.Equal(new DateOnly(2026, 1, 2), pageTwo.Items[1].Date);

        var pageThreeFilter = new ExpenseFilter { Page = 3, PageSize = 2 };
        PagedResult<Expense> pageThree = await repo.GetByFilterAsync(pageThreeFilter);

        Assert.Equal(5, pageThree.Total);
        Assert.Single(pageThree.Items);
        Assert.Equal(new DateOnly(2026, 1, 1), pageThree.Items[0].Date);
    }

    [Fact]
    public async Task UpdateAsync_Existing_UpdatesFields()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Expense expense = CreateExpense(
            _comidaCategoryId,
            new DateOnly(2026, 1, 10),
            1000m,
            "Original",
            _fixedTimestamp);
        await repo.AddAsync(expense);

        expense.Update(
            _comidaCategoryId,
            new DateOnly(2026, 1, 11),
            Money.Create(2000m, Currency.DOP),
            "Actualizado",
            "Nota actualizada",
            _fixedTimestamp.AddDays(1));

        Result result = await repo.UpdateAsync(expense);

        Assert.True(result.IsSuccess);

        Expense? updated = await repo.GetByIdAsync(expense.Id);
        Assert.NotNull(updated);
        Assert.Equal(expense.Id, updated.Id);
        Assert.Equal(new DateOnly(2026, 1, 11), updated.Date);
        Assert.Equal(2000m, updated.Amount.Amount);
        Assert.Equal("Actualizado", updated.Description);
        Assert.Equal("Nota actualizada", updated.Note);
        Assert.Equal(_fixedTimestamp, updated.CreatedAt);
        Assert.Equal(_fixedTimestamp.AddDays(1), updated.UpdatedAt);
    }

    [Fact]
    public async Task RemoveAsync_Existing_DeletesExpense()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        Expense expense = CreateExpense(
            _comidaCategoryId,
            new DateOnly(2026, 1, 10),
            350m,
            "Para borrar");
        await repo.AddAsync(expense);

        Result result = await repo.RemoveAsync(expense.Id);

        Assert.True(result.IsSuccess);

        Expense? deleted = await repo.GetByIdAsync(expense.Id);
        Assert.Null(deleted);
    }

    private static Expense CreateExpense(
        Guid categoryId,
        DateOnly date,
        decimal amount,
        string description,
        DateTime? createdAt = null,
        string? note = null)
    {
        return Expense.Create(
            categoryId,
            date,
            Money.Create(amount, Currency.DOP),
            description,
            note,
            createdAt ?? _fixedTimestamp);
    }
}
