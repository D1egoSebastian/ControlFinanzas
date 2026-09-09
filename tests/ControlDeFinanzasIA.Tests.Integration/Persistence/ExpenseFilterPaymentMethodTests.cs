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
/// Tests de integración para el filtro <see cref="ExpenseFilter.PaymentMethodIds"/>
/// en <see cref="ExpenseRepository.GetByFilterAsync"/>. Fase 5 feature 5-006.
/// </summary>
public sealed class ExpenseFilterPaymentMethodTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _comidaCategoryId = new("11111111-1111-1111-1111-000000000001");

    private static PaymentMethod CreateMethod(string name)
        => new()
        {
            // Prefijo único para no colisionar con los seeds del sistema
            // (Efectivo, Tarjeta de débito, Tarjeta de crédito, Transferencia, Otro).
            Id = Guid.NewGuid(),
            Name = $"T_{name}",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

    private static Expense CreateExpense(
        Guid categoryId,
        string description,
        Guid? paymentMethodId = null)
    {
        return Expense.Create(
            categoryId,
            new DateOnly(2026, 1, 15),
            Money.Create(100m, Currency.DOP),
            description,
            null,
            _fixedTimestamp,
            paymentMethodId);
    }

    [Fact]
    public async Task GetByFilterAsync_WithSinglePaymentMethodId_ReturnsOnlyMatching()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        PaymentMethod efectivo = CreateMethod("Efectivo");
        PaymentMethod tarjeta = CreateMethod("Tarjeta");
        context.PaymentMethods.AddRange(efectivo, tarjeta);
        await context.SaveChangesAsync();

        Expense e1 = CreateExpense(_comidaCategoryId, "E1", efectivo.Id);
        Expense e2 = CreateExpense(_comidaCategoryId, "E2", tarjeta.Id);
        Expense e3 = CreateExpense(_comidaCategoryId, "E3");   // sin método
        await repo.AddAsync(e1);
        await repo.AddAsync(e2);
        await repo.AddAsync(e3);

        var filter = new ExpenseFilter { PaymentMethodIds = new[] { efectivo.Id } };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(e1.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task GetByFilterAsync_WithMultiplePaymentMethodIds_ReturnsExpensesWithAny()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        PaymentMethod efectivo = CreateMethod("Efectivo");
        PaymentMethod tarjeta = CreateMethod("Tarjeta");
        context.PaymentMethods.AddRange(efectivo, tarjeta);
        await context.SaveChangesAsync();

        Expense e1 = CreateExpense(_comidaCategoryId, "E1", efectivo.Id);
        Expense e2 = CreateExpense(_comidaCategoryId, "E2", tarjeta.Id);
        Expense e3 = CreateExpense(_comidaCategoryId, "E3");
        await repo.AddAsync(e1);
        await repo.AddAsync(e2);
        await repo.AddAsync(e3);

        var filter = new ExpenseFilter
        {
            PaymentMethodIds = new[] { efectivo.Id, tarjeta.Id },
        };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.Contains(page.Items, e => e.Id == e1.Id);
        Assert.Contains(page.Items, e => e.Id == e2.Id);
    }

    [Fact]
    public async Task GetByFilterAsync_ExcludesExpensesWithoutPaymentMethod()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        PaymentMethod efectivo = CreateMethod("Efectivo");
        context.PaymentMethods.Add(efectivo);
        await context.SaveChangesAsync();

        Expense eWithMethod = CreateExpense(_comidaCategoryId, "With", efectivo.Id);
        Expense eWithoutMethod = CreateExpense(_comidaCategoryId, "Without");
        await repo.AddAsync(eWithMethod);
        await repo.AddAsync(eWithoutMethod);

        var filter = new ExpenseFilter { PaymentMethodIds = new[] { efectivo.Id } };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(1, page.Total);
        Assert.Equal(eWithMethod.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task GetByFilterAsync_WithEmptyPaymentMethodIds_ReturnsAll()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        PaymentMethod efectivo = CreateMethod("Efectivo");
        context.PaymentMethods.Add(efectivo);
        await context.SaveChangesAsync();

        Expense e1 = CreateExpense(_comidaCategoryId, "E1", efectivo.Id);
        Expense e2 = CreateExpense(_comidaCategoryId, "E2");
        await repo.AddAsync(e1);
        await repo.AddAsync(e2);

        var filter = new ExpenseFilter { PaymentMethodIds = Array.Empty<Guid>() };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task GetByFilterAsync_WithNullPaymentMethodIds_ReturnsAll()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        PaymentMethod efectivo = CreateMethod("Efectivo");
        context.PaymentMethods.Add(efectivo);
        await context.SaveChangesAsync();

        Expense e1 = CreateExpense(_comidaCategoryId, "E1", efectivo.Id);
        Expense e2 = CreateExpense(_comidaCategoryId, "E2");
        await repo.AddAsync(e1);
        await repo.AddAsync(e2);

        var filter = new ExpenseFilter { PaymentMethodIds = null };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task GetByFilterAsync_WithNonExistentPaymentMethodId_ReturnsEmpty()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new ExpenseRepository(context);

        PaymentMethod efectivo = CreateMethod("Efectivo");
        context.PaymentMethods.Add(efectivo);
        await context.SaveChangesAsync();

        Expense e1 = CreateExpense(_comidaCategoryId, "E1", efectivo.Id);
        await repo.AddAsync(e1);

        var filter = new ExpenseFilter { PaymentMethodIds = new[] { Guid.NewGuid() } };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(0, page.Total);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task GetByFilterAsync_WithCombinedCategoryAndPaymentMethodIds_ReturnsIntersection()
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
            Icon = "X",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };
        var categoryY = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Category Y",
            Color = "#000000",
            Icon = "Y",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };
        context.Categories.AddRange(categoryX, categoryY);

        PaymentMethod efectivo = CreateMethod("Efectivo");
        context.PaymentMethods.Add(efectivo);
        await context.SaveChangesAsync();

        Expense e1 = CreateExpense(categoryX.Id, "X+Efectivo", efectivo.Id);
        Expense e2 = CreateExpense(categoryY.Id, "Y+Efectivo", efectivo.Id);
        Expense e3 = CreateExpense(categoryX.Id, "X+None");
        await repo.AddAsync(e1);
        await repo.AddAsync(e2);
        await repo.AddAsync(e3);

        var filter = new ExpenseFilter
        {
            CategoryId = categoryX.Id,
            PaymentMethodIds = new[] { efectivo.Id },
        };
        PagedResult<Expense> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(1, page.Total);
        Assert.Equal(e1.Id, page.Items[0].Id);
    }
}
