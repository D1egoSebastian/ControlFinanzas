using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integración para <see cref="PaymentMethodRepository"/>.
/// </summary>
/// <remarks>
/// Usa SQLite in-memory con <see cref="TestAppDbContextFactory"/> y
/// valida los 5 métodos del repositorio más las reglas de negocio
/// definidas en F-004 de la Fase 2.
/// </remarks>
public class PaymentMethodRepositoryTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsPaymentMethod()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new PaymentMethodRepository(context);

        PaymentMethod? paymentMethod = await repo.GetByIdAsync(
            new Guid("22222222-2222-2222-2222-000000000001"));

        Assert.NotNull(paymentMethod);
        Assert.Equal("Efectivo", paymentMethod.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithMissingId_ReturnsNull()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new PaymentMethodRepository(context);

        PaymentMethod? paymentMethod = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(paymentMethod);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyGuid_ReturnsNull()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new PaymentMethodRepository(context);

        PaymentMethod? paymentMethod = await repo.GetByIdAsync(Guid.Empty);

        Assert.Null(paymentMethod);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllPaymentMethodsOrderedByName()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new PaymentMethodRepository(context);

        IReadOnlyList<PaymentMethod> paymentMethods = await repo.GetAllAsync();

        Assert.Equal(5, paymentMethods.Count);
        Assert.Equal(
            new[] { "Efectivo", "Otro", "Tarjeta de crédito", "Tarjeta de débito", "Transferencia" },
            paymentMethods.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task AddAsync_WithUniqueName_PersistsPaymentMethod()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new PaymentMethodRepository(context);

        var paymentMethod = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            Name = "Custom",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

        Result result = await repo.AddAsync(paymentMethod);

        Assert.True(result.IsSuccess);
        IReadOnlyList<PaymentMethod> all = await repo.GetAllAsync();
        Assert.Equal(6, all.Count);
    }

    [Fact]
    public async Task AddAsync_WithDuplicateName_ReturnsFailureWithDuplicateName()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new PaymentMethodRepository(context);

        var paymentMethod = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            Name = "Efectivo",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

        Result result = await repo.AddAsync(paymentMethod);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("DuplicateName", result.Error!.Code);
    }

    [Fact]
    public async Task AddAsync_WithNullPaymentMethod_ThrowsArgumentNullException()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new PaymentMethodRepository(context);

        ArgumentNullException ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => repo.AddAsync(null!));

        Assert.Contains("paymentMethod", ex.ParamName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_WithExistingId_UpdatesPaymentMethod()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new PaymentMethodRepository(context);

        var paymentMethod = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            Name = "Custom",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

        await repo.AddAsync(paymentMethod);

        var updated = new PaymentMethod
        {
            Id = paymentMethod.Id,
            Name = "Updated",
            IsSystem = false,
            CreatedAt = paymentMethod.CreatedAt,
            UpdatedAt = _fixedTimestamp,
        };

        Result result = await repo.UpdateAsync(updated);

        Assert.True(result.IsSuccess);
        PaymentMethod? persisted = await repo.GetByIdAsync(paymentMethod.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Updated", persisted.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithMissingId_ReturnsFailureWithNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new PaymentMethodRepository(context);

        var paymentMethod = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            Name = "Missing",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

        Result result = await repo.UpdateAsync(paymentMethod);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_WithSystemPaymentMethodRenamed_ReturnsFailureWithSystemPaymentMethodImmutable()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new PaymentMethodRepository(context);

        var paymentMethod = new PaymentMethod
        {
            Id = new Guid("22222222-2222-2222-2222-000000000001"),
            Name = "Dinero en efectivo",
            IsSystem = true,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

        Result result = await repo.UpdateAsync(paymentMethod);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("SystemPaymentMethodImmutable", result.Error!.Code);
    }

    [Fact]
    public async Task RemoveAsync_WithExistingId_DeletesPaymentMethod()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new PaymentMethodRepository(context);

        var paymentMethod = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            Name = "ToDelete",
            IsSystem = false,
            CreatedAt = _fixedTimestamp,
            UpdatedAt = _fixedTimestamp,
        };

        await repo.AddAsync(paymentMethod);

        Result result = await repo.RemoveAsync(paymentMethod.Id);

        Assert.True(result.IsSuccess);
        PaymentMethod? deleted = await repo.GetByIdAsync(paymentMethod.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task RemoveAsync_WithMissingId_ReturnsFailureWithNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new PaymentMethodRepository(context);

        Result result = await repo.RemoveAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task RemoveAsync_WithSystemPaymentMethod_ReturnsFailureWithSystemPaymentMethodImmutable()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();
        var repo = new PaymentMethodRepository(context);

        Result result = await repo.RemoveAsync(
            new Guid("22222222-2222-2222-2222-000000000001"));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("SystemPaymentMethodImmutable", result.Error!.Code);
    }
}
