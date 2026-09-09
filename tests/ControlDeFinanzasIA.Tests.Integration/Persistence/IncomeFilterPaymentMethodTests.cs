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
/// Tests de integración para el filtro <see cref="IncomeFilter.PaymentMethodIds"/>
/// en <see cref="IncomeRepository.GetByFilterAsync"/>. Fase 5 feature 5-006.
/// </summary>
public sealed class IncomeFilterPaymentMethodTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Categoría seed del sistema (Comida) — existe en la BD gracias a
    // SeedData. Usamos el ID deterministico para que IncomeRepository.AddAsync
    // valide correctamente el FK.
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

    private static Income CreateIncome(
        Guid? categoryId,
        string description,
        Guid? paymentMethodId = null)
    {
        return Income.Create(
            categoryId,
            new DateOnly(2026, 1, 15),
            Money.Create(100m, Currency.DOP),
            description,
            null,
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
        var repo = new IncomeRepository(context);

        PaymentMethod transferencia = CreateMethod("Transferencia");
        PaymentMethod cash = CreateMethod("Cash");
        context.PaymentMethods.AddRange(transferencia, cash);
        await context.SaveChangesAsync();

        Income i1 = CreateIncome(_comidaCategoryId, "I1", transferencia.Id);
        Income i2 = CreateIncome(_comidaCategoryId, "I2", cash.Id);
        Income i3 = CreateIncome(null, "I3");   // sin método, sin categoría
        await repo.AddAsync(i1);
        await repo.AddAsync(i2);
        await repo.AddAsync(i3);

        var filter = new IncomeFilter { PaymentMethodIds = new[] { transferencia.Id } };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(i1.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task GetByFilterAsync_WithMultiplePaymentMethodIds_ReturnsIncomesWithAny()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        PaymentMethod transferencia = CreateMethod("Transferencia");
        PaymentMethod cash = CreateMethod("Cash");
        context.PaymentMethods.AddRange(transferencia, cash);
        await context.SaveChangesAsync();

        Income i1 = CreateIncome(_comidaCategoryId, "I1", transferencia.Id);
        Income i2 = CreateIncome(_comidaCategoryId, "I2", cash.Id);
        Income i3 = CreateIncome(_comidaCategoryId, "I3");
        await repo.AddAsync(i1);
        await repo.AddAsync(i2);
        await repo.AddAsync(i3);

        var filter = new IncomeFilter
        {
            PaymentMethodIds = new[] { transferencia.Id, cash.Id },
        };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(2, page.Total);
        Assert.Contains(page.Items, i => i.Id == i1.Id);
        Assert.Contains(page.Items, i => i.Id == i2.Id);
    }

    [Fact]
    public async Task GetByFilterAsync_ExcludesIncomesWithoutPaymentMethod()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        PaymentMethod transferencia = CreateMethod("Transferencia");
        context.PaymentMethods.Add(transferencia);
        await context.SaveChangesAsync();

        Income iWithMethod = CreateIncome(_comidaCategoryId, "With", transferencia.Id);
        Income iWithoutMethod = CreateIncome(_comidaCategoryId, "Without");
        await repo.AddAsync(iWithMethod);
        await repo.AddAsync(iWithoutMethod);

        var filter = new IncomeFilter { PaymentMethodIds = new[] { transferencia.Id } };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(1, page.Total);
        Assert.Equal(iWithMethod.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task GetByFilterAsync_WithEmptyPaymentMethodIds_ReturnsAll()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        PaymentMethod transferencia = CreateMethod("Transferencia");
        context.PaymentMethods.Add(transferencia);
        await context.SaveChangesAsync();

        Income i1 = CreateIncome(_comidaCategoryId, "I1", transferencia.Id);
        Income i2 = CreateIncome(_comidaCategoryId, "I2");
        await repo.AddAsync(i1);
        await repo.AddAsync(i2);

        var filter = new IncomeFilter { PaymentMethodIds = Array.Empty<Guid>() };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task GetByFilterAsync_WithNullPaymentMethodIds_ReturnsAll()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        PaymentMethod transferencia = CreateMethod("Transferencia");
        context.PaymentMethods.Add(transferencia);
        await context.SaveChangesAsync();

        Income i1 = CreateIncome(_comidaCategoryId, "I1", transferencia.Id);
        Income i2 = CreateIncome(_comidaCategoryId, "I2");
        await repo.AddAsync(i1);
        await repo.AddAsync(i2);

        var filter = new IncomeFilter { PaymentMethodIds = null };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task GetByFilterAsync_WithNonExistentPaymentMethodId_ReturnsEmpty()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new IncomeRepository(context);

        PaymentMethod transferencia = CreateMethod("Transferencia");
        context.PaymentMethods.Add(transferencia);
        await context.SaveChangesAsync();

        Income i1 = CreateIncome(_comidaCategoryId, "I1", transferencia.Id);
        await repo.AddAsync(i1);

        var filter = new IncomeFilter { PaymentMethodIds = new[] { Guid.NewGuid() } };
        PagedResult<Income> page = await repo.GetByFilterAsync(filter);

        Assert.Equal(0, page.Total);
        Assert.Empty(page.Items);
    }
}
